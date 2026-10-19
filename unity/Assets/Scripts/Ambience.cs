using System.Collections.Generic;
using Oohra.Core.Characters;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Zvuky okolí (prototyp sfx): praskání nejbližšího ohně, moře u pobřeží, kroky postavy, kručení v břiše při
    /// hladu, zachrochtání divočáka, který se zaměřil, vytí vlka, který se plíží, a zvuky vlastních činností
    /// (sekera, kámen, výroba z kamene, stavba). Déšť a hrom hraje WeatherView. Nahrávky jsou v katalogu zvuků
    /// (SoundCatalog), co v něm chybí, se skládá v kódu (šum, tóny). Hlasitost podle SyllableVoice.Volume.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const float FireHearDistance = 12f;
        private const float AnimalHearDistance = 22f;
        // Moře je slyšet do této vzdálenosti od nejbližší mořské dlaždice (metry).
        private const int SeaHearDistance = 30;
        // Krok zazní po každém kusu cesty (metry).
        private const float StepMeters = 0.9f;
        // Kručení v břiše: pod touhle sytostí, jednou za chvíli (sekundy).
        private const float HungrySatiety = 35f;
        private const float GrowlMinSeconds = 40f;
        private const float GrowlMaxSeconds = 100f;

        private static AudioClip _crackle;
        private static AudioClip _hiss;
        private static AudioClip _step;
        private static AudioClip _grunt;
        private static AudioClip _howl;
        private static AudioClip _rain;
        private static AudioClip _chop;
        private static AudioClip _buzz;
        private static AudioClip _creak;
        private static AudioClip _crash;
        private static AudioClip _thunder;

        private readonly Dictionary<int, AnimalState> _animalStates = new Dictionary<int, AnimalState>();
        private GameConnection _connection;
        private PlayerViews _players;
        private AudioSource _fire;
        private LoopSound _fireLoop;
        private LoopSound _sea;
        private float _seaDistance = float.MaxValue;
        private float _nextSeaCheck;
        private float _nextGrowl;
        private Vector3 _lastSelf;
        private float _walked;
        private ClientSession _session;

        // Vyrobená věc, u které se ještě neví, jestli byla z kamene (recept přijde ze serveru až po hlášce).
        private int _pendingRecipe = -1;
        private float _pendingUntil;

        public void Init(GameConnection connection, PlayerViews players, ClientSession session = null)
        {
            _connection = connection;
            _players = players;
            _session = session;
            if (session != null)
            {
                session.EventReceived += OnEvent;
            }

            var sounds = SoundCatalog.Current;
            if (sounds.FireLoop != null)
            {
                _fireLoop = LoopSound.Create(transform, "Oheň (zvuk)", sounds.FireLoop, 1f, 2f, FireHearDistance);
            }
            else
            {
                _fire = gameObject.AddComponent<AudioSource>();
                _fire.clip = Crackle();
                _fire.loop = true;
                _fire.spatialBlend = 1f;
                _fire.minDistance = 2f;
                _fire.maxDistance = FireHearDistance;
                _fire.rolloffMode = AudioRolloffMode.Linear;
            }

            if (sounds.SeaLoop != null)
            {
                _sea = LoopSound.Create(transform, "Moře (zvuk)", sounds.SeaLoop, 0f);
            }

            _nextGrowl = Time.time + Random.Range(GrowlMinSeconds, GrowlMaxSeconds) / 2f;
        }

        private void Update()
        {
            if (_connection == null || _players == null || _players.Self == null || DreamView.Active)
            {
                if (_fireLoop != null) _fireLoop.Stop();
                if (_sea != null) _sea.Stop();
                return;
            }

            var self = _players.Self.position;
            UpdateFire(self);
            UpdateSea(self);
            UpdateSteps(self);
            UpdateStomach(self);
            UpdateAnimals(self);
            UpdatePendingCraft();
        }

        /// <summary>Praskání u nejbližšího hořícího ohně (i ohniště táborů).</summary>
        private void UpdateFire(Vector3 self)
        {
            Vector3? nearest = null;
            var best = FireHearDistance;
            foreach (var fire in _connection.Fires)
            {
                Consider(fire.Burning, WorldCoords.ToUnity(fire.X, fire.Y), self, ref nearest, ref best);
            }

            foreach (var camp in _connection.World.Camps)
            {
                Consider(true, WorldCoords.ToUnity(camp.X + 0.5, camp.Y + 0.5), self, ref nearest, ref best);
            }

            if (_fireLoop != null)
            {
                if (nearest.HasValue)
                {
                    _fireLoop.transform.position = nearest.Value;
                    _fireLoop.Volume = SyllableVoice.Volume * SoundCatalog.Current.FireVolume;
                    _fireLoop.Play();
                }
                else
                {
                    _fireLoop.Stop();
                }

                return;
            }

            if (nearest.HasValue)
            {
                _fire.transform.position = nearest.Value;
                _fire.volume = SyllableVoice.Volume * 0.5f;
                if (!_fire.isPlaying) _fire.Play();
            }
            else if (_fire.isPlaying)
            {
                _fire.Stop();
            }
        }

        private static void Consider(bool burning, Vector3 at, Vector3 self, ref Vector3? nearest, ref float best)
        {
            var distance = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(self.x, self.z));
            if (burning && distance < best)
            {
                best = distance;
                nearest = at;
            }
        }

        /// <summary>Moře: sílí, čím blíž je postava k moři (v jeskyni ho není slyšet).</summary>
        private void UpdateSea(Vector3 self)
        {
            if (_sea == null)
            {
                return;
            }

            double x, y;
            WorldCoords.FromUnity(self, out x, out y);
            if (Time.time >= _nextSeaCheck)
            {
                // Hledání moře stačí dvakrát za sekundu.
                _nextSeaCheck = Time.time + 0.5f;
                _seaDistance = CaveView.IsCave(_connection.World, x, y) ? float.MaxValue : SeaDistance(_connection.World, x, y, SeaHearDistance);
            }

            var near = Mathf.Clamp01(1f - _seaDistance / SeaHearDistance);
            _sea.Volume = SyllableVoice.Volume * SoundCatalog.Current.SeaVolume * near * near;
            if (near > 0f) _sea.Play();
            else _sea.Stop();
        }

        /// <summary>Vzdálenost k nejbližší mořské dlaždici do range metrů (dál = float.MaxValue).</summary>
        public static float SeaDistance(WorldView world, double x, double y, int range)
        {
            var best = float.MaxValue;
            var (cx, cy) = ((int)System.Math.Floor(x), (int)System.Math.Floor(y));
            for (var ty = cy - range; ty <= cy + range; ty++)
            {
                for (var tx = cx - range; tx <= cx + range; tx++)
                {
                    if (tx < 0 || ty < 0 || tx >= world.Width || ty >= world.Height || world.TileAt(tx, ty) != TileType.Sea)
                    {
                        continue;
                    }

                    var dx = tx + 0.5 - x;
                    var dy = ty + 0.5 - y;
                    var distance = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    if (distance < best) best = distance;
                }
            }

            return best;
        }

        /// <summary>Kroky po každém kusu cesty: nahrávka podle země pod nohama, jinak tiché ťuknutí.</summary>
        private void UpdateSteps(Vector3 self)
        {
            var moved = Vector2.Distance(new Vector2(self.x, self.z), new Vector2(_lastSelf.x, _lastSelf.z));
            _lastSelf = self;
            if (moved > 1f)
            {
                // Skok (nová postava, první snímek), ne chůze.
                return;
            }

            _walked += moved;
            if (_walked < StepMeters)
            {
                return;
            }

            _walked = 0f;
            double x, y;
            WorldCoords.FromUnity(self, out x, out y);
            var recorded = StepClip(SoundCatalog.Current, _connection.World, x, y, _session != null ? _session.SelfLevel : 0, _connection.FloodedTiles);
            if (recorded != null)
            {
                PlayVaried(recorded, self + Vector3.down * 0.8f, SoundCatalog.Current.StepsVolume, 0.1f);
            }
            else
            {
                Play(Step(), self + Vector3.down * 0.8f, 0.25f);
            }
        }

        /// <summary>
        /// Nahrávka kroku podle země: tráva, písek, skála a jeskyně, bažina a brod (i zatopená zem), podlaha patra.
        /// Když pro tu zem nahrávka není, tráva. Když není ani tráva, null (ťuknutí z kódu).
        /// </summary>
        public static AudioClip StepClip(SoundCatalog sounds, WorldView world, double x, double y, int level, int[] flooded)
        {
            AudioClip[] clips;
            var (tx, ty) = ((int)System.Math.Floor(x), (int)System.Math.Floor(y));
            var tile = tx >= 0 && ty >= 0 && tx < world.Width && ty < world.Height ? world.TileAt(tx, ty) : TileType.Grass;
            if (level > 0)
            {
                clips = sounds.StepsWood;
            }
            else if (tile == TileType.Bog || tile == TileType.Ford || System.Array.IndexOf(flooded, ty * world.Width + tx) >= 0)
            {
                clips = sounds.StepsMud;
            }
            else if (tile == TileType.Sand)
            {
                clips = sounds.StepsSand;
            }
            else if (tile == TileType.Rock || tile == TileType.LayeredRock || tile == TileType.CaveFloor)
            {
                clips = sounds.StepsStone;
            }
            else
            {
                clips = sounds.StepsGrass;
            }

            return SoundCatalog.Pick(clips) ?? SoundCatalog.Pick(sounds.StepsGrass);
        }

        /// <summary>Hlad: občas zakručí v břiše (jen nahrávka, v kódu se kručení neskládá).</summary>
        private void UpdateStomach(Vector3 self)
        {
            var state = _connection.Self;
            if (state == null || Time.time < _nextGrowl)
            {
                return;
            }

            _nextGrowl = Time.time + Random.Range(GrowlMinSeconds, GrowlMaxSeconds);
            if (state.Satiety < HungrySatiety && !state.Dead && !state.Sleeping)
            {
                Growl(self);
            }
        }

        private static void Growl(Vector3 self)
        {
            var growl = SoundCatalog.Pick(SoundCatalog.Current.Stomach);
            if (growl != null)
            {
                PlayVaried(growl, self + Vector3.up, SoundCatalog.Current.StomachVolume, 0.08f);
            }
        }

        /// <summary>Divočák, který se zaměřil, zachrochtá. Vlk, který se plíží, zavyje.</summary>
        private void UpdateAnimals(Vector3 self)
        {
            foreach (var animal in _connection.Animals)
            {
                AnimalState before;
                var known = _animalStates.TryGetValue(animal.Id, out before);
                _animalStates[animal.Id] = animal.State;
                if (!known || before == animal.State)
                {
                    continue;
                }

                var at = WorldCoords.ToUnity(animal.X, animal.Y, 0.5f);
                if (Vector3.Distance(at, self) > AnimalHearDistance)
                {
                    continue;
                }

                if (animal.Kind == AnimalKind.Boar && animal.State == AnimalState.Alert)
                {
                    Play(Grunt(), at, 0.9f);
                }
                else if (animal.Kind == AnimalKind.Wolf && animal.State == AnimalState.Stalk)
                {
                    Play(Howl(), at, 0.7f);
                }
            }
        }

        /// <summary>
        /// Výroba se povedla: když byla z kamene nebo pazourku (nůž, sekera, kopí), je slyšet opracování kamene.
        /// Nově naučený recept přijde ze serveru až po hlášce, proto se chvíli čeká.
        /// </summary>
        private void UpdatePendingCraft()
        {
            if (_pendingRecipe < 0)
            {
                return;
            }

            foreach (var recipe in _connection.Recipes)
            {
                if (recipe.Index != _pendingRecipe)
                {
                    continue;
                }

                _pendingRecipe = -1;
                if (FromStone(recipe))
                {
                    PlayRecorded(SoundCatalog.Current.StoneWork, _players.Self.position, SoundCatalog.Current.StoneWorkVolume);
                }

                return;
            }

            if (Time.time > _pendingUntil)
            {
                _pendingRecipe = -1;
            }
        }

        /// <summary>Recept je z kamene nebo pazourku (opracování kamene).</summary>
        public static bool FromStone(RecipeView recipe)
        {
            foreach (var input in recipe.Inputs)
            {
                if (input.Kind == Oohra.Core.Items.ItemKind.Stone || input.Kind == Oohra.Core.Items.ItemKind.Flint) return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.EventReceived -= OnEvent;
            }
        }

        /// <summary>Zvuky vlastních činností a toho, co se stalo tělu.</summary>
        private void OnEvent(GameEvent e)
        {
            if (_players == null || _players.Self == null)
            {
                return;
            }

            var self = _players.Self.position;
            var sounds = SoundCatalog.Current;
            if (e.Kind == EventKind.Body)
            {
                OnBodyEvent((BodyEvent)e.Code, self);
                return;
            }

            if (e.Kind == EventKind.Material && (MaterialResult)e.Code is MaterialResult.GotStone or MaterialResult.GotFlint)
            {
                PlayRecorded(sounds.TakeStone, self, sounds.TakeStoneVolume);
                return;
            }

            if (e.Kind == EventKind.Build && (BuildResult)e.Code is BuildResult.Built or BuildResult.Removed or BuildResult.Repaired)
            {
                PlayRecorded(sounds.Build, self, sounds.BuildVolume);
                return;
            }

            if (e.Kind == EventKind.Craft && (CraftResult)e.Code is CraftResult.Made or CraftResult.Learned)
            {
                _pendingRecipe = e.Subject;
                _pendingUntil = Time.time + 1.5f;
                return;
            }

            if (e.Kind != EventKind.Wood)
            {
                return;
            }

            var result = (WoodResult)e.Code;
            if (result is WoodResult.Chopped or WoodResult.TreeFalling or WoodResult.PieceCut or WoodResult.PieceSplit or WoodResult.WoodSplit or WoodResult.AxeBroke)
            {
                var chop = SoundCatalog.Pick(sounds.Chop);
                if (chop != null) PlayVaried(chop, self, sounds.ChopVolume, 0.08f);
                else Play(Chop(), self, 0.8f);
            }
        }

        private void OnBodyEvent(BodyEvent bodyEvent, Vector3 self)
        {
            switch (bodyEvent)
            {
                case BodyEvent.MosquitoBite:
                    Play(Buzz(), self + Vector3.up, 0.5f);
                    break;
                case BodyEvent.WaspSting:
                    // Vosy bzučí hlasitěji než komár.
                    Play(Buzz(), self + Vector3.up, 0.9f);
                    break;
                case BodyEvent.RoofCracking:
                    Play(Creak(), self + Vector3.up * 2f, 0.9f);
                    break;
                case BodyEvent.Fell:
                case BodyEvent.HitByRoof:
                    Play(Crash(), self, 1f);
                    break;
                case BodyEvent.SnakeWarning:
                    Play(Hiss(), self, 0.7f);
                    break;
                case BodyEvent.BellyAche:
                    Growl(self);
                    break;
            }
        }

        /// <summary>Dopad padajícího stromu: nahrávka z katalogu, jinak rána složená v kódu.</summary>
        public static void PlayTreeFall(Vector3 at)
        {
            var sounds = SoundCatalog.Current;
            var fall = SoundCatalog.Pick(sounds.TreeFall);
            if (fall != null) PlayVaried(fall, at, sounds.TreeFallVolume, 0.05f);
            else Play(Crash(), at, 1f);
        }

        /// <summary>Zvuk v místě (padající strom, dopad), hlasitost podle SyllableVoice.Volume.</summary>
        public static void PlayAt(AudioClip clip, Vector3 at, float volume)
        {
            Play(clip, at, volume);
        }

        private static void Play(AudioClip clip, Vector3 at, float volume)
        {
            if (Application.isPlaying && SyllableVoice.Volume > 0f)
            {
                AudioSource.PlayClipAtPoint(clip, at, SyllableVoice.Volume * volume);
            }
        }

        /// <summary>Nahrávka z katalogu (náhodná z políčka). Když políčko je prázdné, nic nehraje.</summary>
        private static void PlayRecorded(AudioClip[] clips, Vector3 at, float volume)
        {
            var clip = SoundCatalog.Pick(clips);
            if (clip != null)
            {
                PlayVaried(clip, at, volume, 0.06f);
            }
        }

        /// <summary>
        /// Nahrávka v místě, pokaždé trochu jinak vysoko (o ±pitch), ať se stejný zvuk neopakuje jako stroj.
        /// </summary>
        public static void PlayVaried(AudioClip clip, Vector3 at, float volume, float pitch)
        {
            if (!Application.isPlaying || SyllableVoice.Volume <= 0f || clip == null)
            {
                return;
            }

            var go = new GameObject("Zvuk " + clip.name);
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.pitch = 1f + Random.Range(-pitch, pitch);
            source.volume = SyllableVoice.Volume * volume;
            source.Play();
            Destroy(go, clip.length / source.pitch + 0.2f);
        }

        // ---------- zvuky složené v kódu ----------

        /// <summary>Praskání ohně: tiché šumění a náhodné prasknutí (smyčka 2 s).</summary>
        public static AudioClip Crackle()
        {
            if (_crackle != null) return _crackle;
            var samples = new float[SampleRate * 2];
            var seed = 777;
            var pop = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var noise = Noise(ref seed);
                if (noise > 0.9995f) pop = 1f;
                pop *= 0.995f;
                samples[n] = noise * 0.04f + Noise(ref seed) * pop * 0.5f;
            }

            return _crackle = Clip("Oheň", samples);
        }

        /// <summary>Krok: krátké tupé ťuknutí.</summary>
        public static AudioClip Step()
        {
            if (_step != null) return _step;
            var samples = new float[(int)(0.08f * SampleRate)];
            var seed = 31;
            var low = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                low = low * 0.9f + Noise(ref seed) * 0.1f;
                samples[n] = low * 2.5f * (1f - (float)n / samples.Length);
            }

            return _step = Clip("Krok", samples);
        }

        /// <summary>Divočák: hluboké chrochtání, padající tón se šumem.</summary>
        public static AudioClip Grunt()
        {
            if (_grunt != null) return _grunt;
            var samples = new float[(int)(0.45f * SampleRate)];
            var seed = 5;
            var phase = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                phase += 2 * Mathf.PI * Mathf.Lerp(110f, 70f, u) / SampleRate;
                var rough = 0.6f + 0.4f * Mathf.Sin(n * 2 * Mathf.PI * 32f / SampleRate);
                samples[n] = (Mathf.Sin(phase) + 0.5f * Mathf.Sin(2 * phase) + Noise(ref seed) * 0.3f) * rough * 0.35f * Mathf.Min(1f, u * 15f) * (1f - u);
            }

            return _grunt = Clip("Divočák", samples);
        }

        /// <summary>Vlk: dlouhé vytí, tón stoupne a zase klesne.</summary>
        public static AudioClip Howl()
        {
            if (_howl != null) return _howl;
            var samples = new float[(int)(1.8f * SampleRate)];
            var phase = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                var pitch = 380f + 260f * Mathf.Sin(u * Mathf.PI) + 6f * Mathf.Sin(n * 2 * Mathf.PI * 5f / SampleRate);
                phase += 2 * Mathf.PI * pitch / SampleRate;
                samples[n] = (Mathf.Sin(phase) + 0.2f * Mathf.Sin(2 * phase)) * 0.25f * Mathf.Min(1f, u * 6f) * Mathf.Min(1f, (1f - u) * 4f);
            }

            return _howl = Clip("Vlk", samples);
        }

        /// <summary>Komár u ucha: tenké bzučení, které se blíží a vzdaluje.</summary>
        public static AudioClip Buzz()
        {
            if (_buzz != null) return _buzz;
            var samples = new float[(int)(1.6f * SampleRate)];
            var phase = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                phase += 2 * Mathf.PI * (560f + 40f * Mathf.Sin(u * 23f)) / SampleRate;
                var near = Mathf.Sin(u * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(u * 9f));
                samples[n] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(2 * phase)) * 0.18f * near;
            }

            return _buzz = Clip("Komár", samples);
        }

        /// <summary>Had syčí: šum, který zesílí a pomalu odezní.</summary>
        public static AudioClip Hiss()
        {
            if (_hiss != null) return _hiss;
            var samples = new float[(int)(1.1f * SampleRate)];
            var seed = 97;
            var last = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                // Vysoký šum: rozdíl dvou po sobě jdoucích náhodných čísel zvýrazní vysoké tóny.
                var noise = Noise(ref seed);
                var high = noise - last;
                last = noise;
                samples[n] = high * 0.22f * Mathf.Min(1f, u * 8f) * Mathf.Pow(1f - u, 1.5f);
            }

            return _hiss = Clip("Had", samples);
        }

        /// <summary>Seknutí sekerou: tupý úder do dřeva s krátkým dozvukem.</summary>
        public static AudioClip Chop()
        {
            if (_chop != null) return _chop;
            var samples = new float[(int)(0.18f * SampleRate)];
            var seed = 71;
            var phase = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                phase += 2 * Mathf.PI * Mathf.Lerp(220f, 140f, u) / SampleRate;
                var knock = Mathf.Sin(phase) * Mathf.Exp(-u * 9f);
                var crack = u < 0.05f ? Noise(ref seed) * (1f - u / 0.05f) : 0f;
                samples[n] = (knock * 0.6f + crack * 0.5f) * 0.8f;
            }

            return _chop = Clip("Seknutí", samples);
        }

        /// <summary>Praskání padajícího stromu: nepravidelné praskání dřeva, čím dál hustší.</summary>
        public static AudioClip Creak()
        {
            if (_creak != null) return _creak;
            var samples = new float[(int)(1.4f * SampleRate)];
            var seed = 303;
            var pop = 0f;
            var phase = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                if (Noise(ref seed) > 0.9985f - u * 0.004f) pop = 1f;
                pop *= 0.992f;
                phase += 2 * Mathf.PI * (90f + 40f * Mathf.Sin(u * 7f)) / SampleRate;
                samples[n] = Noise(ref seed) * pop * 0.5f + Mathf.Sin(phase) * 0.15f * u;
            }

            return _creak = Clip("Praskání stromu", samples);
        }

        /// <summary>Dopad stromu: těžká rána a šumění větví.</summary>
        public static AudioClip Crash()
        {
            if (_crash != null) return _crash;
            var samples = new float[(int)(1.2f * SampleRate)];
            var seed = 911;
            var low = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                low = low * 0.96f + Noise(ref seed) * 0.04f;
                var boom = Mathf.Sin(2 * Mathf.PI * 55f * n / SampleRate) * Mathf.Exp(-u * 6f);
                samples[n] = Mathf.Clamp(boom * 0.7f + low * 4f * Mathf.Exp(-u * 3f) + Noise(ref seed) * 0.15f * Mathf.Exp(-u * 5f), -1f, 1f);
            }

            return _crash = Clip("Dopad stromu", samples);
        }

        /// <summary>Déšť: hustý šum s ťukáním kapek (smyčka 3 s, konec navazuje na začátek).</summary>
        public static AudioClip Rain()
        {
            if (_rain != null) return _rain;
            var samples = new float[SampleRate * 3];
            var seed = 4242;
            var low = 0f;
            var drop = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var noise = Noise(ref seed);
                low = low * 0.75f + noise * 0.25f;
                if (Noise(ref seed) > 0.996f) drop = 0.6f;
                drop *= 0.96f;
                samples[n] = low * 0.35f + noise * 0.05f + Noise(ref seed) * drop * 0.2f;
            }

            // Krátké prolnutí konce a začátku, ať smyčka necvaká.
            var fade = SampleRate / 10;
            for (var n = 0; n < fade; n++)
            {
                var u = (float)n / fade;
                samples[n] = samples[n] * u + samples[samples.Length - fade + n] * (1f - u);
            }

            return _rain = Clip("Déšť", samples);
        }

        /// <summary>Hrom: prásknutí a dlouhé hluboké dunění, které slábne.</summary>
        public static AudioClip Thunder()
        {
            if (_thunder != null) return _thunder;
            var samples = new float[SampleRate * 4];
            var seed = 99;
            var low = 0f;
            var lower = 0f;
            for (var n = 0; n < samples.Length; n++)
            {
                var u = (float)n / samples.Length;
                low = low * 0.97f + Noise(ref seed) * 0.03f;
                lower = lower * 0.995f + low * 0.005f;
                var crack = u < 0.04f ? Noise(ref seed) * (1f - u / 0.04f) * 0.5f : 0f;
                var rumble = 0.7f + 0.3f * Mathf.Sin(u * 37f) * Mathf.Sin(u * 11f);
                samples[n] = Mathf.Clamp(crack + (low * 6f + lower * 40f) * rumble * Mathf.Pow(1f - u, 1.5f), -1f, 1f);
            }

            return _thunder = Clip("Hrom", samples);
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Noise(ref int seed)
        {
            seed = seed * 1103515245 + 12345;
            return ((seed >> 16) & 0x7fff) / 16384f - 1f;
        }
    }
}

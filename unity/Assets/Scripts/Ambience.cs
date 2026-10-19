using System.Collections.Generic;
using Oohra.Core.Characters;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Zvuky okolí (prototyp sfx): praskání nejbližšího ohně, kroky postavy, zachrochtání divočáka, který se zaměřil,
    /// a vytí vlka, který se plíží (déšť a hrom hraje WeatherView). Nahrávky hra nemá, zvuky se skládají v kódu (šum, tóny). Hlasitost podle
    /// SyllableVoice.Volume.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const float FireHearDistance = 12f;
        private const float AnimalHearDistance = 22f;
        // Krok zazní po každém kusu cesty (metry).
        private const float StepMeters = 0.9f;

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
        private Vector3 _lastSelf;
        private float _walked;
        private ClientSession _session;

        public void Init(GameConnection connection, PlayerViews players, ClientSession session = null)
        {
            _connection = connection;
            _players = players;
            _session = session;
            if (session != null)
            {
                session.EventReceived += OnEvent;
            }

            _fire = gameObject.AddComponent<AudioSource>();
            _fire.clip = Crackle();
            _fire.loop = true;
            _fire.spatialBlend = 1f;
            _fire.minDistance = 2f;
            _fire.maxDistance = FireHearDistance;
            _fire.rolloffMode = AudioRolloffMode.Linear;
        }

        private void Update()
        {
            if (_connection == null || _players == null || _players.Self == null || DreamView.Active)
            {
                return;
            }

            var self = _players.Self.position;
            UpdateFire(self);
            UpdateSteps(self);
            UpdateAnimals(self);
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

        /// <summary>Kroky: tiché ťuknutí po každém kusu cesty.</summary>
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
            if (_walked >= StepMeters)
            {
                _walked = 0f;
                Play(Step(), self + Vector3.down * 0.8f, 0.25f);
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

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.EventReceived -= OnEvent;
            }
        }

        /// <summary>Vlastní seknutí sekerou je slyšet (do stromu i do dřeva na zemi).</summary>
        private void OnEvent(GameEvent e)
        {
            if (_players == null || _players.Self == null)
            {
                return;
            }

            if (e.Kind == EventKind.Body && (BodyEvent)e.Code == BodyEvent.MosquitoBite)
            {
                Play(Buzz(), _players.Self.position + Vector3.up, 0.5f);
                return;
            }

            if (e.Kind == EventKind.Body && (BodyEvent)e.Code == BodyEvent.WaspSting)
            {
                // Vosy bzučí hlasitěji než komár.
                Play(Buzz(), _players.Self.position + Vector3.up, 0.9f);
                return;
            }

            if (e.Kind == EventKind.Body && (BodyEvent)e.Code == BodyEvent.RoofCracking)
            {
                Play(Creak(), _players.Self.position + Vector3.up * 2f, 0.9f);
                return;
            }

            if (e.Kind == EventKind.Body && (BodyEvent)e.Code == BodyEvent.HitByRoof)
            {
                Play(Crash(), _players.Self.position, 1f);
                return;
            }

            if (e.Kind == EventKind.Body && (BodyEvent)e.Code == BodyEvent.SnakeWarning)
            {
                Play(Hiss(), _players.Self.position, 0.7f);
                return;
            }

            if (e.Kind != EventKind.Wood)
            {
                return;
            }

            var result = (WoodResult)e.Code;
            if (result is WoodResult.Chopped or WoodResult.TreeFalling or WoodResult.PieceCut or WoodResult.PieceSplit or WoodResult.WoodSplit or WoodResult.AxeBroke)
            {
                Play(Chop(), _players.Self.position, 0.8f);
            }
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

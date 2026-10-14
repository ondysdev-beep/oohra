using System.Linq;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Rozhraní šedé krabice (Unity OnGUI): čas, pruhy těla, příznaky, hlášky, jmenovky hráčů,
    /// nabídka u rostliny, vody, stromu, skály a ohně, batoh a okno smrti. Data posílá server.
    /// Klávesy: B batoh, G režim stavby (F rovnou oheň), Esc zavřít.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        private const float LogSeconds = 9f;
        private const float BaseHeight = 900f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);

        private ClientSession _session;
        private PlayerViews _players;
        private Camera _camera;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _warn;
        private GUIStyle _name;
        private GUIStyle _tribeName;
        private GUIStyle _center;
        private Rect _menuRect;
        private Rect _backpackRect;
        private Rect _deathRect;
        private Rect _backpackButtonRect;
        private Rect _buildButtonRect;
        private Rect _buildPanelRect;
        private Rect _sleepButtonRect;
        private Rect _skillsButtonRect;
        private Rect _skillsRect;
        private BuildMode _buildMode;

        /// <summary>Myš je nad rozhraním: kliknutí nemá posílat postavu na zem pod ním.</summary>
        public static bool MouseOverHud { get; private set; }

        public void Init(ClientSession session, PlayerViews players, Camera camera)
        {
            _session = session;
            _players = players;
            _camera = camera;
            _buildMode = GetComponent<BuildMode>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (_session == null || keyboard == null || _session.Dead)
            {
                return;
            }

            if (keyboard.bKey.wasPressedThisFrame) _session.BackpackOpen = !_session.BackpackOpen;
            if (keyboard.jKey.wasPressedThisFrame) _session.BackpackOpen = false;
            if (keyboard.nKey.wasPressedThisFrame) _session.ToggleSleep();
            if (keyboard.kKey.wasPressedThisFrame) _session.SkillsOpen = !_session.SkillsOpen;
            if (keyboard.spaceKey.wasPressedThisFrame && !BuildMode.IsActive) _session.Attack();
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _session.BackpackOpen = false;
                _session.Menu = null;
                _session.Selected = null;
            }
        }

        private void OnGUI()
        {
            if (_session == null || _session.Connection.Self == null)
            {
                return;
            }

            CreateStyles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            DrawNames(scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            _menuRect = _backpackRect = _deathRect = _buildPanelRect = _skillsRect = Rect.zero;
            if (_session.Sleeping)
            {
                DrawSleep(width, height);
            }

            DrawInfo();
            DrawButtons(width);
            var bottom = DrawBody(width, height);
            bottom = DrawMenu(width, bottom);
            DrawLog(bottom);

            if (_session.Dead)
            {
                DrawDeath(width, height);
            }
            else if (BuildMode.IsActive)
            {
                DrawBuildPanel(width);
            }
            else if (_session.BackpackOpen)
            {
                DrawBackpack(width, height);
            }
            else if (_session.SkillsOpen)
            {
                DrawSkills(width);
            }

            var mouse = Event.current.mousePosition;
            if (Event.current.type == EventType.Repaint)
            {
                MouseOverHud = _menuRect.Contains(mouse) || _backpackRect.Contains(mouse)
                    || _deathRect.Contains(mouse) || _backpackButtonRect.Contains(mouse) || _buildButtonRect.Contains(mouse) || _buildPanelRect.Contains(mouse)
                    || _sleepButtonRect.Contains(mouse) || _skillsButtonRect.Contains(mouse) || _skillsRect.Contains(mouse)
                    || SpeechHud.Covers(mouse) || SignHud.Covers(mouse) || TribeHud.Covers(mouse)
                    || CorpseHud.Covers(mouse) || SignatureHud.Covers(mouse);
            }
        }

        /// <summary>Jména hráčů nad hlavou (kreslí se v obrazovkových bodech, bez zvětšení).</summary>
        private void DrawNames(float scale)
        {
            GUI.matrix = Matrix4x4.identity;
            var connection = _session.Connection;
            foreach (var id in _players.PlayerIds)
            {
                PlayerInfo info;
                var position = _players.WorldPositionOf(id);
                if (!position.HasValue || !connection.Players.TryGetValue(id, out info))
                {
                    continue;
                }

                var screen = _camera.WorldToScreenPoint(position.Value + Vector3.up * (PlayerViews.BodyHeight / 2 + 0.3f));
                if (screen.z < 0)
                {
                    continue;
                }

                var rect = new Rect(screen.x - 80, Screen.height - screen.y - 12, 160, 22);
                // Členové mého kmene mají jméno okrovou barvou.
                var tribe = id != connection.PlayerId && _session.InMyTribe(id);
                GUI.Label(rect, info.Nickname, tribe ? _tribeName : _name);
            }
        }

        private void DrawInfo()
        {
            var clock = _session.Clock;
            var self = _session.Connection.Self;
            var hour = (int)clock.Hour;
            var minute = (int)((clock.Hour - hour) * 60);
            var players = _session.Connection.Players.Count;
            var text = $"Den {clock.Day + 1}, {hour:00}:{minute:00} · {Texts.SeasonName(clock.Season)}, {clock.MonthOfYear + 1}. měsíc\n"
                + (self.Generation == 0 ? "Pradávný" : $"Nový · generace {self.Generation + 1}")
                + $" · hráčů na ostrově: {players}";
            text += $"\nKlacky {_session.Count(ItemKind.Stick)} · kameny {_session.Count(ItemKind.Stone)}" + (self.NearFire ? " · hřeje tě oheň" : string.Empty) + (self.Sheltered ? " · jsi v úkrytu" : string.Empty);
            GUI.Box(new Rect(12, 12, 420, 68), text, _label);
            GUI.Label(new Rect(12, 84, 980, 20),
                "WASD / klik = chůze · mezerník rána · G stavět · B batoh · N spát · K dovednosti · T mluvit · V gesta · J slovník · H kreslit · P podpis · Q, E, kolečko = kamera", _label);
        }

        /// <summary>Vpravo nahoře: batoh a režim stavby.</summary>
        private void DrawButtons(float width)
        {
            var inventory = _session.Connection.Inventory;
            _backpackButtonRect = new Rect(width - 132, 12, 120, 34);
            if (GUI.Button(_backpackButtonRect, $"Batoh (B) {inventory.Stacks.Length}/{inventory.Capacity}"))
            {
                _session.BackpackOpen = !_session.BackpackOpen;
            }

            _buildButtonRect = new Rect(width - 264, 12, 120, 34);
            if (GUI.Button(_buildButtonRect, BuildMode.IsActive ? "Konec stavby" : "Stavět (G)"))
            {
                if (BuildMode.IsActive) _buildMode.Close();
                else _buildMode.Open(_buildMode.Selected);
            }

            _sleepButtonRect = new Rect(width - 396, 12, 120, 34);
            if (GUI.Button(_sleepButtonRect, _session.Sleeping ? "Vstát (N)" : "Spát (N)"))
            {
                _session.ToggleSleep();
            }

            _skillsButtonRect = new Rect(width - 528, 12, 120, 34);
            if (GUI.Button(_skillsButtonRect, "Dovednosti (K)"))
            {
                _session.SkillsOpen = !_session.SkillsOpen;
            }
        }

        /// <summary>Spánek: obrazovka ztmavne, uprostřed nápis. Svět běží dál (ve sdíleném světě se čas nezrychluje).</summary>
        private void DrawSleep(float width, float height)
        {
            var old = GUI.color;
            GUI.color = new Color(0.02f, 0.03f, 0.08f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(0, height * 0.38f, width, 30), "Spíš…", _name);
            GUI.Label(new Rect(0, height * 0.38f + 30, width, 24), "Síly se vracejí. Vstaneš tlačítkem Vstát (N) nebo pohybem.", _center);
        }

        /// <summary>Dovednosti: úroveň 0 až 5 (body) a jestli nácvik čeká na spánek.</summary>
        private void DrawSkills(float width)
        {
            var self = _session.Connection.Self;
            var skills = new[] { SkillKind.Gather, SkillKind.Hunt, SkillKind.Craft, SkillKind.Build };
            _skillsRect = new Rect(width - 364, 56, 352, 70 + skills.Length * 30);
            GUI.Box(_skillsRect, GUIContent.none);
            GUI.Box(_skillsRect, GUIContent.none);
            var x = _skillsRect.x + 12;
            var y = _skillsRect.y + 8;
            GUI.Label(new Rect(x, y, 300, 22), "DOVEDNOSTI", _title);
            y += 30;
            foreach (var skill in skills)
            {
                var index = (int)skill;
                var level = self.SkillLevels != null && index < self.SkillLevels.Length ? self.SkillLevels[index] : 0;
                GUI.Label(new Rect(x, y, 110, 22), Texts.SkillName(skill), _label);
                for (var i = 0; i < 5; i++)
                {
                    DrawBar(new Rect(x + 110 + i * 22, y + 5, 16, 12), i < level ? 1f : 0f, Ochre);
                }

                if ((self.PendingSkills & (1 << index)) != 0)
                {
                    GUI.Label(new Rect(x + 230, y, 110, 22), "čeká na spánek", _label);
                }

                y += 30;
            }

            GUI.Label(new Rect(x, y, 330, 22), "Nácvik se utvrdí ve spánku (aspoň hodinu).", _label);
        }

        /// <summary>Režim stavby: výběr stavby a nápověda k ovládání.</summary>
        private void DrawBuildPanel(float width)
        {
            var options = _buildMode.Options;
            _buildPanelRect = new Rect(width - 364, 56, 352, 122 + options.Length * 32);
            GUI.Box(_buildPanelRect, GUIContent.none);
            GUI.Box(_buildPanelRect, GUIContent.none);
            var x = _buildPanelRect.x + 10;
            var y = _buildPanelRect.y + 6;
            GUI.Label(new Rect(x, y, 300, 22), "REŽIM STAVBY", _title);
            y += 28;
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var selected = option == _buildMode.SelectedOption;
                var key = i < 9 ? (i + 1).ToString() : " ";
                var text = $"{key}  {option.Name} ({option.Cost})" + (selected ? "  ◄" : string.Empty);
                if (GUI.Button(new Rect(x, y, 332, 28), text))
                {
                    _buildMode.Open(option);
                }

                y += 32;
            }

            var mode = _buildMode.Modular ? "MODULÁRNĚ (napojí a dorovná, Q, E po 15°)" : "VOLNĚ (Q, E plynule otáčí)";
            if (GUI.Button(new Rect(x, y + 2, 332, 28), "R  " + mode))
            {
                _buildMode.Modular = !_buildMode.Modular;
            }

            GUI.Label(new Rect(x, y + 36, 332, 44), "levé tlačítko postavit (do 3,5 m)\npravé tlačítko nebo Esc konec", _label);
        }

        /// <summary>Pět pruhů a příznaky dole uprostřed. Vrací horní okraj (pro další prvky nad nimi).</summary>
        private float DrawBody(float width, float height)
        {
            var self = _session.Connection.Self;
            var panelWidth = Mathf.Min(760, width - 24);
            var x = (width - panelWidth) / 2;
            var y = height - 74;
            GUI.Box(new Rect(x, y, panelWidth, 62), GUIContent.none);

            var bars = new[]
            {
                ("SYTOST", self.Satiety, new Color(0.79f, 0.54f, 0.23f)),
                ("VODA", self.Hydration, new Color(0.29f, 0.56f, 0.66f)),
                ("SÍLA", self.Energy, new Color(0.60f, 0.66f, 0.29f)),
                ("TEPLO", self.Warmth, new Color(0.83f, 0.42f, 0.23f)),
                ("ZDRAVÍ", self.Health, new Color(0.69f, 0.29f, 0.29f)),
            };
            var barWidth = (panelWidth - 20 - 4 * 10) / 5;
            for (var i = 0; i < bars.Length; i++)
            {
                var bx = x + 10 + i * (barWidth + 10);
                GUI.Label(new Rect(bx, y + 4, barWidth, 18), bars[i].Item1, _label);
                DrawBar(new Rect(bx, y + 22, barWidth, 7), bars[i].Item2 / 100f, bars[i].Item3);
            }

            var symptomText = self.Symptoms.Length == 0 ? string.Empty : "PŘÍZNAKY  " + string.Join(" · ", self.Symptoms.Select(Texts.For));
            GUI.Label(new Rect(x + 10, y + 36, panelWidth - 20, 22), symptomText, _warn);
            return y - 6;
        }

        /// <summary>Nabídka u rostliny nebo vody, nad pruhy.</summary>
        private float DrawMenu(float width, float bottom)
        {
            var target = _session.Menu;
            if (target == null || _session.Dead)
            {
                return bottom;
            }

            if (target.Kind == TargetKind.Corpse)
            {
                // Tělo ukazuje CorpseHud (věci a podpis) uprostřed obrazovky.
                return bottom;
            }

            if (target.Kind == TargetKind.Sign)
            {
                // Značku ukazuje SignHud (kresba a záznam), tady se pro ni jen nechá místo.
                return bottom - SignHud.ReadPanelHeight;
            }

            var y = bottom - 64;
            _menuRect = new Rect((width - 520) / 2, y, 520, 60);
            GUI.Box(_menuRect, GUIContent.none);
            var buttonRect = new Rect(_menuRect.x + 10, y + 26, 120, 28);

            var titleRect = new Rect(_menuRect.x + 10, y + 4, 400, 20);
            switch (target.Kind)
            {
                case TargetKind.Plant:
                    GUI.Label(titleRect, "ROSTLINA", _title);
                    if (Button(ref buttonRect, "Utrhnout list")) _session.Harvest(PlantPart.Leaf);
                    if (HasFruit(target.PlantId) && Button(ref buttonRect, "Utrhnout plod")) _session.Harvest(PlantPart.Fruit);
                    if (_session.Menu != null && Button(ref buttonRect, "Vykopat kořen")) _session.Harvest(PlantPart.Root);
                    break;
                case TargetKind.Water:
                    var tile = _session.Connection.World.TileAt(target.TileX, target.TileY);
                    GUI.Label(titleRect, Texts.WaterName(tile).ToUpper(), _title);
                    if (Button(ref buttonRect, "Napít se")) _session.Drink();
                    if (tile != TileType.Bog && _session.Count(ItemKind.Spear) > 0 && Button(ref buttonRect, "Rybařit")) _session.Fish();
                    break;
                case TargetKind.Tree:
                    GUI.Label(titleRect, "STROM", _title);
                    if (Button(ref buttonRect, "Nasbírat klacky")) _session.GatherMaterial();
                    break;
                case TargetKind.Rock:
                    var pile = _session.RockPileAt(target.TileX, target.TileY);
                    if (pile == null)
                    {
                        // Hromada je vybraná (zmizela): nabídka se zavře.
                        _session.Menu = null;
                        _session.Selected = null;
                        break;
                    }

                    GUI.Label(titleRect, "SKÁLA (kamenů: " + pile.Value.Stones + ")", _title);
                    if (Button(ref buttonRect, "Vzít kámen")) _session.GatherMaterial();
                    break;
                case TargetKind.Fire:
                    DrawFireMenu(titleRect, ref buttonRect);
                    break;
                case TargetKind.Structure:
                    DrawStructureMenu(titleRect, ref buttonRect);
                    break;
                case TargetKind.Animal:
                    DrawAnimalMenu(target, titleRect, ref buttonRect);
                    break;
                case TargetKind.Player:
                    DrawPlayerMenu(target, titleRect, ref buttonRect);
                    break;
            }

            return y - 6;
        }

        /// <summary>
        /// U stavby: rozebrat. U pasti vzít úlovek (když v ní něco je) a zvednout ji. Koš se zásobami ukazuje TribeHud.
        /// Na cizí stavbu (ne tvou ani tvého kmene) sahat nesmíš.
        /// </summary>
        private void DrawStructureMenu(Rect titleRect, ref Rect buttonRect)
        {
            var structure = _session.MenuStructure();
            if (structure == null)
            {
                _session.Menu = null;
                _session.Selected = null;
                return;
            }

            var trap = structure.Value.Kind == BuildKind.Trap;
            var caught = trap && structure.Value.Caught >= 0;
            var title = _session.StructureName(structure.Value).ToUpper();
            GUI.Label(titleRect, caught ? title + " (" + AnimalViews.NameOf((AnimalKind)structure.Value.Caught).ToLower() + ")" : title, _title);
            if (!_session.MayTouch(structure.Value))
            {
                GUI.Label(new Rect(buttonRect.x, buttonRect.y + 4, 480, 22), "Cizí stavba. Sahat na ni smí jen ten, kdo ji postavil, a jeho kmen.", _label);
                return;
            }

            if (caught && Button(ref buttonRect, "Vzít úlovek")) _session.TakeCatch();
            if (Button(ref buttonRect, trap ? "Zvednout past" : "Rozebrat")) _session.RemoveStructure();
        }

        /// <summary>U jiného hráče: pozvat do kmene (když už nejste spolu).</summary>
        private void DrawPlayerMenu(InteractionTarget target, Rect titleRect, ref Rect buttonRect)
        {
            PlayerInfo info;
            if (!_session.Connection.Players.TryGetValue(target.Id, out info))
            {
                _session.Menu = null;
                _session.Selected = null;
                return;
            }

            GUI.Label(titleRect, info.Nickname.ToUpper(), _title);
            if (_session.InMyTribe(target.Id))
            {
                GUI.Label(new Rect(buttonRect.x, buttonRect.y + 4, 300, 22), "Je s tebou v kmeni.", _label);
                return;
            }

            buttonRect.width = 180;
            if (Button(ref buttonRect, "Pozvat do kmene")) _session.InviteToTribe();
        }

        /// <summary>U zvířete: živé udeřit (pěstí nebo kopím, i mezerníkem), z mrtvého stáhnout maso.</summary>
        private void DrawAnimalMenu(InteractionTarget target, Rect titleRect, ref Rect buttonRect)
        {
            var animal = _session.AnimalOf(target.Id);
            if (animal == null)
            {
                _session.Menu = null;
                _session.Selected = null;
                return;
            }

            var dead = animal.Value.State == AnimalState.Dead;
            var name = AnimalViews.NameOf(animal.Value.Kind).ToUpper();
            GUI.Label(titleRect, dead ? name + " (MRTVÉ)" : name, _title);
            if (dead)
            {
                if (Button(ref buttonRect, "Stáhnout maso")) _session.Butcher();
                return;
            }

            var spear = _session.Count(ItemKind.Spear) > 0;
            buttonRect.width = 200;
            if (Button(ref buttonRect, spear ? "Bodnout kopím (mezerník)" : "Udeřit (mezerník)")) _session.Attack();
        }

        /// <summary>U ohně: přiložit (vyhaslý znovu zapálit) a batoh na pečení.</summary>
        private void DrawFireMenu(Rect titleRect, ref Rect buttonRect)
        {
            var fire = _session.MenuFire();
            if (fire == null)
            {
                _session.Menu = null;
                _session.Selected = null;
                return;
            }

            GUI.Label(titleRect, fire.Value.Burning ? "OHEŇ" : "POPEL", _title);
            if (Button(ref buttonRect, fire.Value.Burning ? "Přiložit klacek" : "Zapálit znovu")) _session.FeedFire();
            if (fire.Value.Burning && Button(ref buttonRect, "Péct (batoh)")) _session.BackpackOpen = true;
        }

        private bool HasFruit(int plantId)
        {
            var visual = _session.Connection.PlantVisuals[plantId];
            return visual == PlantVisual.UnripeFruit || visual == PlantVisual.RipeFruit || visual == PlantVisual.OverripeFruit;
        }

        /// <summary>Posledních pár hlášek nad nabídkou, staré mizí.</summary>
        private void DrawLog(float bottom)
        {
            var lines = _session.Log.Where(l => Time.unscaledTime - l.ShownAt < LogSeconds).ToList();
            var y = bottom - lines.Count * 26;
            foreach (var line in lines)
            {
                var rect = new Rect(16, y, 560, 24);
                GUI.Box(rect, GUIContent.none);
                GUI.Label(new Rect(rect.x + 8, rect.y + 3, rect.width - 12, 20), line.Text, line.Warn ? _warn : _label);
                y += 26;
            }
        }

        private void DrawBackpack(float width, float height)
        {
            var connection = _session.Connection;
            var inventory = connection.Inventory;
            var rowHeight = 34f;
            var panelHeight = Mathf.Min(height - 40, 90 + Mathf.Max(1, inventory.Stacks.Length) * rowHeight);
            _backpackRect = new Rect((width - 660) / 2, (height - panelHeight) / 2, 660, panelHeight);
            GUI.Box(_backpackRect, GUIContent.none);
            GUI.Box(_backpackRect, GUIContent.none);

            var x = _backpackRect.x + 14;
            var y = _backpackRect.y + 10;
            GUI.Label(new Rect(x, y, 300, 24), "BATOH", _title);
            GUI.Label(new Rect(x + 90, y + 2, 300, 22), $"{inventory.Stacks.Length} z {inventory.Capacity} míst", _label);
            if (GUI.Button(new Rect(_backpackRect.xMax - 94, y, 80, 26), "Zavřít"))
            {
                _session.BackpackOpen = false;
            }

            var canMakeSpear = _session.Count(ItemKind.Spear) == 0 && _session.Count(ItemKind.Stick) > 0 && _session.Count(ItemKind.Stone) > 0;
            if (canMakeSpear && GUI.Button(new Rect(_backpackRect.xMax - 284, y, 180, 26), "Vyrobit kopí (klacek, kámen)"))
            {
                _session.MakeSpear();
            }

            y += 36;
            if (inventory.Stacks.Length == 0)
            {
                GUI.Label(new Rect(x, y, 560, 22), "Batoh je prázdný. Klikni na rostlinu a utrhni list, plod nebo vykopej kořen.", _label);
                return;
            }

            for (var i = 0; i < inventory.Stacks.Length; i++)
            {
                var stack = inventory.Stacks[i];
                var plant = stack.Kind == ItemKind.Plant;
                var food = plant || stack.Kind == ItemKind.Meat || stack.Kind == ItemKind.Fish;
                DrawBar(new Rect(x, y + 6, 18, 18), 1f, plant ? Palette.Leaf(connection.World.Species[stack.SpeciesId]) : Palette.Material(stack.Kind));
                GUI.Label(new Rect(x + 26, y + 6, 240, 22), $"{Texts.Describe(stack)}  ×{stack.Count}", _label);

                var buttons = new Rect(x + 270, y + 2, 84, 26);
                var canBake = food && stack.Cooking == Cooking.Raw && connection.Self.NearFire;
                if (food && Button(ref buttons, "Ochutnat")) connection.Taste(i);
                if (food && Button(ref buttons, "Sníst")) connection.Eat(i);
                if (canBake && Button(ref buttons, "Upéct")) _session.Bake(i);
                buttons.x = x + 270 + 3 * (buttons.width + 8);
                if (Button(ref buttons, "Zahodit")) connection.Drop(i);
                y += rowHeight;
            }
        }

        private void DrawDeath(float width, float height)
        {
            var self = _session.Connection.Self;
            _deathRect = new Rect((width - 520) / 2, (height - 210) / 2, 520, 210);
            GUI.Box(_deathRect, GUIContent.none);
            GUI.Box(_deathRect, GUIContent.none);

            var x = _deathRect.x + 16;
            var y = _deathRect.y + 14;
            // Narodí se jako aktuální generace světa (návrh 8.2), ne o jednu výš.
            var next = System.Math.Max(1, self.WorldGeneration);
            GUI.Label(new Rect(x, y, 480, 26), "ZEMŘEL JSI", _title);
            GUI.Label(new Rect(x, y + 30, 488, 22), self.DeathCause.HasValue ? Texts.For(self.DeathCause.Value) : "Zemřel jsi.", _warn);
            GUI.Label(new Rect(x, y + 58, 488, 66),
                "Postava zapomněla, co věděla. Věci zůstaly u tvého těla: můžeš si pro ně dojít,\n"
                + "prvních 24 hodin je smí vzít jen tvůj kmen, potom kdokoli.\n"
                + $"Narodíš se jako Nový, generace {next + 1}." + (self.Generation == 0 ? " Status Pradávného je navždy pryč." : string.Empty),
                _label);
            if (!_session.HasTribe)
            {
                if (GUI.Button(new Rect(x, y + 136, 200, 34), "Narodit se znovu"))
                {
                    _session.Reborn();
                }

                return;
            }

            // V kmeni si hráč vybere (návrh 4.1): u kmene je to bezpečnější, divočina je riziko a objevování.
            if (GUI.Button(new Rect(x, y + 136, 200, 34), "Narodit se u kmene"))
            {
                _session.Reborn(BirthPlace.Tribe);
            }

            if (GUI.Button(new Rect(x + 212, y + 136, 200, 34), "Narodit se v divočině"))
            {
                _session.Reborn(BirthPlace.Wild);
            }
        }

        private static bool Button(ref Rect rect, string text)
        {
            var clicked = GUI.Button(rect, text);
            rect.x += rect.width + 8;
            return clicked;
        }

        private static void DrawBar(Rect rect, float value, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0.23f, 0.18f, 0.15f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void CreateStyles()
        {
            if (_label != null)
            {
                return;
            }

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _label.normal.textColor = new Color(0.94f, 0.9f, 0.82f);
            _title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Ochre;
            _warn = new GUIStyle(_label);
            _warn.normal.textColor = new Color(0.95f, 0.7f, 0.62f);
            _name = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            _tribeName = new GUIStyle(_name);
            _tribeName.normal.textColor = Ochre;
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
        }
    }
}

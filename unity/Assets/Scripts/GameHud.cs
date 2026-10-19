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
        private Rect _helpRect;
        private Rect _backpackButtonRect;
        private Rect _buildButtonRect;
        private Rect _buildPanelRect;
        private Rect _sleepButtonRect;
        private Rect _skillsButtonRect;
        private Rect _skillsRect;
        private BuildMode _buildMode;

        /// <summary>Myš je nad rozhraním: kliknutí nemá posílat postavu na zem pod ním.</summary>
        public static bool MouseOverHud { get; private set; }

        /// <summary>Přehled kláves (F1).</summary>
        public bool HelpOpen { get; set; }

        public void Init(ClientSession session, PlayerViews players, Camera camera)
        {
            _session = session;
            _players = players;
            _camera = camera;
            _buildMode = GetComponent<BuildMode>();
        }

        private void Update()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (_session == null || keyboard == null || _session.Dead)
            {
                return;
            }

            if (keyboard.bKey.wasPressedThisFrame) _session.BackpackOpen = !_session.BackpackOpen;
            if (keyboard.jKey.wasPressedThisFrame) _session.BackpackOpen = false;
            if (keyboard.nKey.wasPressedThisFrame) _session.ToggleSleep();
            if (keyboard.kKey.wasPressedThisFrame) _session.SkillsOpen = !_session.SkillsOpen;
            if (keyboard.f1Key.wasPressedThisFrame) HelpOpen = !HelpOpen;
            if (keyboard.f1Key.wasPressedThisFrame) HelpOpen = !HelpOpen;
            if (keyboard.spaceKey.wasPressedThisFrame && !BuildMode.IsActive) _session.Attack();
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _session.BackpackOpen = false;
                HelpOpen = false;
                _session.Menu = null;
                _session.Selected = null;
            }

            if (!_session.BackpackOpen)
            {
                // Zavřený batoh ukončí dávání cizinci.
                _session.GiveTo = -1;
            }
        }

        private void OnGUI()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

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

            _menuRect = _backpackRect = _deathRect = _buildPanelRect = _skillsRect = _helpRect = Rect.zero;
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
            else if (HelpOpen)
            {
                DrawHelp(width, height);
            }

            var mouse = Event.current.mousePosition;
            if (Event.current.type == EventType.Repaint)
            {
                MouseOverHud = _menuRect.Contains(mouse) || _backpackRect.Contains(mouse)
                    || _deathRect.Contains(mouse) || _backpackButtonRect.Contains(mouse) || _buildButtonRect.Contains(mouse) || _buildPanelRect.Contains(mouse)
                    || _sleepButtonRect.Contains(mouse) || _skillsButtonRect.Contains(mouse) || _skillsRect.Contains(mouse) || _helpRect.Contains(mouse)
                    || SpeechHud.Covers(mouse) || SignHud.Covers(mouse) || TribeHud.Covers(mouse)
                    || CorpseHud.Covers(mouse) || SignatureHud.Covers(mouse) || ReportHud.Covers(mouse) || JournalHud.Covers(mouse);
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
                + Texts.GenerationName(self.Generation)
                + $" · hráčů na ostrově: {players}";
            text += $"\nKlacky {_session.Count(ItemKind.Stick)} · kameny {_session.Count(ItemKind.Stone)}" + (self.NearFire ? " · hřeje tě oheň" : string.Empty) + (self.Sheltered ? " · jsi v úkrytu" : string.Empty);
            GUI.Box(new Rect(12, 12, 420, 68), text, _label);
            GUI.Label(new Rect(12, 84, 980, 20),
                "WASD / klik = chůze · F1 = všechny klávesy", _label);
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
            var leeches = self.Symptoms.Contains(Symptom.Leeches);
            GUI.Label(new Rect(x + 10, y + 36, panelWidth - (leeches ? 300 : 150), 22), symptomText, _warn);
            // Klíšťata se přisají potichu: najde je jen prohlídka.
            if (GUI.Button(new Rect(x + panelWidth - 130, y + 34, 120, 22), "Prohlédnout se")) _session.Connection.InspectBody();
            // Pijavice jdou sundat u ohně, nebo popelem z batohu.
            if (leeches && GUI.Button(new Rect(x + panelWidth - 280, y + 34, 140, 22), "Sundat pijavice")) _session.Connection.RemoveLeeches();
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
                    if (_session.Menu != null && _session.NearestStranger(StrangerViews.AskDistance).HasValue && Button(ref buttonRect, "Ukázat cizinci"))
                    {
                        _session.ShowPlantToStranger();
                    }

                    break;
                case TargetKind.Water:
                    var tile = _session.Connection.World.TileAt(target.TileX, target.TileY);
                    GUI.Label(titleRect, Texts.WaterName(tile).ToUpper(), _title);
                    if (Button(ref buttonRect, "Napít se")) _session.Drink();
                    if (tile != TileType.Bog && (_session.Count(ItemKind.Spear) > 0 || _session.Count(ItemKind.Hook) > 0) && Button(ref buttonRect, "Rybařit")) _session.Fish();
                    if (tile != TileType.Sea && Button(ref buttonRect, "Natřít se bahnem")) _session.Connection.Smear(false);
                    if (_session.Knows("tanHide") && _session.Count(ItemKind.Hide) > 0 && _session.Count(ItemKind.Ash) > 0 && Button(ref buttonRect, "Vyčinit kůži")) _session.Craft("tanHide");
                    break;
                case TargetKind.Tree:
                    if (!_session.TreeStanding(target.TileY * _session.Connection.World.Width + target.TileX))
                    {
                        // Strom padá nebo z něj zbyl pařez: nabídka se zavře.
                        _session.Menu = null;
                        _session.Selected = null;
                        break;
                    }

                    var nest = _session.NestAt(target.TileX, target.TileY);
                    GUI.Label(titleRect, TreeTitle(target) + (nest.HasValue ? " · VOSÍ HNÍZDO" : string.Empty), _title);
                    // Vosy odežene kouř: s hořící pochodní jde hnízdo vykouřit.
                    if (nest.HasValue && _session.TorchLit && Button(ref buttonRect, "Vykouřit vosy")) _session.Connection.SmokeOutNest(nest.Value.TileIndex);
                    if (Button(ref buttonRect, "Nasbírat klacky")) _session.GatherMaterial();
                    if (_session.Count(ItemKind.Axe) > 0 && Button(ref buttonRect, "Kácet (sekera)")) _session.ChopTree();
                    break;
                case TargetKind.Timber:
                    DrawTimberMenu(titleRect, ref buttonRect);
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
                case TargetKind.Stranger:
                    DrawStrangerMenu(target, titleRect, ref buttonRect);
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
            buttonRect.width = 180;
            if (_session.InMyTribe(target.Id))
            {
                GUI.Label(new Rect(buttonRect.x, buttonRect.y + 4, 180, 22), "Je s tebou v kmeni.", _label);
                buttonRect.x += buttonRect.width + 8;
            }
            else if (Button(ref buttonRect, "Pozvat do kmene"))
            {
                _session.InviteToTribe();
            }

            buttonRect.width = 120;
            if (Button(ref buttonRect, "Nahlásit")) _session.AskReport(ReportTarget.Player, target.Id, "hráče " + info.Nickname);

            // Ukázat, jak se dělá recept, který postava umí (ten druhý ho pak umí bez zkoušení).
            var recipes = _session.Connection.Recipes;
            if (recipes.Length == 0)
            {
                return;
            }

            var row = new Rect(titleRect.x, buttonRect.y + 34, 220, 22);
            GUI.Label(row, "Ukázat, jak se dělá:", _label);
            for (var r = 0; r < recipes.Length; r++)
            {
                var rect = new Rect(titleRect.x + r % 3 * 168, row.y + 24 + r / 3 * 28, 162, 24);
                if (GUI.Button(rect, recipes[r].Name)) _session.Connection.TeachRecipe(target.Id, recipes[r].Id);
            }
        }

        /// <summary>
        /// U cizince: dát mu věc z batohu. Mluvit s ním jde slovy (T) a gesty (V), rostlinu mu jde ukázat v její nabídce.
        /// Jméno cizinci nemají, jen barvu kmene.
        /// </summary>
        private void DrawStrangerMenu(InteractionTarget target, Rect titleRect, ref Rect buttonRect)
        {
            var stranger = _session.StrangerOf(target.Id);
            if (!stranger.HasValue)
            {
                _session.Menu = null;
                _session.Selected = null;
                return;
            }

            GUI.Label(titleRect, "CIZINEC", _title);
            DrawBar(new Rect(titleRect.x + 80, titleRect.y + 3, 14, 14), 1f, StrangerViews.TribeColor(stranger.Value.Tribe));
            GUI.Label(new Rect(titleRect.x + 100, titleRect.y, 300, 20), "Mluví jinak než ty. Zkus slova a gesta (T, V).", _label);
            buttonRect.width = 140;
            if (Button(ref buttonRect, "Dát věc")) _session.StartGiving();
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
        /// <summary>Název stromu podle druhu (DUB, BŘÍZA…).</summary>
        private string TreeTitle(InteractionTarget target)
        {
            var index = target.TileY * _session.Connection.World.Width + target.TileX;
            foreach (var tree in _session.Connection.World.Trees)
            {
                if (tree.TileIndex == index) return Texts.TreeName(tree.Kind).ToUpper();
            }

            return "STROM";
        }

        /// <summary>Kmen: odseknout klad. Klad: rozštípat na dřevo, nebo vzít a nést.</summary>
        private void DrawTimberMenu(Rect titleRect, ref Rect buttonRect)
        {
            var timber = _session.MenuTimber();
            if (timber == null)
            {
                _session.Menu = null;
                _session.Selected = null;
                return;
            }

            var axe = _session.Count(ItemKind.Axe) > 0;
            var wood = Texts.TreeName(timber.Value.Tree).ToLower();
            if (timber.Value.Kind == TimberKind.Trunk)
            {
                GUI.Label(titleRect, "KMEN (" + wood + ", " + timber.Value.Length + " m)", _title);
                if (axe && Button(ref buttonRect, "Odseknout klad")) _session.ChopTimber();
                if (_session.Count(ItemKind.Knife) > 0 && Button(ref buttonRect, "Sloupnout lýko")) _session.StripBark();
                if (!axe) GUI.Label(new Rect(buttonRect.x, buttonRect.y + 4, 480, 22), "Kmen se rozseká na klady sekerou (B batoh: vyrobit sekeru).", _label);
                return;
            }

            GUI.Label(titleRect, "KLAD (" + wood + ")", _title);
            if (axe && Button(ref buttonRect, "Rozštípat")) _session.ChopTimber();
            if (Button(ref buttonRect, "Vzít")) _session.PickUpTimber();
            if (_session.Count(ItemKind.Knife) > 0 && Button(ref buttonRect, "Sloupnout lýko")) _session.StripBark();
        }

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
            if (Button(ref buttonRect, "Nabrat popel")) _session.TakeAsh();
            if (fire.Value.Burning && !_session.TorchLit && Button(ref buttonRect, "Zapálit pochodeň")) _session.LightTorch();
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
            var recipes = _session.GiveTo >= 0 ? new RecipeView[0] : connection.Recipes;
            var recipeRows = recipes.Length == 0 ? 0 : 1 + (recipes.Length + 2) / 3;
            var panelHeight = Mathf.Min(height - 40, 130 + Mathf.Max(1, inventory.Stacks.Length) * rowHeight + recipeRows * 32);
            _backpackRect = new Rect((width - 780) / 2, (height - panelHeight) / 2, 780, panelHeight);
            GUI.Box(_backpackRect, GUIContent.none);
            GUI.Box(_backpackRect, GUIContent.none);

            var x = _backpackRect.x + 14;
            var y = _backpackRect.y + 10;
            var title = _session.GiveTo >= 0 ? "CO DÁT CIZINCI?" : inventory.Bag.HasValue ? "BATOH" : "RUCE";
            GUI.Label(new Rect(x, y, 300, 24), title, _title);
            var full = inventory.Stacks.Length > inventory.Capacity ? " · víc neuneseš, něco odlož" : string.Empty;
            GUI.Label(new Rect(x + (_session.GiveTo >= 0 ? 190 : 90), y + 2, 420, 22), $"{inventory.Stacks.Length} z {inventory.Capacity} míst{full}", _label);
            if (GUI.Button(new Rect(_backpackRect.xMax - 94, y, 80, 26), "Zavřít"))
            {
                _session.BackpackOpen = false;
            }

            y += 30;
            // Batoh na zádech (nezabírá místo). Bez něj má nováček jen dvě ruce.
            if (inventory.Bag.HasValue)
            {
                GUI.Label(new Rect(x, y + 2, 420, 22), "Na zádech: " + Texts.ItemName(inventory.Bag.Value), _label);
                if (_session.GiveTo < 0 && GUI.Button(new Rect(x + 430, y, 90, 24), "Sundat")) connection.TakeOffBag();
            }
            else if (_session.GiveTo < 0)
            {
                GUI.Label(new Rect(x, y + 2, 700, 22), "Bez batohu uneseš jen to, co máš v rukou. Batoh si vyrobíš (zkus spojit lýko a klacky).", _label);
            }

            y += 30;
            if (inventory.Stacks.Length == 0)
            {
                GUI.Label(new Rect(x, y, 560, 22), "Nic neneseš. Klikni na rostlinu a utrhni list, plod nebo vykopej kořen.", _label);
                y += rowHeight;
            }

            for (var i = 0; i < inventory.Stacks.Length; i++)
            {
                var stack = inventory.Stacks[i];
                var plant = stack.Kind == ItemKind.Plant;
                var food = plant || stack.Kind == ItemKind.Meat || stack.Kind == ItemKind.Fish;
                if (_session.GiveTo < 0)
                {
                    // Zaškrtnout věci a zkusit je spojit (tak se hledají recepty).
                    var picked = _session.CombineSelection.Contains(i);
                    if (GUI.Toggle(new Rect(x, y + 6, 20, 20), picked, GUIContent.none) != picked) _session.ToggleCombine(i);
                }

                DrawBar(new Rect(x + 24, y + 6, 18, 18), 1f, plant ? Palette.Leaf(connection.World.Species[stack.SpeciesId]) : Palette.Material(stack.Kind));
                GUI.Label(new Rect(x + 50, y + 6, 250, 22), $"{Texts.Describe(stack)}  ×{stack.Count}", _label);

                var buttons = new Rect(x + 300, y + 2, 84, 26);
                if (_session.GiveTo >= 0)
                {
                    // Dávání cizinci: u každé věci jen „Dát“.
                    if (Button(ref buttons, "Dát"))
                    {
                        _session.Give(i);
                        _session.BackpackOpen = false;
                    }

                    y += rowHeight;
                    continue;
                }

                var canBake = food && stack.Cooking == Cooking.Raw && connection.Self.NearFire;
                if (food && Button(ref buttons, "Ochutnat")) connection.Taste(i);
                if (food && Button(ref buttons, "Sníst")) connection.Eat(i);
                if (canBake && Button(ref buttons, "Upéct")) _session.Bake(i);
                if (stack.Kind == ItemKind.Wood && _session.Count(ItemKind.Axe) > 0 && Button(ref buttons, "Naštípat")) connection.SplitWood(i);
                if ((stack.Kind == ItemKind.SmallBag || stack.Kind == ItemKind.LargeBag) && Button(ref buttons, "Nasadit")) connection.WearBag(i);
                if (stack.Kind == ItemKind.Journal && Button(ref buttons, "Otevřít")) _session.OpenJournal(i);
                if (stack.Kind == ItemKind.Ash && Button(ref buttons, "Natřít se")) connection.Smear(true);
                buttons.x = x + 300 + 3 * (buttons.width + 8);
                // Klad se nezahazuje, ale pokládá na zem.
                if (Button(ref buttons, stack.Kind == ItemKind.LogPiece ? "Položit" : "Zahodit")) connection.Drop(i);
                // Ukázat věc ostatním (návrh 9.3): řekni k tomu slovo, ať se ho naučí.
                if (Button(ref buttons, "Ukázat")) connection.ShowItem(i);
                y += rowHeight;
            }

            if (_session.GiveTo >= 0)
            {
                return;
            }

            if (_session.CombineSelection.Count > 0 && GUI.Button(new Rect(x, y, 260, 26), "Zkusit spojit (" + _session.CombineSelection.Count + ")"))
            {
                _session.CombineSelected();
            }

            y += 32;
            if (recipes.Length == 0)
            {
                return;
            }

            // Recepty, které postava umí (ostatní se hledají zkoušením). Smrtí se zapomenou.
            GUI.Label(new Rect(x, y, 300, 24), "UMÍŠ VYROBIT", _title);
            y += 28;
            for (var r = 0; r < recipes.Length; r++)
            {
                var recipe = recipes[r];
                var rect = new Rect(x + r % 3 * 250, y + r / 3 * 32, 244, 26);
                GUI.enabled = _session.HasMaterials(recipe);
                if (GUI.Button(rect, new GUIContent(recipe.Name, Texts.RecipeInputs(recipe)))) _session.Craft(recipe.Id);
                GUI.enabled = true;
            }
        }

        /// <summary>Přehled kláves (F1).</summary>
        public static readonly string[] Keys =
        {
            "WASD nebo klik myší: chůze · klik na věc: jít k ní a otevřít nabídku",
            "Mezerník: rána (zvíře poblíž) · Esc: zavřít nabídku",
            "B batoh (jíst, ochutnat, upéct, ukázat, zahodit, zkusit spojit věci, vyrobit, co umíš, otevřít deník) · N spát a vstát · K dovednosti",
            "G stavět (oheň, zdi, dveře, past, koš, dekorace) · R volné nebo modulární stavění",
            "T mluvit (Shift + 1 až 0 slabiky, Enter říct, Tab nálada) · V gesta · J zápisník slov",
            "H nakreslit značku · P podpis (jméno ze slabik a znak)",
            "Q a E otočit kamerou · kolečko přiblížit · F1 tento přehled",
            "Nahlásit (tlačítko nahoře): problém nebo chyba ve hře",
        };

        /// <summary>Řádek s cestou k souboru s chybami (pro testery).</summary>
        public static string ErrorFileLine => "Chyby hry se zapisují do: " + ErrorLog.FilePath;

        private void DrawHelp(float width, float height)
        {
            _helpRect = new Rect((width - 640) / 2, (height - 110 - Keys.Length * 26) / 2, 640, 110 + Keys.Length * 26);
            GUI.Box(_helpRect, GUIContent.none);
            GUI.Box(_helpRect, GUIContent.none);
            var x = _helpRect.x + 14;
            var y = _helpRect.y + 10;
            GUI.Label(new Rect(x, y, 400, 24), "OVLÁDÁNÍ", _title);
            if (GUI.Button(new Rect(_helpRect.xMax - 94, y, 80, 26), "Zavřít"))
            {
                HelpOpen = false;
            }

            y += 34;
            foreach (var line in Keys)
            {
                GUI.Label(new Rect(x, y, 612, 24), line, _label);
                y += 26;
            }

            GUI.Label(new Rect(x, y, 612, 40), ErrorFileLine, _label);
        }

        private void DrawDeath(float width, float height)
        {
            var self = _session.Connection.Self;
            _deathRect = new Rect((width - 520) / 2, (height - 210) / 2, 520, 210);
            GUI.Box(_deathRect, GUIContent.none);
            GUI.Box(_deathRect, GUIContent.none);

            var x = _deathRect.x + 16;
            var y = _deathRect.y + 14;
            // Narodí se jako aktuální generace světa (návrh 8.2), aspoň Nový (status Pradávného smrtí končí).
            var next = System.Math.Max(1, self.WorldGeneration);
            GUI.Label(new Rect(x, y, 480, 26), "ZEMŘEL JSI", _title);
            GUI.Label(new Rect(x, y + 30, 488, 22), self.DeathCause.HasValue ? Texts.For(self.DeathCause.Value) : "Zemřel jsi.", _warn);
            GUI.Label(new Rect(x, y + 58, 488, 66),
                "Postava zapomněla, co věděla. Věci zůstaly u tvého těla: můžeš si pro ně dojít,\n"
                + "prvních 24 hodin je smí vzít jen tvůj kmen, potom kdokoli.\n"
                + $"Narodíš se jako {Texts.GenerationName(next)}." + (self.Generation == 0 ? " Status Pradávného je navždy pryč." : string.Empty),
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

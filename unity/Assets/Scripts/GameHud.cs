using System.Linq;
using Oohra.Core.Items;
using Oohra.Core.Plants;
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
        private Rect _menuRect;
        private Rect _backpackRect;
        private Rect _deathRect;
        private Rect _backpackButtonRect;
        private Rect _buildButtonRect;
        private Rect _buildPanelRect;
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

            _menuRect = _backpackRect = _deathRect = _buildPanelRect = Rect.zero;
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

            var mouse = Event.current.mousePosition;
            if (Event.current.type == EventType.Repaint)
            {
                MouseOverHud = _menuRect.Contains(mouse) || _backpackRect.Contains(mouse)
                    || _deathRect.Contains(mouse) || _backpackButtonRect.Contains(mouse) || _buildButtonRect.Contains(mouse) || _buildPanelRect.Contains(mouse);
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
                GUI.Label(rect, info.Nickname, _name);
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
            GUI.Label(new Rect(12, 84, 760, 20),
                "WASD / klik = chůze · klik na rostlinu, vodu, strom, skálu, oheň, zeď · G stavět · B batoh · Q, E nebo kolečko = kamera", _label);
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
        }

        /// <summary>Režim stavby: výběr stavby a nápověda k ovládání.</summary>
        private void DrawBuildPanel(float width)
        {
            var options = BuildMode.Catalog;
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
                var selected = option.Kind == _buildMode.Selected;
                var text = $"{i + 1}  {option.Name} ({option.Cost})" + (selected ? "  ◄" : string.Empty);
                if (GUI.Button(new Rect(x, y, 332, 28), text))
                {
                    _buildMode.Open(option.Kind);
                }

                y += 32;
            }

            var mode = _buildMode.Modular ? "MODULÁRNĚ (zdi se napojují, Q, E po 90°)" : "VOLNĚ (Q, E plynule otáčí)";
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
                    break;
                case TargetKind.Tree:
                    GUI.Label(titleRect, "STROM", _title);
                    if (Button(ref buttonRect, "Nasbírat klacky")) _session.GatherMaterial();
                    break;
                case TargetKind.Rock:
                    GUI.Label(titleRect, "SKÁLA", _title);
                    if (Button(ref buttonRect, "Vzít kámen")) _session.GatherMaterial();
                    break;
                case TargetKind.Fire:
                    DrawFireMenu(titleRect, ref buttonRect);
                    break;
                case TargetKind.Structure:
                    GUI.Label(titleRect, StructureViews.NameOf(_session.MenuStructureKind()).ToUpper(), _title);
                    if (Button(ref buttonRect, "Rozebrat")) _session.RemoveStructure();
                    break;
            }

            return y - 6;
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
                DrawBar(new Rect(x, y + 6, 18, 18), 1f, plant ? Palette.Leaf(connection.World.Species[stack.SpeciesId]) : Palette.Material(stack.Kind));
                GUI.Label(new Rect(x + 26, y + 6, 240, 22), $"{Texts.Describe(stack)}  ×{stack.Count}", _label);

                var buttons = new Rect(x + 270, y + 2, 84, 26);
                var canBake = plant && stack.Cooking == Cooking.Raw && connection.Self.NearFire;
                if (plant && Button(ref buttons, "Ochutnat")) connection.Taste(i);
                if (plant && Button(ref buttons, "Sníst")) connection.Eat(i);
                if (canBake && Button(ref buttons, "Upéct")) _session.Bake(i);
                buttons.x = x + 270 + 3 * (buttons.width + 8);
                if (Button(ref buttons, "Zahodit")) connection.Drop(i);
                y += rowHeight;
            }
        }

        private void DrawDeath(float width, float height)
        {
            var self = _session.Connection.Self;
            _deathRect = new Rect((width - 520) / 2, (height - 190) / 2, 520, 190);
            GUI.Box(_deathRect, GUIContent.none);
            GUI.Box(_deathRect, GUIContent.none);

            var x = _deathRect.x + 16;
            var y = _deathRect.y + 14;
            var next = System.Math.Min(6, System.Math.Max(1, self.Generation + 1));
            GUI.Label(new Rect(x, y, 480, 26), "ZEMŘEL JSI", _title);
            GUI.Label(new Rect(x, y + 30, 488, 22), self.DeathCause.HasValue ? Texts.For(self.DeathCause.Value) : "Zemřel jsi.", _warn);
            GUI.Label(new Rect(x, y + 58, 488, 44),
                "Přišel jsi o věci u sebe a o všechno, co tvoje postava věděla.\n"
                + $"Narodíš se jako Nový, generace {next + 1}. Status Pradávného je navždy pryč.",
                _label);
            if (GUI.Button(new Rect(x, y + 120, 200, 34), "Narodit se znovu"))
            {
                _session.Reborn();
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
        }
    }
}

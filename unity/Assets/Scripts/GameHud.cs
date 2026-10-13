using System.Linq;
using Oohra.Core.Plants;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Rozhraní šedé krabice (Unity OnGUI): čas, pruhy těla, příznaky, hlášky,
    /// nabídka u rostliny a vody, batoh a okno smrti. Klávesy: B batoh, Esc zavřít, F1 až F3 rychlost času.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        private const float LogSeconds = 9f;
        private const float BaseHeight = 900f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);

        private GameState _state;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _warn;
        private Rect _menuRect;
        private Rect _backpackRect;
        private Rect _deathRect;
        private Rect _backpackButtonRect;

        /// <summary>Myš je nad rozhraním: kliknutí nemá posílat postavu na zem pod ním.</summary>
        public static bool MouseOverHud { get; private set; }

        public void Init(GameState state)
        {
            _state = state;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (_state == null || keyboard == null || _state.Character.IsDead)
            {
                return;
            }

            if (keyboard.bKey.wasPressedThisFrame) _state.BackpackOpen = !_state.BackpackOpen;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _state.BackpackOpen = false;
                _state.Menu = null;
            }

            if (keyboard.f1Key.wasPressedThisFrame) _state.TimeSpeed = 1f;
            if (keyboard.f2Key.wasPressedThisFrame) _state.TimeSpeed = 10f;
            if (keyboard.f3Key.wasPressedThisFrame) _state.TimeSpeed = 60f;
        }

        private void OnGUI()
        {
            if (_state == null)
            {
                return;
            }

            CreateStyles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            _menuRect = _backpackRect = _deathRect = Rect.zero;
            DrawInfo();
            DrawBackpackButton(width);
            var bottom = DrawBody(width, height);
            bottom = DrawMenu(width, bottom);
            DrawLog(bottom);

            if (_state.Character.IsDead)
            {
                DrawDeath(width, height);
            }
            else if (_state.BackpackOpen)
            {
                DrawBackpack(width, height);
            }

            var mouse = Event.current.mousePosition;
            if (Event.current.type == EventType.Repaint)
            {
                MouseOverHud = _menuRect.Contains(mouse) || _backpackRect.Contains(mouse)
                    || _deathRect.Contains(mouse) || _backpackButtonRect.Contains(mouse);
            }
        }

        private void DrawInfo()
        {
            var clock = _state.Clock;
            var c = _state.Character;
            var hour = (int)clock.Hour;
            var minute = (int)((clock.Hour - hour) * 60);
            var speed = _state.TimeSpeed > 1 ? $" · čas ×{_state.TimeSpeed:0}" : string.Empty;
            var text = $"Den {clock.Day + 1}, {hour:00}:{minute:00} · {Texts.SeasonName(clock.Season)}, {clock.MonthOfYear + 1}. měsíc{speed}\n"
                + (c.Traits.IsAncient ? "Pradávný" : $"Nový · generace {c.Traits.Generation + 1}");
            GUI.Box(new Rect(12, 12, 380, 48), text, _label);
            GUI.Label(new Rect(12, 64, 600, 20),
                "WASD / klik = chůze · klik na rostlinu nebo vodu · B batoh · Q, E kamera · F1–F3 rychlost času", _label);
        }

        private void DrawBackpackButton(float width)
        {
            _backpackButtonRect = new Rect(width - 132, 12, 120, 34);
            if (GUI.Button(_backpackButtonRect, $"Batoh (B) {_state.Inventory.Stacks.Count}/{_state.Inventory.Capacity}"))
            {
                _state.BackpackOpen = !_state.BackpackOpen;
            }
        }

        /// <summary>Pět pruhů a příznaky dole uprostřed. Vrací horní okraj (pro další prvky nad nimi).</summary>
        private float DrawBody(float width, float height)
        {
            var c = _state.Character;
            var panelWidth = Mathf.Min(760, width - 24);
            var x = (width - panelWidth) / 2;
            var y = height - 74;
            GUI.Box(new Rect(x, y, panelWidth, 62), GUIContent.none);

            var bars = new[]
            {
                ("SYTOST", c.Satiety, new Color(0.79f, 0.54f, 0.23f)),
                ("VODA", c.Hydration, new Color(0.29f, 0.56f, 0.66f)),
                ("SÍLA", c.Energy, new Color(0.60f, 0.66f, 0.29f)),
                ("TEPLO", c.Warmth, new Color(0.83f, 0.42f, 0.23f)),
                ("ZDRAVÍ", c.Health, new Color(0.69f, 0.29f, 0.29f)),
            };
            var barWidth = (panelWidth - 20 - 4 * 10) / 5;
            for (var i = 0; i < bars.Length; i++)
            {
                var bx = x + 10 + i * (barWidth + 10);
                GUI.Label(new Rect(bx, y + 4, barWidth, 18), bars[i].Item1, _label);
                DrawBar(new Rect(bx, y + 22, barWidth, 7), (float)bars[i].Item2 / 100f, bars[i].Item3);
            }

            var symptoms = c.Symptoms();
            var symptomText = symptoms.Count == 0 ? string.Empty : "PŘÍZNAKY  " + string.Join(" · ", symptoms.Select(Texts.For));
            GUI.Label(new Rect(x + 10, y + 36, panelWidth - 20, 22), symptomText, _warn);
            return y - 6;
        }

        /// <summary>Nabídka u rostliny nebo vody, nad pruhy.</summary>
        private float DrawMenu(float width, float bottom)
        {
            var target = _state.Menu;
            if (target == null || _state.Paused)
            {
                return bottom;
            }

            var y = bottom - 64;
            _menuRect = new Rect((width - 520) / 2, y, 520, 60);
            GUI.Box(_menuRect, GUIContent.none);
            var buttonRect = new Rect(_menuRect.x + 10, y + 26, 120, 28);

            if (target.IsPlant)
            {
                GUI.Label(new Rect(_menuRect.x + 10, y + 4, 400, 20), "ROSTLINA", _title);
                if (Button(ref buttonRect, "Utrhnout list")) _state.Harvest(target.Plant, PlantPart.Leaf);
                if (Oohra.Core.World.Gathering.FruitStage(_state.Island, _state.PlantStates, target.Plant, _state.Clock).HasValue
                    && Button(ref buttonRect, "Utrhnout plod"))
                {
                    _state.Harvest(target.Plant, PlantPart.Fruit);
                }

                if (_state.Menu != null && Button(ref buttonRect, "Vykopat kořen")) _state.Harvest(target.Plant, PlantPart.Root);
            }
            else
            {
                var tile = _state.Island.TileAt(target.TileX, target.TileY);
                GUI.Label(new Rect(_menuRect.x + 10, y + 4, 400, 20), Texts.WaterName(tile).ToUpper(), _title);
                if (Button(ref buttonRect, "Napít se")) _state.Drink(target.TileX, target.TileY);
            }

            return y - 6;
        }

        /// <summary>Posledních pár hlášek nad nabídkou, staré mizí.</summary>
        private void DrawLog(float bottom)
        {
            var lines = _state.Log.Where(l => Time.unscaledTime - l.ShownAt < LogSeconds).ToList();
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
            var inventory = _state.Inventory;
            var rowHeight = 34f;
            var panelHeight = Mathf.Min(height - 40, 90 + Mathf.Max(1, inventory.Stacks.Count) * rowHeight);
            _backpackRect = new Rect((width - 600) / 2, (height - panelHeight) / 2, 600, panelHeight);
            GUI.Box(_backpackRect, GUIContent.none);
            GUI.Box(_backpackRect, GUIContent.none);

            var x = _backpackRect.x + 14;
            var y = _backpackRect.y + 10;
            GUI.Label(new Rect(x, y, 300, 24), "BATOH", _title);
            GUI.Label(new Rect(x + 90, y + 2, 300, 22), $"{inventory.Stacks.Count} z {inventory.Capacity} míst · hra stojí", _label);
            if (GUI.Button(new Rect(_backpackRect.xMax - 94, y, 80, 26), "Zavřít"))
            {
                _state.BackpackOpen = false;
            }

            y += 36;
            if (inventory.Stacks.Count == 0)
            {
                GUI.Label(new Rect(x, y, 560, 22), "Batoh je prázdný. Klikni na rostlinu a utrhni list, plod nebo vykopej kořen.", _label);
                return;
            }

            for (var i = 0; i < inventory.Stacks.Count; i++)
            {
                var stack = inventory.Stacks[i];
                var species = _state.Island.Species[stack.Item.SpeciesId];
                DrawBar(new Rect(x, y + 6, 18, 18), 1f, Palette.Hsl(species.Hue, species.Saturation, species.Lightness));
                GUI.Label(new Rect(x + 26, y + 6, 250, 22), $"{Texts.Describe(stack.Item)}  ×{stack.Count}", _label);

                var buttons = new Rect(x + 290, y + 2, 84, 26);
                var taste = Button(ref buttons, "Ochutnat");
                var eat = Button(ref buttons, "Sníst");
                var drop = Button(ref buttons, "Zahodit");
                if (taste || eat || drop)
                {
                    // Po akci se batoh změní, proto kreslení ukončíme a dokreslí se v dalším snímku.
                    if (taste) _state.Taste(i);
                    if (eat) _state.Eat(i);
                    if (drop) _state.Drop(i);
                    break;
                }

                y += rowHeight;
            }
        }

        private void DrawDeath(float width, float height)
        {
            var c = _state.Character;
            _deathRect = new Rect((width - 520) / 2, (height - 190) / 2, 520, 190);
            GUI.Box(_deathRect, GUIContent.none);
            GUI.Box(_deathRect, GUIContent.none);

            var x = _deathRect.x + 16;
            var y = _deathRect.y + 14;
            GUI.Label(new Rect(x, y, 480, 26), "ZEMŘEL JSI", _title);
            GUI.Label(new Rect(x, y + 30, 488, 22), c.DeathCause.HasValue ? Texts.For(c.DeathCause.Value) : "Zemřel jsi.", _warn);
            GUI.Label(new Rect(x, y + 58, 488, 44),
                "Přišel jsi o věci u sebe a o všechno, co tvoje postava věděla.\n"
                + $"Narodíš se jako Nový, generace {Oohra.Core.Characters.GenerationTraits.NextAfterDeath(c.Traits.Generation) + 1}. Status Pradávného je navždy pryč.",
                _label);
            if (GUI.Button(new Rect(x, y + 120, 200, 34), "Narodit se znovu"))
            {
                _state.Reborn();
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
            GUI.skin.box.normal.textColor = _label.normal.textColor;
        }
    }
}

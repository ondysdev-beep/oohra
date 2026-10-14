using System.Linq;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Značky v rozhraní (prototyp listy sign a readsign): kreslení na mřížku 12 × 12 (klávesa H) a čtení značky,
    /// na kterou hráč kliknul. Žádný text, jen body. Značku si může přečíst kdokoli, i ty po své smrti.
    /// </summary>
    public class SignHud : MonoBehaviour
    {
        /// <summary>Výška panelu se značkou nad pruhy (GameHud pro něj nechá místo).</summary>
        public const float ReadPanelHeight = 150f;

        private const float BaseHeight = 900f;
        private const float Cell = 22f;

        // Značka se kreslí kousek před postavu (server povolí do 1,6 m).
        private const float DrawAhead = 0.6f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);
        private static Rect _editorRect;
        private static Rect _readRect;

        private ClientSession _session;
        private GUIStyle _label;
        private GUIStyle _title;
        private Texture2D _readTexture;
        private int _readId = -1;
        private bool _paintValue;

        /// <summary>Rozkreslená značka (SignBits).</summary>
        public byte[] Drawing { get; private set; } = SignBits.Empty();

        public bool EditorOpen { get; set; }

        public void Init(ClientSession session)
        {
            _session = session;
        }

        /// <summary>Je myš nad panelem značek (souřadnice rozhraní)?</summary>
        public static bool Covers(Vector2 mouse)
        {
            return _editorRect.Contains(mouse) || _readRect.Contains(mouse);
        }

        /// <summary>Kam se značka nakreslí: kousek před postavu.</summary>
        public Vector2? DrawPlace()
        {
            var self = _session.Connection.SelfSnapshot();
            if (!self.HasValue)
            {
                return null;
            }

            return new Vector2(self.Value.X + Mathf.Cos(self.Value.Facing) * DrawAhead, self.Value.Y + Mathf.Sin(self.Value.Facing) * DrawAhead);
        }

        /// <summary>Pošle kresbu serveru a zavře panel.</summary>
        public void Submit()
        {
            var place = DrawPlace();
            if (!place.HasValue)
            {
                return;
            }

            _session.Connection.DrawSign(place.Value.x, place.Value.y, (byte[])Drawing.Clone());
            Drawing = SignBits.Empty();
            EditorOpen = false;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (_session == null || keyboard == null || _session.Dead)
            {
                EditorOpen = false;
                return;
            }

            if (keyboard.hKey.wasPressedThisFrame) EditorOpen = !EditorOpen;
            if (keyboard.escapeKey.wasPressedThisFrame) EditorOpen = false;
        }

        private void OnGUI()
        {
            if (_session == null || _session.Connection.Self == null)
            {
                return;
            }

            CreateStyles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            _editorRect = _readRect = Rect.zero;
            if (_session.Dead)
            {
                return;
            }

            if (EditorOpen)
            {
                DrawEditor(width, height);
            }

            var sign = _session.MenuSign();
            if (sign != null)
            {
                DrawRead(sign, width, height);
            }
        }

        /// <summary>Mřížka 12 × 12: levé tlačítko kreslí (tažením i víc bodů), pravé maže.</summary>
        private void DrawEditor(float width, float height)
        {
            var gridSize = Cell * SignBits.Size;
            _editorRect = new Rect((width - 340) / 2, (height - gridSize - 150) / 2, 340, gridSize + 150);
            GUI.Box(_editorRect, GUIContent.none);
            GUI.Box(_editorRect, GUIContent.none);
            var x = _editorRect.x + 14;
            var y = _editorRect.y + 8;
            GUI.Label(new Rect(x, y, 300, 24), "ZNAČKA", _title);
            y += 26;
            GUI.Label(new Rect(x, y, 312, 40), PlaceText(), _label);
            y += 44;

            var grid = new Rect(_editorRect.x + (_editorRect.width - gridSize) / 2, y, gridSize, gridSize);
            Paint(grid);
            for (var cy = 0; cy < SignBits.Size; cy++)
            {
                for (var cx = 0; cx < SignBits.Size; cx++)
                {
                    var on = SignBits.Get(Drawing, cx, cy);
                    var old = GUI.color;
                    GUI.color = on ? SignViews.ClayInk : SignViews.Clay;
                    GUI.DrawTexture(new Rect(grid.x + cx * Cell, grid.y + cy * Cell, Cell - 1, Cell - 1), Texture2D.whiteTexture);
                    GUI.color = old;
                }
            }

            y = grid.yMax + 10;
            if (GUI.Button(new Rect(x, y, 100, 28), "Nakreslit")) Submit();
            if (GUI.Button(new Rect(x + 108, y, 90, 28), "Vymazat")) Drawing = SignBits.Empty();
            if (GUI.Button(new Rect(x + 206, y, 90, 28), "Zavřít")) EditorOpen = false;
            GUI.Label(new Rect(x, y + 32, 312, 20), "Levé tlačítko kreslí, pravé maže. Žádný text.", _label);
        }

        /// <summary>Na skále vydrží rytina, v hlíně kresba za pár dní zvětrá (prototyp list sign).</summary>
        private string PlaceText()
        {
            var place = DrawPlace();
            var world = _session.Connection.World;
            var rock = place.HasValue && world.TileAt(Mathf.FloorToInt(place.Value.x), Mathf.FloorToInt(place.Value.y)) == TileType.Rock;
            return rock ? "Stojíš u skály. Rytina vydrží." : "Kreslíš do hlíny. Za pár dní zvětrá.";
        }

        private void Paint(Rect grid)
        {
            var e = Event.current;
            if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || !grid.Contains(e.mousePosition))
            {
                return;
            }

            if (e.type == EventType.MouseDown)
            {
                _paintValue = e.button == 0;
            }

            var cx = Mathf.Clamp((int)((e.mousePosition.x - grid.x) / Cell), 0, SignBits.Size - 1);
            var cy = Mathf.Clamp((int)((e.mousePosition.y - grid.y) / Cell), 0, SignBits.Size - 1);
            SignBits.Set(Drawing, cx, cy, _paintValue);
            e.Use();
        }

        /// <summary>Přečtení značky: kresba, stáří a u staré rytiny záznam o rostlině (může se mýlit).</summary>
        private void DrawRead(SignView sign, float width, float height)
        {
            _readRect = new Rect((width - 520) / 2, height - 86 - ReadPanelHeight, 520, ReadPanelHeight - 6);
            GUI.Box(_readRect, GUIContent.none);
            GUI.Box(_readRect, GUIContent.none);
            if (_readId != sign.Id)
            {
                _readId = sign.Id;
                _readTexture = sign.Carved ? SignViews.Texture(sign.Bits, SignViews.Stone, SignViews.StoneInk) : SignViews.Texture(sign.Bits, SignViews.Clay, SignViews.ClayInk);
            }

            var x = _readRect.x + 10;
            var y = _readRect.y + 8;
            GUI.DrawTexture(new Rect(x, y, 96, 96), _readTexture);
            if (sign.RecordSpeciesId >= 0 && sign.RecordSpeciesId < _session.Connection.World.Species.Length)
            {
                // Barva listů rostliny z rytiny, ať ji jde poznat v přírodě.
                var old = GUI.color;
                GUI.color = Palette.Leaf(_session.Connection.World.Species[sign.RecordSpeciesId]);
                GUI.DrawTexture(new Rect(x, y + 102, 96, 24), Texture2D.whiteTexture);
                GUI.color = old;
            }

            var tx = x + 110;
            GUI.Label(new Rect(tx, y, 380, 22), sign.Ancient ? "STARÁ RYTINA" : sign.Carved ? "RYTINA" : "ZNAČKA", _title);
            GUI.Label(new Rect(tx, y + 24, 390, 60), Describe(sign), _label);
            if (sign.AuthorId == _session.Connection.PlayerId && GUI.Button(new Rect(tx, _readRect.yMax - 36, 110, 28), "Smazat"))
            {
                _session.EraseSign();
            }
        }

        private string Describe(SignView sign)
        {
            if (sign.Ancient)
            {
                var record = sign.RecordSpeciesId >= 0
                    ? "Jméno: " + Oohra.Core.Language.Words.Text(sign.RecordName) + " · "
                      + (sign.RecordTags.Length == 0 ? "bez poznámek" : string.Join(", ", sign.RecordTags.Select(Texts.TagName)))
                    : string.Empty;
                return record + "\nVyryto před dlouhou dobou. Autor se mohl mýlit a svět se od té doby mohl změnit.";
            }

            var now = _session.Clock.TotalMinutes;
            var text = (sign.AuthorId == _session.Connection.PlayerId ? "Tvoje značka" : "Zanecháno") + " " + Texts.Ago(now - sign.CreatedAt) + ".";
            return text + (sign.Carved ? " Rytina vydrží." : " Kresba v hlíně časem zvětrá.");
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
        }
    }
}

using Oohra.Core.Language;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Podpis hráče (návrh 14.1, klávesa P): jméno ze slabik (1 až 3, žádný volný text) a znak nakreslený do mřížky
    /// 12 × 12. Podpis patří hráči (přežije smrt) a ukládá se ke značkám, které nakreslí, a k jeho tělu.
    /// </summary>
    public class SignatureHud : MonoBehaviour
    {
        private const float BaseHeight = 900f;
        private const float Cell = 18f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);
        private static Rect _panelRect;

        private readonly BitGrid _grid = new BitGrid();
        private ClientSession _session;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _hint;

        /// <summary>Rozepsané jméno a znak (při otevření se načte uložený podpis).</summary>
        public SpeechInput NameInput { get; } = new SpeechInput();

        public byte[] Mark { get; private set; } = SignBits.Empty();

        public bool Open { get; private set; }

        public void Init(ClientSession session)
        {
            _session = session;
        }

        /// <summary>Je myš nad panelem podpisu (souřadnice rozhraní)?</summary>
        public static bool Covers(Vector2 mouse)
        {
            return _panelRect.Contains(mouse);
        }

        /// <summary>Otevře panel s uloženým podpisem (nebo prázdný).</summary>
        public void Show()
        {
            Open = true;
            NameInput.Set(_session.Connection.SignatureName);
            var saved = _session.Connection.SignatureMark;
            Mark = saved.Length == SignBits.ByteCount ? (byte[])saved.Clone() : SignBits.Empty();
        }

        /// <summary>Pošle podpis serveru. Jméno musí mít aspoň jednu slabiku.</summary>
        public bool Save()
        {
            if (NameInput.Count == 0)
            {
                return false;
            }

            _session.Connection.SetSignature(NameInput.ToArray(), (byte[])Mark.Clone());
            Open = false;
            return true;
        }

        private void Update()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (_session == null || keyboard == null)
            {
                return;
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                if (Open) Open = false;
                else Show();
            }

            if (keyboard.escapeKey.wasPressedThisFrame) Open = false;
        }

        private void OnGUI()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            _panelRect = Rect.zero;
            if (_session == null || _session.Connection.Self == null || !Open)
            {
                return;
            }

            CreateStyles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var gridSize = BitGrid.Size(Cell);
            _panelRect = new Rect((width - 460) / 2, Mathf.Max(100, (height - gridSize - 210) / 2), 460, gridSize + 210);
            GUI.Box(_panelRect, GUIContent.none);
            GUI.Box(_panelRect, GUIContent.none);

            var x = _panelRect.x + 12;
            var y = _panelRect.y + 8;
            GUI.Label(new Rect(x, y, 300, 22), "PODPIS", _title);
            GUI.Label(new Rect(x + 80, y + 2, 360, 22), "Jméno ze slabik a znak. Uvidí ho ostatní na značkách a u těla.", _hint);
            y += 28;

            for (var i = 0; i < Words.Syllables.Length; i++)
            {
                if (GUI.Button(new Rect(x + i * 43, y, 41, 26), Words.Syllables[i], _small))
                {
                    NameInput.Add((byte)i);
                }
            }

            y += 32;
            GUI.Label(new Rect(x, y, 220, 24), NameInput.Count == 0 ? "Jméno: vyber 1 až 3 slabiky" : "Jméno: " + NameInput.Text, _title);
            if (GUI.Button(new Rect(x + 230, y, 70, 24), "Smazat")) NameInput.RemoveLast();
            y += 32;

            GUI.Label(new Rect(x, y, 200, 22), "Znak (levé kreslí, pravé maže):", _label);
            var grid = new Rect(x, y + 24, gridSize, gridSize);
            _grid.Edit(grid, Mark, SignViews.Clay, SignViews.ClayInk);
            if (GUI.Button(new Rect(grid.xMax + 12, grid.y, 100, 26), "Vymazat znak")) Mark = SignBits.Empty();

            var bottom = _panelRect.yMax - 36;
            GUI.enabled = NameInput.Count > 0;
            if (GUI.Button(new Rect(x, bottom, 110, 28), "Uložit")) Save();
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + 118, bottom, 90, 28), "Zavřít")) Open = false;
        }

        private void CreateStyles()
        {
            if (_label != null)
            {
                return;
            }

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = false };
            _label.normal.textColor = new Color(0.94f, 0.9f, 0.82f);
            _title = new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Ochre;
            _small = new GUIStyle(GUI.skin.button) { fontSize = 11, wordWrap = false };
            _hint = new GUIStyle(_label) { fontSize = 12 };
        }
    }
}

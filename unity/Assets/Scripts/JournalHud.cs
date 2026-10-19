using Oohra.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Deník (návrh 0.8, kap. 3): otevírá se z batohu („Otevřít“). Stránky jsou kresby 12 × 12 bodů jako značky:
    /// poznámky, mapy, co je jedlé. Obsah drží server (deník je věc, po smrti zůstane u těla a kdo ho vezme, přečte ho).
    /// </summary>
    public class JournalHud : MonoBehaviour
    {
        private const float BaseHeight = 900f;
        private const float Cell = 20f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);
        private static Rect _panelRect;

        private readonly BitGrid _grid = new BitGrid();
        private ClientSession _session;
        private int _stackIndex = -1;
        private int _page;
        private byte[] _drawing = SignBits.Empty();
        private bool _edited;
        private GUIStyle _label;
        private GUIStyle _title;

        /// <summary>Otevřený deník (hromádka v batohu), -1 = zavřený.</summary>
        public int StackIndex
        {
            get { return _stackIndex; }
        }

        public int Page
        {
            get { return _page; }
        }

        public void Init(ClientSession session)
        {
            _session = session;
        }

        public static bool Covers(Vector2 mouse)
        {
            return _panelRect.Contains(mouse);
        }

        /// <summary>Otevře deník z hromádky v batohu: server pošle stránky.</summary>
        public void Show(int stackIndex)
        {
            _stackIndex = stackIndex;
            _page = 0;
            _edited = false;
            _drawing = SignBits.Empty();
            _session.Connection.ReadJournal(stackIndex);
        }

        public void Close()
        {
            _stackIndex = -1;
        }

        /// <summary>Uloží rozkreslenou stránku na server.</summary>
        public void SavePage()
        {
            _session.Connection.DrawJournal(_stackIndex, _page, (byte[])_drawing.Clone());
            _edited = false;
        }

        /// <summary>Kresba na stránce, jak ji teď ukazuje okno (pro testy).</summary>
        public byte[] Drawing
        {
            get { return _drawing; }
        }

        public void SelectPage(int page)
        {
            _page = page;
            _edited = false;
            LoadPage();
        }

        private void LoadPage()
        {
            var journal = _session.Connection.Journal;
            if (journal != null && journal.StackIndex == _stackIndex && _page < journal.Pages.Length)
            {
                _drawing = (byte[])journal.Pages[_page].Clone();
            }
        }

        private void Update()
        {
            if (_session == null || _stackIndex < 0)
            {
                return;
            }

            // Dokud hráč nekreslí, ukazuje se stránka tak, jak ji má server (i po uložení).
            if (!_edited)
            {
                LoadPage();
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnGUI()
        {
            _panelRect = Rect.zero;
            if (DreamView.Active || _session == null || _stackIndex < 0 || _session.Connection.Self == null)
            {
                return;
            }

            CreateStyles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var gridSize = BitGrid.Size(Cell);
            _panelRect = new Rect((width - 520) / 2, Mathf.Max(80, (height - gridSize - 150) / 2), 520, gridSize + 150);
            GUI.Box(_panelRect, GUIContent.none);
            GUI.Box(_panelRect, GUIContent.none);

            var x = _panelRect.x + 12;
            var y = _panelRect.y + 8;
            GUI.Label(new Rect(x, y, 300, 22), "DENÍK", _title);
            GUI.Label(new Rect(x + 70, y + 2, 430, 22), "Kresby, mapy, co je jedlé. Po smrti zůstane u těla.", _label);
            y += 28;

            var journal = _session.Connection.Journal;
            var pages = journal != null && journal.StackIndex == _stackIndex ? journal.Pages.Length : 0;
            for (var p = 0; p < pages; p++)
            {
                var selected = p == _page;
                if (GUI.Toggle(new Rect(x + p * 40, y, 38, 24), selected, (p + 1).ToString(), GUI.skin.button) && !selected)
                {
                    SelectPage(p);
                }
            }

            y += 32;
            var grid = new Rect(x, y, gridSize, gridSize);
            if (_grid.Edit(grid, _drawing, SignViews.Clay, SignViews.ClayInk))
            {
                _edited = true;
            }

            if (GUI.Button(new Rect(grid.xMax + 12, grid.y, 140, 28), "Uložit stránku")) SavePage();
            if (GUI.Button(new Rect(grid.xMax + 12, grid.y + 34, 140, 28), "Vymazat")) { _drawing = SignBits.Empty(); _edited = true; }
            if (GUI.Button(new Rect(grid.xMax + 12, grid.yMax - 28, 140, 28), "Zavřít")) Close();
            if (_edited) GUI.Label(new Rect(grid.xMax + 12, grid.y + 70, 160, 40), "Neuloženo", _label);
        }

        private void CreateStyles()
        {
            if (_label != null)
            {
                return;
            }

            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _label.normal.textColor = new Color(0.94f, 0.9f, 0.82f);
            _title = new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Ochre;
        }
    }
}

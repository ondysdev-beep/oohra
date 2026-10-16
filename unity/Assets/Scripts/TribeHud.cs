using Oohra.Core.Items;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Kmen v rozhraní (návrh 14.1): pozvánka od jiného hráče (Přijmout, Odmítnout), kolik lidí je v kmeni
    /// (Opustit kmen) a koš se zásobami kmene (uložit z batohu, vzít do batohu). Žádná správa ani listina.
    /// </summary>
    public class TribeHud : MonoBehaviour
    {
        private const float BaseHeight = 900f;
        private const float InviteSeconds = 35f;
        private const float RowHeight = 30f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);
        private static Rect _inviteRect;
        private static Rect _tribeRect;
        private static Rect _stockRect;

        private ClientSession _session;
        private GUIStyle _label;
        private GUIStyle _title;
        private float _inviteUntil;

        /// <summary>Kdo mě zve do kmene (číslo hráče), nebo -1.</summary>
        public int PendingInvite { get; private set; } = -1;

        public void Init(ClientSession session)
        {
            _session = session;
        }

        /// <summary>Je myš nad panelem kmene (souřadnice rozhraní)?</summary>
        public static bool Covers(Vector2 mouse)
        {
            return _inviteRect.Contains(mouse) || _tribeRect.Contains(mouse) || _stockRect.Contains(mouse);
        }

        /// <summary>Odpovědět na pozvánku.</summary>
        public void Answer(bool accept)
        {
            if (PendingInvite >= 0)
            {
                _session.Connection.AnswerInvite(PendingInvite, accept);
                PendingInvite = -1;
            }
        }

        private void Update()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            if (_session == null)
            {
                return;
            }

            foreach (var inviter in _session.Connection.TakeInvites())
            {
                PendingInvite = inviter;
                _inviteUntil = Time.time + InviteSeconds;
            }

            if (PendingInvite >= 0 && Time.time > _inviteUntil)
            {
                PendingInvite = -1;
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
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            _inviteRect = _tribeRect = _stockRect = Rect.zero;
            if (_session.Dead)
            {
                return;
            }

            DrawTribe();
            if (PendingInvite >= 0)
            {
                DrawInvite(width);
            }

            var stockpile = _session.MenuStockpile();
            if (stockpile != null)
            {
                _session.BackpackOpen = false;
                DrawStockpile(stockpile, width, height);
            }
        }

        /// <summary>Pod údaji vlevo nahoře: kolik lidí je v kmeni a tlačítko Opustit kmen.</summary>
        private void DrawTribe()
        {
            var members = _session.Connection.TribeMembers.Length;
            if (members == 0)
            {
                return;
            }

            _tribeRect = new Rect(12, 108, 330, 30);
            GUI.Box(_tribeRect, GUIContent.none);
            GUI.Label(new Rect(20, 112, 200, 22), "Jsi v kmeni (lidí: " + members + ")", _title);
            if (GUI.Button(new Rect(_tribeRect.xMax - 112, 111, 104, 24), "Opustit kmen"))
            {
                _session.Connection.LeaveTribe();
            }
        }

        private void DrawInvite(float width)
        {
            PlayerInfo info;
            var name = _session.Connection.Players.TryGetValue(PendingInvite, out info) ? info.Nickname : "Někdo";
            _inviteRect = new Rect((width - 420) / 2, 70, 420, 70);
            GUI.Box(_inviteRect, GUIContent.none);
            GUI.Box(_inviteRect, GUIContent.none);
            GUI.Label(new Rect(_inviteRect.x + 12, _inviteRect.y + 6, 396, 22), name + " tě zve do svého kmene.", _title);
            if (GUI.Button(new Rect(_inviteRect.x + 12, _inviteRect.y + 34, 120, 28), "Přijmout")) Answer(true);
            if (GUI.Button(new Rect(_inviteRect.x + 140, _inviteRect.y + 34, 120, 28), "Odmítnout")) Answer(false);
        }

        /// <summary>Koš se zásobami: vlevo batoh (Uložit), vpravo zásoby (Vzít). Přesouvá se celá hromádka.</summary>
        private void DrawStockpile(StockpileView stockpile, float width, float height)
        {
            var inventory = _session.Connection.Inventory;
            var rows = Mathf.Max(1, Mathf.Max(inventory.Stacks.Length, stockpile.Contents.Stacks.Length));
            var panelHeight = Mathf.Min(height - 260, 70 + rows * RowHeight);
            _stockRect = new Rect((width - 720) / 2, Mathf.Max(110, (height - panelHeight) / 2 - 60), 720, panelHeight);
            GUI.Box(_stockRect, GUIContent.none);
            GUI.Box(_stockRect, GUIContent.none);

            var left = _stockRect.x + 12;
            var right = _stockRect.x + 368;
            var y = _stockRect.y + 8;
            GUI.Label(new Rect(left, y, 340, 22), "BATOH  " + inventory.Stacks.Length + "/" + inventory.Capacity, _title);
            GUI.Label(new Rect(right, y, 340, 22), "ZÁSOBY KMENE  " + stockpile.Contents.Stacks.Length + "/" + stockpile.Contents.Capacity, _title);
            y += 30;

            var id = stockpile.StructureId;
            for (var i = 0; i < inventory.Stacks.Length; i++)
            {
                if (StackRow(inventory.Stacks[i], new Rect(left, y + i * RowHeight, 340, RowHeight - 4), "Uložit →"))
                {
                    _session.Connection.StoreItem(id, i);
                }
            }

            for (var i = 0; i < stockpile.Contents.Stacks.Length; i++)
            {
                if (StackRow(stockpile.Contents.Stacks[i], new Rect(right, y + i * RowHeight, 340, RowHeight - 4), "← Vzít"))
                {
                    _session.Connection.TakeItem(id, i);
                }
            }

            if (stockpile.Contents.Stacks.Length == 0)
            {
                GUI.Label(new Rect(right, y, 340, 22), "Koš je prázdný.", _label);
            }
        }

        /// <summary>Řádek s hromádkou: barva, popis a tlačítko. Vrací, jestli hráč klikl.</summary>
        private bool StackRow(StackView stack, Rect row, string button)
        {
            var plant = stack.Kind == ItemKind.Plant;
            var old = GUI.color;
            GUI.color = plant ? Palette.Leaf(_session.Connection.World.Species[stack.SpeciesId]) : Palette.Material(stack.Kind);
            GUI.DrawTexture(new Rect(row.x, row.y + 5, 16, 16), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(row.x + 22, row.y + 3, row.width - 120, 22), Texts.Describe(stack) + "  ×" + stack.Count, _label);
            return GUI.Button(new Rect(row.xMax - 92, row.y, 88, row.height - 2), button);
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
        }
    }
}

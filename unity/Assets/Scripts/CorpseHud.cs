using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Tělo mrtvého hráče v rozhraní: čí je (podpis), kdy zemřel a co u něj zůstalo. Věci si vezme mrtvý svým
    /// dalším životem a jeho kmen hned, ostatní až po 24 hodinách (návrh 14.1).
    /// </summary>
    public class CorpseHud : MonoBehaviour
    {
        private const float BaseHeight = 900f;
        private const float RowHeight = 30f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);
        private static Rect _panelRect;

        private ClientSession _session;
        private GUIStyle _label;
        private GUIStyle _title;

        public void Init(ClientSession session)
        {
            _session = session;
        }

        /// <summary>Je myš nad panelem těla (souřadnice rozhraní)?</summary>
        public static bool Covers(Vector2 mouse)
        {
            return _panelRect.Contains(mouse);
        }

        private void OnGUI()
        {
            _panelRect = Rect.zero;
            if (_session == null || _session.Connection.Self == null || _session.Dead)
            {
                return;
            }

            var corpse = _session.MenuCorpse();
            if (corpse == null)
            {
                return;
            }

            CreateStyles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _session.BackpackOpen = false;
            Draw(corpse, Screen.width / scale, Screen.height / scale);
        }

        private void Draw(CorpseView corpse, float width, float height)
        {
            var stacks = corpse.Contents.Stacks;
            var panelHeight = Mathf.Min(height - 260, 130 + Mathf.Max(1, stacks.Length) * RowHeight);
            _panelRect = new Rect((width - 440) / 2, Mathf.Max(110, (height - panelHeight) / 2 - 60), 440, panelHeight);
            GUI.Box(_panelRect, GUIContent.none);
            GUI.Box(_panelRect, GUIContent.none);

            var x = _panelRect.x + 12;
            var y = _panelRect.y + 8;
            var mine = corpse.OwnerId == _session.Connection.PlayerId;
            GUI.Label(new Rect(x, y, 300, 22), mine ? "TVOJE TĚLO" : "TĚLO", _title);
            if (corpse.SignatureName.Length > 0)
            {
                GUI.Label(new Rect(x + 120, y + 1, 220, 22), "Podpis: " + Words.Text(corpse.SignatureName), _label);
                if (corpse.SignatureMark.Length == SignBits.ByteCount)
                {
                    BitGrid.Draw(new Rect(_panelRect.xMax - 50, y, 36, 36), corpse.SignatureMark, SignViews.Clay, SignViews.ClayInk);
                }
            }

            y += 26;
            var now = _session.Clock.TotalMinutes;
            GUI.Label(new Rect(x, y, 380, 22), (corpse.Generation == 0 ? "Pradávný" : "Nový") + ", zemřel " + Texts.Ago(now - corpse.DiedAt) + ".", _label);
            y += 22;
            var mayLoot = _session.MayLoot(corpse);
            var note = mayLoot ? "Věci si může vzít kdokoli z vás." : "Ještě " + Hours(corpse.TribeOnlyUntil - now) + " si věci smí vzít jen jeho kmen.";
            GUI.Label(new Rect(x, y, 400, 22), note, _label);
            y += 28;

            if (stacks.Length == 0)
            {
                GUI.Label(new Rect(x, y, 400, 22), "U těla už nic není.", _label);
                return;
            }

            for (var i = 0; i < stacks.Length; i++)
            {
                if (StackRow(stacks[i], new Rect(x, y + i * RowHeight, 416, RowHeight - 4), mayLoot))
                {
                    _session.Connection.TakeFromCorpse(corpse.Id, i);
                }
            }

            if (mayLoot && GUI.Button(new Rect(_panelRect.xMax - 112, _panelRect.yMax - 34, 100, 26), "Vzít vše"))
            {
                // Server bere vždy první hromádku, ostatní se posunou: stačí poslat tolikrát, kolik jich je.
                for (var i = 0; i < stacks.Length; i++)
                {
                    _session.Connection.TakeFromCorpse(corpse.Id, 0);
                }
            }
        }

        private static string Hours(double minutes)
        {
            var hours = Mathf.CeilToInt((float)(minutes / 60));
            return hours <= 1 ? "hodinu" : hours < 5 ? hours + " hodiny" : hours + " hodin";
        }

        private bool StackRow(StackView stack, Rect row, bool canTake)
        {
            var plant = stack.Kind == ItemKind.Plant;
            var old = GUI.color;
            GUI.color = plant ? Palette.Leaf(_session.Connection.World.Species[stack.SpeciesId]) : Palette.Material(stack.Kind);
            GUI.DrawTexture(new Rect(row.x, row.y + 5, 16, 16), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(row.x + 22, row.y + 3, row.width - 120, 22), Texts.Describe(stack) + "  ×" + stack.Count, _label);
            return canTake && GUI.Button(new Rect(row.xMax - 92, row.y, 88, row.height - 2), "Vzít");
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

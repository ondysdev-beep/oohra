using Oohra.Core.Social;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oohra.Client
{
    /// <summary>
    /// Tlačítko Nahlásit (návrh 14.3, správce rozhoduje). Nahlásit jde hráče (v jeho nabídce), značku (při čtení)
    /// nebo obecný problém (tlačítko nahoře). Hráč vybere důvod, volný text hra nemá. Server záznam uloží pro správce.
    /// </summary>
    public class ReportHud : MonoBehaviour
    {
        private const float BaseHeight = 900f;

        private static readonly Color Ochre = new Color(0.86f, 0.64f, 0.25f);
        private static Rect _panelRect;
        private static Rect _buttonRect;

        private ClientSession _session;
        private GUIStyle _label;
        private GUIStyle _title;

        public void Init(ClientSession session)
        {
            _session = session;
        }

        /// <summary>Je myš nad panelem nebo tlačítkem Nahlásit (souřadnice rozhraní)?</summary>
        public static bool Covers(Vector2 mouse)
        {
            return _panelRect.Contains(mouse) || _buttonRect.Contains(mouse);
        }

        /// <summary>Důvody, které se k cíli hodí (hráč: chování a podvádění, značka: kresba, obecně: chyba).</summary>
        public static ReportReason[] ReasonsFor(ReportTarget target)
        {
            switch (target)
            {
                case ReportTarget.Player: return new[] { ReportReason.Behavior, ReportReason.Cheating, ReportReason.Other };
                case ReportTarget.Sign: return new[] { ReportReason.OffensiveSign, ReportReason.Other };
                default: return new[] { ReportReason.Bug, ReportReason.Cheating, ReportReason.Other };
            }
        }

        private void Update()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (_session != null && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _session.PendingReport = null;
            }
        }

        private void OnGUI()
        {
            // Během snu bez slov (nováček) se rozhraní neukazuje a hra nereaguje na ovládání.
            if (DreamView.Active)
            {
                return;
            }

            _panelRect = _buttonRect = Rect.zero;
            if (_session == null || _session.Connection.Self == null)
            {
                return;
            }

            CreateStyles();
            var scale = Mathf.Clamp(Screen.height / BaseHeight, 1f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            // Obecné nahlášení vedle tlačítek nahoře (Batoh, Stavět, Spát, Dovednosti).
            _buttonRect = new Rect(width - 660, 12, 120, 34);
            if (GUI.Button(_buttonRect, "Nahlásit"))
            {
                _session.AskReport(ReportTarget.General, 0, "problém");
            }

            if (_session.PendingReport != null)
            {
                DrawPanel(_session.PendingReport, width, height);
            }
        }

        private void DrawPanel(ReportRequest request, float width, float height)
        {
            var reasons = ReasonsFor(request.Target);
            _panelRect = new Rect((width - 420) / 2, (height - 150 - reasons.Length * 34) / 2, 420, 150 + reasons.Length * 34);
            GUI.Box(_panelRect, GUIContent.none);
            GUI.Box(_panelRect, GUIContent.none);

            var x = _panelRect.x + 14;
            var y = _panelRect.y + 10;
            GUI.Label(new Rect(x, y, 390, 24), "NAHLÁSIT " + request.What.ToUpper(), _title);
            y += 28;
            GUI.Label(new Rect(x, y, 392, 44), "Nahlášení uvidí jen správce testu. Pošli ho, když jde o něco vážného nebo o chybu ve hře.", _label);
            y += 50;
            foreach (var reason in reasons)
            {
                if (GUI.Button(new Rect(x, y, 392, 28), Texts.ReasonName(reason)))
                {
                    _session.SendReport(reason);
                }

                y += 34;
            }

            if (GUI.Button(new Rect(_panelRect.xMax - 114, _panelRect.yMax - 36, 100, 26), "Zrušit"))
            {
                _session.PendingReport = null;
            }
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

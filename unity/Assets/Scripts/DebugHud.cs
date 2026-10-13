using System.Linq;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Time;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Dočasné rozhraní šedé krabice (OnGUI): čas, stav těla a příznaky.
    /// Opravdové rozhraní (pruhy, batoh, zápisník) přijde v dalším kroku Fáze B.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        private GameClock _clock;
        private Character _character;
        private GUIStyle _style;

        public void Init(GameClock clock, Character character)
        {
            _clock = clock;
            _character = character;
        }

        private void OnGUI()
        {
            if (_clock == null)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = false };
            }

            var c = _character;
            var hour = (int)_clock.Hour;
            var minute = (int)((_clock.Hour - hour) * 60);
            var symptoms = c.Symptoms();
            var text =
                $"Den {_clock.Day + 1}, {hour:00}:{minute:00} · {SeasonName(_clock.Season)}, {_clock.MonthOfYear + 1}. měsíc\n" +
                (c.Traits.IsAncient ? "Pradávný" : $"Nový, generace {c.Traits.Generation}") + "\n" +
                $"Sytost {Bar(c.Satiety)}  Voda {Bar(c.Hydration)}  Síla {Bar(c.Energy)}\n" +
                $"Teplo  {Bar(c.Warmth)}  Zdraví {Bar(c.Health)}\n" +
                "Příznaky: " + (symptoms.Count == 0 ? "žádné" : string.Join(" · ", symptoms.Select(SymptomName))) + "\n" +
                "WASD / klik = chůze · Q, E = otočit kameru · kolečko = přiblížit";
            GUI.Box(new Rect(12, 12, 460, 130), text, _style);
        }

        private static string Bar(double value)
        {
            var filled = Mathf.RoundToInt(Mathf.Clamp((float)value, 0, 100) / 10f);
            return "[" + new string('|', filled) + new string('.', 10 - filled) + "]";
        }

        private static string SeasonName(Season season)
        {
            switch (season)
            {
                case Season.Spring: return "Jaro";
                case Season.Summer: return "Léto";
                case Season.Autumn: return "Podzim";
                default: return "Zima";
            }
        }

        private static string SymptomName(Symptom symptom)
        {
            switch (symptom)
            {
                case Symptom.HeavyBody: return "ztěžklé tělo";
                case Symptom.Fever: return "horečka";
                case Symptom.Tremor: return "třes";
                case Symptom.Agitated: return "rozjitřený";
                case Symptom.Diarrhea: return "průjem";
                case Symptom.Inflammation: return "zánět";
                case Symptom.Unconscious: return "bezvědomí";
                default: return symptom.ToString();
            }
        }
    }
}

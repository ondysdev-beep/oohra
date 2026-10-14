using System.Collections.Generic;
using Oohra.Core.Language;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>České texty k jazyku: pojmy, souvislosti, gesta a popis slova v zápisníku (prototyp CTXT a vocabInfo).</summary>
    public static class LanguageTexts
    {
        public static string ConceptName(Concept concept)
        {
            switch (concept)
            {
                case Concept.Food: return "jídlo";
                case Concept.Water: return "voda";
                case Concept.Danger: return "nebezpečí";
                case Concept.Come: return "pojď";
                case Concept.Fire: return "oheň";
                case Concept.Yes: return "ano";
                case Concept.No: return "ne";
                case Concept.Poison: return "jed";
                case Concept.Animal: return "zvíře";
                case Concept.Plant: return "rostlina";
                case Concept.Tree: return "strom";
                case Concept.Stone: return "kámen";
                case Concept.Me: return "já";
                case Concept.You: return "ty";
                case Concept.Good: return "dobré";
                default: return "špatné";
            }
        }

        /// <summary>Co posluchač viděl, když slovo zaznělo.</summary>
        public static string ContextName(SpeechContext context)
        {
            switch (context)
            {
                case SpeechContext.Eating: return "u jídla";
                case SpeechContext.AtWater: return "u vody";
                case SpeechContext.AnimalNear: return "zvíře poblíž";
                case SpeechContext.AtFire: return "u ohně";
                case SpeechContext.Nodding: return "kývá hlavou";
                case SpeechContext.ShakingHead: return "vrtí hlavou";
                default: return "odhazuje věc";
            }
        }

        public static string GestureName(Gesture gesture)
        {
            switch (gesture)
            {
                case Gesture.Point: return "Ukázat";
                case Gesture.Yes: return "Kývnout (ano)";
                case Gesture.No: return "Zavrtět hlavou (ne)";
                case Gesture.Come: return "Pojď sem";
                case Gesture.Beware: return "Pozor!";
                case Gesture.Eat: return "Jíst";
                case Gesture.Invite: return "Pozvat do kmene";
                default: return "Pít";
            }
        }

        /// <summary>Text bubliny nad postavou, která udělala gesto.</summary>
        public static string GestureBubble(Gesture gesture)
        {
            switch (gesture)
            {
                case Gesture.Point: return "ukazuje ▼";
                case Gesture.Yes: return "kývá hlavou";
                case Gesture.No: return "vrtí hlavou";
                case Gesture.Come: return "kývá rukou: pojď";
                case Gesture.Beware: return "varovně mává rukama!";
                case Gesture.Eat: return "dělá, že jí";
                case Gesture.Invite: return "zve do kmene";
                default: return "dělá, že pije";
            }
        }

        public static string ToneName(Tone tone)
        {
            switch (tone)
            {
                case Tone.Alarm: return "Poplach";
                case Tone.Joy: return "Radost";
                default: return "Klidně";
            }
        }

        /// <summary>Barva bubliny podle nálady (slovu nemusí hráč rozumět, náladu pozná).</summary>
        public static Color ToneColor(Tone tone)
        {
            switch (tone)
            {
                case Tone.Alarm: return new Color(0.78f, 0.25f, 0.2f, 0.9f);
                case Tone.Joy: return new Color(0.3f, 0.6f, 0.3f, 0.9f);
                default: return new Color(0.18f, 0.16f, 0.13f, 0.85f);
            }
        }

        /// <summary>
        /// Popis slova v zápisníku: kolikrát zaznělo, v jakých situacích a odhad významu, když ho hráč ještě
        /// nevyplnil (např. „slyšeno 3× · u jídla ×2, u ohně ×1 · asi: jídlo“).
        /// </summary>
        public static string Describe(WordEntryView entry)
        {
            var parts = new List<string>();
            if (entry.OldVoice)
            {
                parts.Add("Starý hlas");
            }

            parts.Add("slyšeno " + entry.Heard + "×");
            var seen = new List<string>();
            var best = -1;
            for (var i = 0; i < entry.Contexts.Length; i++)
            {
                if (entry.Contexts[i] <= 0)
                {
                    continue;
                }

                seen.Add(ContextName((SpeechContext)i) + " ×" + entry.Contexts[i]);
                if (best < 0 || entry.Contexts[i] > entry.Contexts[best])
                {
                    best = i;
                }
            }

            if (seen.Count > 0)
            {
                parts.Add(string.Join(", ", seen));
            }

            if (best >= 0 && entry.Meaning == null)
            {
                parts.Add("asi: " + ConceptName(Words.GuessOf((SpeechContext)best)));
            }

            return string.Join(" · ", parts);
        }
    }
}

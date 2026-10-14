using System.Collections.Generic;

namespace Oohra.Core.Language
{
    /// <summary>Nálada vyslovených slov (návrh 9.6, bod 1). Je vidět barvou bubliny, i když slovo hráč nezná.</summary>
    public enum Tone
    {
        Calm = 0,
        Alarm = 1,
        Joy = 2,
    }

    /// <summary>Univerzální gesta (návrh 9.3 a 9.6, bod 4). Gestům rozumí každý, slovům ne.</summary>
    public enum Gesture
    {
        /// <summary>Ukázat prstem na místo.</summary>
        Point = 0,
        Yes = 1,
        No = 2,
        Come = 3,
        Beware = 4,
        Eat = 5,
        Drink = 6,


        /// <summary>Pozvat do kmene (posílá se se zprávou o pozvání, ne samostatně).</summary>
        Invite = 7,

        /// <summary>Krčí rameny: neví, co hráč chce (dělají to cizinci, když slovu nerozumí).</summary>
        Puzzled = 8,
    }

    /// <summary>
    /// V jaké situaci byl ten, kdo slovo řekl (prototyp CTXT). Posluchač to vidí, ne co slovo znamená.
    /// Bity v jednom bajtu (1 &lt;&lt; hodnota).
    /// </summary>
    public enum SpeechContext
    {
        /// <summary>Jedl nebo ochutnával.</summary>
        Eating = 0,
        AtWater = 1,

        /// <summary>Poblíž je zvíře (nebezpečné, nebo zrovna loví).</summary>
        AnimalNear = 2,
        AtFire = 3,

        /// <summary>Kývá hlavou (gesto ano).</summary>
        Nodding = 4,

        /// <summary>Vrtí hlavou (gesto ne).</summary>
        ShakingHead = 5,

        /// <summary>Odhazuje věc.</summary>
        Discarding = 6,
    }

    /// <summary>
    /// Pojmy, které si hráč může přiřadit ke slovu v zápisníku (návrh 9.3). Ve hře není volný text,
    /// proto se význam vybírá ze seznamu. Hra význam nikdy nepotvrzuje.
    /// </summary>
    public enum Concept
    {
        Food = 0,
        Water = 1,
        Danger = 2,
        Come = 3,
        Fire = 4,
        Yes = 5,
        No = 6,
        Poison = 7,
        Animal = 8,
        Plant = 9,
        Tree = 10,
        Stone = 11,
        Me = 12,
        You = 13,
        Good = 14,
        Bad = 15,
    }

    /// <summary>Slova ze slabik: názvy slabik a pomůcky pro zápis slova (prototyp SYL a wordStr).</summary>
    public static class Words
    {
        /// <summary>Deset slabik, ze kterých se skládají všechna slova (návrh 9.1).</summary>
        public static readonly string[] Syllables = { "Gooh", "Arh", "Oo", "Mah", "Tek", "Rin", "Ush", "Dak", "Hee", "Lom" };

        /// <summary>Nejvíc slabik v jednom slově.</summary>
        public const int MaxSyllables = 3;

        /// <summary>Slovo má 1 až 3 slabiky a každá je jedna z deseti.</summary>
        public static bool IsValid(IReadOnlyList<byte>? word)
        {
            if (word == null || word.Count < 1 || word.Count > MaxSyllables)
            {
                return false;
            }

            foreach (var syllable in word)
            {
                if (syllable >= Syllables.Length)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Klíč slova pro slovník, např. "0-3".</summary>
        public static string Key(IReadOnlyList<byte> word)
        {
            return string.Join("-", word);
        }

        /// <summary>Slovo tak, jak ho hráč vidí, např. "Gooh-Mah".</summary>
        public static string Text(IReadOnlyList<byte> word)
        {
            var parts = new string[word.Count];
            for (var i = 0; i < word.Count; i++)
            {
                parts[i] = word[i] < Syllables.Length ? Syllables[word[i]] : "?";
            }

            return string.Join("-", parts);
        }

        /// <summary>
        /// Jaký význam napovídá souvislost (prototyp CTXMEAN). Je to jen odhad, hra význam nikdy nepotvrdí.
        /// </summary>
        public static Concept GuessOf(SpeechContext context)
        {
            switch (context)
            {
                case SpeechContext.Eating: return Concept.Food;
                case SpeechContext.AtWater: return Concept.Water;
                case SpeechContext.AnimalNear: return Concept.Danger;
                case SpeechContext.AtFire: return Concept.Fire;
                case SpeechContext.Nodding: return Concept.Yes;
                case SpeechContext.ShakingHead: return Concept.No;
                default: return Concept.Poison;
            }
        }

        /// <summary>Počet druhů souvislostí (velikost pole počtů ve slovníku).</summary>
        public const int ContextCount = 7;
    }
}

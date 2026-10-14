using System.Collections.Generic;
using Oohra.Core.Language;

namespace Oohra.Client
{
    /// <summary>
    /// Rozepsané slovo v panelu řeči: 1 až 3 slabiky (Shift + 1 až 0), Backspace maže poslední,
    /// Enter slovo řekne (prototyp spBuf, addSyl a delSyl).
    /// </summary>
    public sealed class SpeechInput
    {
        private readonly List<byte> _syllables = new List<byte>();

        public int Count
        {
            get { return _syllables.Count; }
        }

        /// <summary>Přidá slabiku, když slovo ještě nemá tři.</summary>
        public void Add(byte syllable)
        {
            if (_syllables.Count < Words.MaxSyllables && syllable < Words.Syllables.Length)
            {
                _syllables.Add(syllable);
            }
        }

        public void RemoveLast()
        {
            if (_syllables.Count > 0)
            {
                _syllables.RemoveAt(_syllables.Count - 1);
            }
        }

        public void Clear()
        {
            _syllables.Clear();
        }

        /// <summary>Nahradí rozepsané slovo jiným (Zopakovat, Říct ze zápisníku).</summary>
        public void Set(IReadOnlyList<byte> word)
        {
            _syllables.Clear();
            foreach (var syllable in word)
            {
                Add(syllable);
            }
        }

        public byte[] ToArray()
        {
            return _syllables.ToArray();
        }

        /// <summary>Slabika podle číslice na klávese: 1 = první slabika, …, 0 = desátá (prototyp (n + 9) % 10).</summary>
        public static byte SyllableOfDigit(int digit)
        {
            return (byte)((digit + 9) % 10);
        }

        public string Text
        {
            get { return _syllables.Count == 0 ? "Slabiky: Shift + 1 až 0" : Words.Text(_syllables); }
        }
    }
}

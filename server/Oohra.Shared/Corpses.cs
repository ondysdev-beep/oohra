using System.Collections.Generic;
using Oohra.Core.Language;

namespace Oohra.Core.World
{
    /// <summary>Výsledek braní věcí z těla mrtvého hráče.</summary>
    public enum CorpseResult
    {
        Taken = 0,
        TooFar = 1,
        NotFound = 2,
        InventoryFull = 3,

        /// <summary>Prvních 24 hodin smí z těla brát jen mrtvý (jeho další život) a jeho kmen.</summary>
        TribeOnly = 4,

        /// <summary>V těle už nic není.</summary>
        Empty = 5,
    }

    /// <summary>
    /// Podpis hráče (návrh 14.1): jméno ze slabik (1 až 3) a znak, kresba 12 × 12 bodů (SignBits).
    /// Žádný volný text. Patří hráči, ne postavě, takže přežije smrt.
    /// </summary>
    public sealed class Signature
    {
        public Signature(byte[] name, byte[] mark)
        {
            Name = name;
            Mark = mark;
        }

        public byte[] Name { get; }

        public byte[] Mark { get; }

        /// <summary>Jméno má 1 až 3 slabiky a znak správnou velikost.</summary>
        public static bool IsValid(IReadOnlyList<byte>? name, byte[]? mark)
        {
            return Words.IsValid(name) && mark != null && mark.Length == SignBits.ByteCount;
        }
    }
}

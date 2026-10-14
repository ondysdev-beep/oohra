using System.Collections.Generic;

namespace Oohra.Core.World
{
    /// <summary>Výsledek kreslení nebo smazání značky.</summary>
    public enum SignResult
    {
        Drawn = 0,
        Erased = 1,

        /// <summary>Kresba nemá žádný bod.</summary>
        Empty = 2,
        TooFar = 3,

        /// <summary>Do vody nebo hned vedle jiné značky kreslit nejde.</summary>
        CannotDrawHere = 4,
        NotFound = 5,

        /// <summary>Cizí značku smazat nejde.</summary>
        NotYours = 6,

        /// <summary>Hráč má ve světě už moc značek.</summary>
        TooMany = 7,
    }

    /// <summary>Poznámky na starých rytinách (prototyp TAGS). Jsou zčásti chybné, autor se mohl mýlit.</summary>
    public enum CarvingTag
    {
        Filling = 0,
        Bitter = 1,
        Burns = 2,
        Vomit = 3,
        Diarrhea = 4,
        Fever = 5,
        Tremor = 6,
        Faint = 7,
        Strengthened = 8,
        Heals = 9,
    }

    /// <summary>
    /// Kresba značky: mřížka 12 × 12 bodů (prototyp bits), uložená po bitech v 18 bajtech.
    /// Bod (x, y) je bit číslo y * 12 + x, x doprava, y dolů.
    /// </summary>
    public static class SignBits
    {
        public const int Size = 12;
        public const int ByteCount = Size * Size / 8;

        public static byte[] Empty()
        {
            return new byte[ByteCount];
        }

        public static bool Get(byte[] bits, int x, int y)
        {
            var i = y * Size + x;
            return (bits[i / 8] & (1 << (i % 8))) != 0;
        }

        public static void Set(byte[] bits, int x, int y, bool on)
        {
            var i = y * Size + x;
            if (on)
            {
                bits[i / 8] |= (byte)(1 << (i % 8));
            }
            else
            {
                bits[i / 8] &= (byte)~(1 << (i % 8));
            }
        }

        public static int Count(byte[] bits)
        {
            var count = 0;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    if (Get(bits, x, y)) count++;
                }
            }

            return count;
        }

        /// <summary>Kresba z textu "0101..." (144 znaků, jako v prototypu).</summary>
        public static byte[] FromText(string text)
        {
            var bits = Empty();
            for (var i = 0; i < text.Length && i < Size * Size; i++)
            {
                Set(bits, i % Size, i / Size, text[i] == '1');
            }

            return bits;
        }

        public static string ToText(byte[] bits)
        {
            var chars = new List<char>(Size * Size);
            for (var i = 0; i < Size * Size; i++)
            {
                chars.Add(Get(bits, i % Size, i / Size) ? '1' : '0');
            }

            return new string(chars.ToArray());
        }
    }
}

namespace Oohra.Core.Rng;

/// <summary>
/// Hašovací funkce přepsané z prototypu (part1.js). Dají pro stejné vstupy vždy stejné číslo.
/// </summary>
public static class Hash
{
    /// <summary>
    /// Číslo 0 (včetně) až 1 (bez 1) pro souřadnice a seed, jako hash2 v prototypu.
    /// </summary>
    public static double Hash2(int x, int y, int seed)
    {
        // V JS se součet počítá v double a pak ořízne na 32 bitů. Výsledek je stejný jako sčítání int s přetečením.
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + seed * 2147483647;
            h = (h ^ (int)((uint)h >> 13)) * 1274126177;
            return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
        }
    }
}

namespace Oohra.Core.Rng;

/// <summary>
/// Generátor náhodných čísel přepsaný z prototypu (funkce mulberry32 v part1.js).
/// Stejný seed dá stejnou řadu čísel jako v prototypu. Ve světě se používá místo System.Random.
/// </summary>
public sealed class Mulberry32
{
    private int _state;

    public Mulberry32(int seed)
    {
        _state = seed;
    }

    /// <summary>Další číslo v rozsahu 0 (včetně) až 1 (bez 1), jako r() v prototypu.</summary>
    public double NextDouble()
    {
        // V JS: Math.imul je násobení int s přetečením, >>> je posun u uint.
        unchecked
        {
            _state += 0x6D2B79F5;
            var a = _state;
            var t = (a ^ (int)((uint)a >> 15)) * (1 | a);
            t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
            return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
        }
    }
}

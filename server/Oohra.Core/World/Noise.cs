using Oohra.Core.Rng;

namespace Oohra.Core.World;

/// <summary>
/// Šum pro tvar ostrova, přepsaný z prototypu (vnoise a fbm v part1.js).
/// Pořadí operací je stejné jako v JS, aby desetinná čísla vyšla bit po bitu stejně.
/// </summary>
public static class Noise
{
    /// <summary>Hladký šum 0 až 1 z hodnot v rozích mřížky.</summary>
    public static double Value(double x, double y, int seed)
    {
        var xi = Math.Floor(x);
        var yi = Math.Floor(y);
        var xf = x - xi;
        var yf = y - yi;
        var ix = (int)xi;
        var iy = (int)yi;

        var a = Hash.Hash2(ix, iy, seed);
        var b = Hash.Hash2(ix + 1, iy, seed);
        var c = Hash.Hash2(ix, iy + 1, seed);
        var d = Hash.Hash2(ix + 1, iy + 1, seed);
        var u = Smooth(xf);
        var v = Smooth(yf);

        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    /// <summary>Čtyři vrstvy šumu přes sebe (hrubé tvary a jemné detaily), výsledek zhruba 0 až 1.</summary>
    public static double Fbm(double x, double y, int seed)
    {
        var value = 0.0;
        var amplitude = 0.5;
        var frequency = 1.0;
        for (var i = 0; i < 4; i++)
        {
            value += amplitude * Value(x * frequency, y * frequency, unchecked(seed + i * 101));
            amplitude /= 2;
            frequency *= 2;
        }

        return value / 0.9375;
    }

    private static double Smooth(double t)
    {
        return t * t * (3 - 2 * t);
    }
}

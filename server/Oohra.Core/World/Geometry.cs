namespace Oohra.Core.World;

/// <summary>Výpočty s úsečkami (zdi) v rovině mapy. Souřadnice v metrech (= dlaždicích).</summary>
public static class Geometry
{
    /// <summary>Vzdálenost bodu (px, py) od úsečky (x1, y1)–(x2, y2).</summary>
    public static double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared > 0 ? Math.Clamp(((px - x1) * dx + (py - y1) * dy) / lengthSquared, 0, 1) : 0;
        var cx = x1 + t * dx - px;
        var cy = y1 + t * dy - py;
        return Math.Sqrt(cx * cx + cy * cy);
    }

    /// <summary>
    /// Kde paprsek z (ox, oy) ve směru (dx, dy) (jednotkový) protne úsečku. Vrací vzdálenost od začátku
    /// paprsku, nebo null, když ji neprotne.
    /// </summary>
    public static double? RayHitsSegment(double ox, double oy, double dx, double dy, double x1, double y1, double x2, double y2)
    {
        var ex = x2 - x1;
        var ey = y2 - y1;
        var denominator = dx * ey - dy * ex;
        if (Math.Abs(denominator) < 1e-12)
        {
            return null;
        }

        var t = ((x1 - ox) * ey - (y1 - oy) * ex) / denominator;
        var s = ((x1 - ox) * dy - (y1 - oy) * dx) / denominator;
        return t >= 0 && s >= -1e-9 && s <= 1 + 1e-9 ? t : null;
    }
}

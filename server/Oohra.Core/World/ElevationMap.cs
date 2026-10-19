namespace Oohra.Core.World;

/// <summary>
/// Výška terénu (metry nad hladinou) v rozích dlaždic, mezi nimi se dopočítá (bilineárně). Voda leží ve výšce 0,
/// souš se od ní zvedá, ve vrchovině jsou kopce a skalní hřbety. Vzniká ze seedu (neukládá se).
/// </summary>
public sealed class ElevationMap
{
    private readonly float[] _corners;

    /// <summary>corners: (width + 1) × (height + 1) výšek rohů dlaždic po řádcích.</summary>
    public ElevationMap(int width, int height, float[] corners)
    {
        if (corners.Length != (width + 1) * (height + 1))
        {
            throw new ArgumentException("Výšek rohů musí být (šířka + 1) × (výška + 1).", nameof(corners));
        }

        Width = width;
        Height = height;
        _corners = corners;
    }

    /// <summary>Rovina ve výšce 0 (testy, staré světy).</summary>
    public static ElevationMap Flat(int width, int height)
    {
        return new ElevationMap(width, height, new float[(width + 1) * (height + 1)]);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Výšky rohů po řádcích (pro klienta).</summary>
    public IReadOnlyList<float> Corners => _corners;

    public float Corner(int x, int y)
    {
        return _corners[y * (Width + 1) + x];
    }

    /// <summary>Výška terénu v bodě (x, y) na mapě. Mimo mapu 0.</summary>
    public double At(double x, double y)
    {
        if (x < 0 || y < 0 || x > Width || y > Height)
        {
            return 0;
        }

        var (cx, cy) = (Math.Min((int)Math.Floor(x), Width - 1), Math.Min((int)Math.Floor(y), Height - 1));
        var (fx, fy) = (x - cx, y - cy);
        var top = Corner(cx, cy) * (1 - fx) + Corner(cx + 1, cy) * fx;
        var bottom = Corner(cx, cy + 1) * (1 - fx) + Corner(cx + 1, cy + 1) * fx;
        return top * (1 - fy) + bottom * fy;
    }

    /// <summary>Sklon v bodě: o kolik metrů terén stoupne na metr ve směru (dx, dy) (jednotkový směr).</summary>
    public double Grade(double x, double y, double dx, double dy)
    {
        const double h = 0.25;
        return (At(x + dx * h, y + dy * h) - At(x - dx * h, y - dy * h)) / (2 * h);
    }

    /// <summary>Největší sklon v bodě (strmost svahu), metrů na metr.</summary>
    public double Steepness(double x, double y)
    {
        var gx = Grade(x, y, 1, 0);
        var gy = Grade(x, y, 0, 1);
        return Math.Sqrt(gx * gx + gy * gy);
    }

    /// <summary>Stejný terén, jen mapa vyšší (podzemí s jeskyněmi pod ostrovem je rovné, ve výšce 0).</summary>
    public ElevationMap WithHeight(int height)
    {
        var corners = new float[(Width + 1) * (height + 1)];
        Array.Copy(_corners, corners, Math.Min(_corners.Length, corners.Length));
        return new ElevationMap(Width, height, corners);
    }
}

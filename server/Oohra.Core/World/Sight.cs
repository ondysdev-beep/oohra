namespace Oohra.Core.World;

/// <summary>
/// Je mezi dvěma místy volno? Úsečka nesmí vést přes nic, co překáží v chůzi: zeď (i se otvorem jen tam, kde je
/// zeď), sloupek, dekoraci, přes kterou se nedá projít, ležící kmen, skálu ani stěnu jeskyně. Dveře nepřekáží
/// (projde se jimi). Hlídá útok hráče i zvířat a braní věcí, ať se nedá zabíjet ani brát přes zeď.
/// </summary>
public static class Sight
{
    // Po jakých kouscích se úsečka prochází (metry). Zeď je tlustší, takže se nepřeskočí.
    private const double Step = 0.05;

    public static bool Clear(Island island, double x1, double y1, double x2, double y2, int level = 0)
    {
        var length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        var steps = (int)Math.Ceiling(length / Step);

        // Konce úsečky se nepočítají: kdo stojí těsně u zdi, není v ní.
        for (var i = 1; i < steps; i++)
        {
            var t = (double)i / steps;
            var (x, y) = (x1 + (x2 - x1) * t, y1 + (y2 - y1) * t);
            if (Blocked(island, x, y, level))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Blocked(Island island, double x, double y, int level)
    {
        var (tx, ty) = ((int)Math.Floor(x), (int)Math.Floor(y));
        if (!island.InBounds(tx, ty))
        {
            return true;
        }

        if (level == 0 && island.TileAt(tx, ty) is TileType.Rock or TileType.LayeredRock or TileType.CaveWall)
        {
            return true;
        }

        return island.Structures.Blocks(x, y, 0.01, level) || (level == 0 && island.Forestry.Blocks(x, y, 0.01));
    }
}

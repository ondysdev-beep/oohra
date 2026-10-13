using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>
/// Úkryt (prototyp sheltered v part4.js): postava je v prostoru uzavřeném zdmi, který má nejvýš
/// maxTiles dlaždic. Počítá se krokem nahoru, dolů, doleva a doprava. Stromy a voda se jako stěna
/// nepočítají, jen zdi. Kdo dojde na okraj mapy, v úkrytu není.
/// </summary>
public static class Shelter
{
    private static readonly (int Dx, int Dy)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static bool IsSheltered(Island island, double x, double y)
    {
        var maxTiles = GameData.Building.Shelter.MaxTiles;
        var start = new GridPoint((int)Math.Floor(x), (int)Math.Floor(y));
        if (!island.InBounds(start.X, start.Y))
        {
            return false;
        }

        var seen = new HashSet<int> { island.Index(start.X, start.Y) };
        var stack = new Stack<GridPoint>();
        stack.Push(start);
        var visited = 0;
        while (stack.Count > 0)
        {
            var tile = stack.Pop();
            if (++visited > maxTiles)
            {
                return false;
            }

            foreach (var (dx, dy) in Neighbours)
            {
                var nx = tile.X + dx;
                var ny = tile.Y + dy;
                if (!island.InBounds(nx, ny))
                {
                    return false;
                }

                var index = island.Index(nx, ny);
                if (!island.Walls.Has(index) && seen.Add(index))
                {
                    stack.Push(new GridPoint(nx, ny));
                }
            }
        }

        return true;
    }
}

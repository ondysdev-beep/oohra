using Oohra.Core.Data;
using Oohra.Core.Rng;

namespace Oohra.Core.World;

/// <summary>Kde se postava narodí (prototyp pickWildSpawn v part4.js).</summary>
public static class SpawnPoint
{
    /// <summary>
    /// Náhodné místo v divočině: nejdřív na písku, později i na trávě, daleko od táborů.
    /// V prvním životě navíc blízko jezírka, aby byla voda na dosah. Po smrti dál od místa smrti (avoid).
    /// </summary>
    public static Position PickWild(Island island, Mulberry32 rng, bool firstLife, Position? avoid = null)
    {
        var rules = GameData.Movement.Spawn;
        for (var n = 0; n < rules.Attempts; n++)
        {
            var x = rng.NextInt(island.Width);
            var y = rng.NextInt(island.SurfaceHeight);
            var tile = island.TileAt(x, y);
            var tileOk = n < rules.SandOnlyAttempts
                ? tile == TileType.Sand
                : tile == TileType.Sand || tile == TileType.Grass;
            if (!tileOk || !Movement.IsWalkable(island, x + 0.5, y + 0.5, rules.Clearance))
            {
                continue;
            }

            if (firstLife && n < rules.NearPondAttempts && !island.PondNear(x, y, rules.PondRadius))
            {
                continue;
            }

            if (!AwayFromCamps(island, x + 0.5, y + 0.5, rules.CampDistance))
            {
                continue;
            }

            if (avoid is { } a && n < rules.AvoidAttempts && Math.Sqrt(Math.Pow(x - a.X, 2) + Math.Pow(y - a.Y, 2)) < rules.AvoidDistance)
            {
                continue;
            }

            if (OpenTiles(island, x, y, rules.MinOpenTiles) < rules.MinOpenTiles)
            {
                continue;
            }

            return new Position(x + 0.5, y + 0.5);
        }

        return new Position(island.Width / 2.0, island.SurfaceHeight / 2.0);
    }

    /// <summary>
    /// Místo u kmene (návrh 4.1): okraj území kmene, mimo domy. home jsou místa kmene (stavby členů, když žádné
    /// nejsou, živí členové). Postava se narodí minDistance až maxDistance od některého z nich, aspoň minDistance
    /// od každé stavby a tam, odkud se dá odejít. Když se místo nenajde (nebo kmen žádné místo nemá), vrátí null.
    /// </summary>
    public static Position? PickNearTribe(Island island, Mulberry32 rng, IReadOnlyList<Position> home)
    {
        var rules = GameData.Tribes.Birth;
        var spawn = GameData.Movement.Spawn;
        for (var n = 0; n < rules.Attempts && home.Count > 0; n++)
        {
            var anchor = home[rng.NextInt(home.Count)];
            var angle = rng.NextDouble() * 2 * Math.PI;
            var distance = rules.MinDistance + rng.NextDouble() * (rules.MaxDistance - rules.MinDistance);
            var x = anchor.X + Math.Cos(angle) * distance;
            var y = anchor.Y + Math.Sin(angle) * distance;
            if (!island.InBounds((int)Math.Floor(x), (int)Math.Floor(y))
                || !Movement.IsWalkable(island, x, y, spawn.Clearance)
                || island.Structures.Near(x, y, rules.MinDistance).Any()
                || OpenTiles(island, (int)Math.Floor(x), (int)Math.Floor(y), spawn.MinOpenTiles) < spawn.MinOpenTiles)
            {
                continue;
            }

            return new Position(x, y);
        }

        return null;
    }

    /// <summary>
    /// Na kolik volných dlaždic se dá z dlaždice (x, y) dojít krokem nahoru, dolů, doleva a doprava.
    /// Počítá jen do limitu (víc není potřeba vědět).
    /// </summary>
    public static int OpenTiles(Island island, int x, int y, int limit)
    {
        var seen = new HashSet<int> { island.Index(x, y) };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(new GridPoint(x, y));
        while (queue.Count > 0 && seen.Count < limit)
        {
            var tile = queue.Dequeue();
            foreach (var (dx, dy) in Neighbours)
            {
                var nx = tile.X + dx;
                var ny = tile.Y + dy;
                if (!Movement.IsBlocked(island, nx, ny) && seen.Add(island.Index(nx, ny)))
                {
                    queue.Enqueue(new GridPoint(nx, ny));
                }
            }
        }

        return seen.Count;
    }

    private static readonly (int Dx, int Dy)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private static bool AwayFromCamps(Island island, double x, double y, double distance)
    {
        foreach (var camp in island.Camps)
        {
            var dx = x - camp.X;
            var dy = y - camp.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= distance)
            {
                return false;
            }
        }

        return true;
    }
}

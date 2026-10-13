using Oohra.Core.Data;
using Oohra.Core.Rng;

namespace Oohra.Core.World;

/// <summary>Kde se postava narodí (prototyp pickWildSpawn v part4.js).</summary>
public static class SpawnPoint
{
    /// <summary>
    /// Náhodné místo v divočině: nejdřív na písku, později i na trávě, daleko od táborů.
    /// V prvním životě navíc blízko jezírka, aby byla voda na dosah.
    /// </summary>
    public static Position PickWild(Island island, Mulberry32 rng, bool firstLife)
    {
        var rules = GameData.Movement.Spawn;
        for (var n = 0; n < rules.Attempts; n++)
        {
            var x = rng.NextInt(island.Width);
            var y = rng.NextInt(island.Height);
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

            return new Position(x + 0.5, y + 0.5);
        }

        return new Position(island.Width / 2.0, island.Height / 2.0);
    }

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

using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>Kontroly místa pro stavby (zeď, oheň): suchá zem, kmeny stromů, vzdálenost od postavy.</summary>
public static class Placement
{
    /// <summary>Krok, po kterém se kontroluje zem pod zdí (v metrech).</summary>
    private const double SampleStep = 0.1;

    /// <summary>Střed stavby je na dosah postavy (placeReach v building.json).</summary>
    public static bool InReach(double playerX, double playerY, double x, double y)
    {
        var dx = x - playerX;
        var dy = y - playerY;
        return Math.Sqrt(dx * dx + dy * dy) <= GameData.Building.PlaceReach;
    }

    /// <summary>Bod je na mapě, ne ve vodě (moře, jezírko) a ne ve skále kolem jeskyně.</summary>
    public static bool OnDryLand(Island island, double x, double y)
    {
        var tx = (int)Math.Floor(x);
        var ty = (int)Math.Floor(y);
        if (!island.InBounds(tx, ty))
        {
            return false;
        }

        var tile = island.TileAt(tx, ty);
        return tile is not (TileType.Sea or TileType.Pond or TileType.Ford or TileType.CaveWall or TileType.Mire or TileType.Shallows);
    }

    /// <summary>Celá úsečka leží na suché zemi.</summary>
    public static bool SegmentOnDryLand(Island island, double x1, double y1, double x2, double y2)
    {
        var length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        var steps = Math.Max(1, (int)Math.Ceiling(length / SampleStep));
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            if (!OnDryLand(island, x1 + (x2 - x1) * t, y1 + (y2 - y1) * t))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Úsečka (nebo bod, když jsou konce stejné) je dál než clearance od každého kmene stromu (i pařezu),
    /// ležícího kmene a hromady kamenů.
    /// </summary>
    public static bool ClearOfTrunks(Island island, double x1, double y1, double x2, double y2, double clearance)
    {
        if (island.Forestry.BlocksSegment(x1, y1, x2, y2, clearance))
        {
            return false;
        }

        var reach = clearance + GameData.Movement.TreeTrunkRadius;
        var rockReach = clearance + GameData.Rocks.Radius;
        var minX = (int)Math.Floor(Math.Min(x1, x2) - reach) - 1;
        var maxX = (int)Math.Floor(Math.Max(x1, x2) + reach) + 1;
        var minY = (int)Math.Floor(Math.Min(y1, y2) - reach) - 1;
        var maxY = (int)Math.Floor(Math.Max(y1, y2) + reach) + 1;
        for (var ty = minY; ty <= maxY; ty++)
        {
            for (var tx = minX; tx <= maxX; tx++)
            {
                if (island.InBounds(tx, ty) && island.HasTrunk(island.Index(tx, ty))
                    && island.TrunkPosition(island.Index(tx, ty)) is var (trunkX, trunkY)
                    && Geometry.DistanceToSegment(trunkX, trunkY, x1, y1, x2, y2) < reach)
                {
                    return false;
                }

                // Hromada kamenů (dokud v ní něco je) překáží stejně jako kmen.
                if (island.InBounds(tx, ty) && island.Rocks.At(island.Index(tx, ty)) is { Stones: > 0 }
                    && Geometry.DistanceToSegment(tx + 0.5, ty + 0.5, x1, y1, x2, y2) < rockReach)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

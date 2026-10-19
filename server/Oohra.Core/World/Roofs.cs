using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>
/// Střechy a jejich statika, první verze (návrh 0.8, kap. 6, building.json roof): střecha je čtverec size × size
/// ve výšce zdí. Drží, když se opírá aspoň o minSupports podpor: zeď nebo zeď s otvorem (aspoň minWallSupport
/// metrů zdi pod okrajem nebo pod střechou), sloupek pod ní, nebo sousední střecha, která sama drží. Střecha
/// bez podpory zapraská a za warnSeconds spadne (Structures.Update). Před deštěm chrání, kdo stojí pod
/// střechou, která drží.
/// </summary>
public static class Roofs
{
    /// <summary>Bod (x, y) je pod střechou (čtverec).</summary>
    public static bool Covers(Structure roof, double x, double y, double margin = 0)
    {
        var half = GameData.Building.Roof.Size / 2 + margin;
        var (dx, dy) = (x - roof.X, y - roof.Y);
        var (c, s) = (Math.Cos(roof.Angle), Math.Sin(roof.Angle));
        var along = dx * c + dy * s;
        var across = -dx * s + dy * c;
        return Math.Abs(along) <= half && Math.Abs(across) <= half;
    }

    /// <summary>Kdo stojí na (x, y), má nad hlavou střechu, která drží (nepadá).</summary>
    public static bool IsRoofed(Structures structures, double x, double y)
    {
        return structures.All.Any(s => s.Kind == BuildKind.Roof && s.CrackingSince is null && Covers(s, x, y));
    }

    /// <summary>Kolik zdí a sloupků střechu přímo podpírá.</summary>
    public static int DirectSupports(Structures structures, double x, double y, double angle)
    {
        var rules = GameData.Building.Roof;
        var probe = new Structure(0, BuildKind.Roof, x, y, angle, rules.Size);
        var count = 0;
        foreach (var s in structures.All)
        {
            if (s.Kind == BuildKind.Post && Covers(probe, s.X, s.Y, rules.SupportTolerance))
            {
                count++;
            }
            else if (s.Kind is BuildKind.StickWall or BuildKind.DoorFrame && WallUnder(probe, s, rules) >= rules.MinWallSupport)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Které střechy drží: nejdřív ty, které mají dost přímých podpor, pak opakovaně ty, kterým podpory doplní
    /// sousední střechy, které už drží (podpůrný graf). Extra = střecha navíc (zkouška před postavením).
    /// </summary>
    public static HashSet<int> Stable(Structures structures, Structure? extra = null)
    {
        var rules = GameData.Building.Roof;
        var roofs = structures.All.Where(s => s.Kind == BuildKind.Roof).ToList();
        if (extra is not null)
        {
            roofs.Add(extra);
        }

        var direct = roofs.ToDictionary(r => r.Id, r => DirectSupports(structures, r.X, r.Y, r.Angle));
        var stable = new HashSet<int>();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var roof in roofs.Where(r => !stable.Contains(r.Id)))
            {
                var neighbours = roofs.Count(o => o.Id != roof.Id && stable.Contains(o.Id) && Neighbours(roof, o, rules));
                if (direct[roof.Id] + neighbours >= rules.MinSupports)
                {
                    stable.Add(roof.Id);
                    changed = true;
                }
            }
        }

        return stable;
    }

    /// <summary>Kolik metrů zdi leží pod střechou (i kousek za okrajem, supportTolerance).</summary>
    private static double WallUnder(Structure roof, Structure wall, RoofRules rules)
    {
        var (x1, y1, x2, y2) = wall.Ends;
        var steps = Math.Max(1, (int)Math.Ceiling(wall.Length / 0.1));
        var inside = 0;
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            if (Covers(roof, x1 + (x2 - x1) * t, y1 + (y2 - y1) * t, rules.SupportTolerance))
            {
                inside++;
            }
        }

        return wall.Length * inside / (steps + 1);
    }

    private static bool Neighbours(Structure a, Structure b, RoofRules rules)
    {
        return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) <= rules.NeighbourDistance;
    }
}

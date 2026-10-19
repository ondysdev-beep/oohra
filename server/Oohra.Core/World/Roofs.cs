using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>
/// Desky (střechy a podlahy pater) a statika, podpůrný graf (návrh 0.8, kap. 6, building.json roof): deska je
/// čtverec Length × Length. Výška desky: střecha v podlaží L leží ve výšce L + 1, podlaha patra L ve výšce L
/// (podlaha přízemí leží na zemi a drží vždy). Deska drží, když se opírá aspoň o minSupports podpor: zeď nebo zeď
/// s otvorem o podlaží níž (aspoň minWallSupport metrů pod ní nebo pod okrajem), sloupek pod ní, nebo sousední
/// deska ve stejné výšce, která sama drží. Stavby v patře drží, když stojí na podlaze patra, která drží. Co nedrží,
/// zapraská a za warnSeconds spadne (Structures.Update).
/// </summary>
public static class Roofs
{
    /// <summary>Nejvyšší podlaží (0 přízemí, 1 patro). Nad patrem už je jen střecha.</summary>
    public const int TopLevel = 1;

    /// <summary>Deska (střecha nebo podlaha patra), která potřebuje podporu.</summary>
    public static bool IsDeck(Structure s)
    {
        return s.Kind == BuildKind.Roof || (s.Kind == BuildKind.Floor && s.Level > 0);
    }

    /// <summary>Výška desky v podlažích (střecha přízemí a podlaha patra jsou obě ve výšce 1).</summary>
    public static int DeckHeight(Structure s)
    {
        return s.Kind == BuildKind.Roof ? s.Level + 1 : s.Level;
    }

    /// <summary>Bod (x, y) je nad nebo pod čtvercem (střecha, podlaha).</summary>
    public static bool Covers(Structure square, double x, double y, double margin = 0)
    {
        var half = square.Length / 2 + margin;
        var (dx, dy) = (x - square.X, y - square.Y);
        var (c, s) = (Math.Cos(square.Angle), Math.Sin(square.Angle));
        var along = dx * c + dy * s;
        var across = -dx * s + dy * c;
        return Math.Abs(along) <= half && Math.Abs(across) <= half;
    }

    /// <summary>Kdo stojí na (x, y) v podlaží level, má nad hlavou desku, která drží (nepadá): chrání před deštěm.</summary>
    public static bool IsRoofed(Structures structures, double x, double y, int level = 0, double now = 0)
    {
        return structures.Near(x, y, 0.5).Any(s => IsDeck(s) && DeckHeight(s) > level && s.CrackingSince is null && s.BurningSince is null
            && !Structures.Leaking(s, now) && Covers(s, x, y));
    }

    /// <summary>V podlaží level (patro) je na (x, y) podlaha, na které se dá stát.</summary>
    public static bool StandsOn(Structures structures, double x, double y, int level, double margin = 0)
    {
        return level > 0 && structures.Near(x, y, 0.5).Any(s => s.Kind == BuildKind.Floor && s.Level == level && Covers(s, x, y, margin));
    }

    /// <summary>Kolik stojících zdí a sloupků (o podlaží níž) desku ve výšce height přímo podpírá.</summary>
    public static int DirectSupports(IEnumerable<Structure> standing, Structure deck)
    {
        var rules = GameData.Building.Roof;
        var below = DeckHeight(deck) - 1;
        var count = 0;
        foreach (var s in standing)
        {
            if (s.Level != below)
            {
                continue;
            }

            if (s.Kind == BuildKind.Post && Covers(deck, s.X, s.Y, rules.SupportTolerance))
            {
                count++;
            }
            else if (s.Kind is BuildKind.StickWall or BuildKind.DoorFrame && WallUnder(deck, s, rules) >= rules.MinWallSupport)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Co drží (čísla staveb): všechno v přízemí kromě střech, pak po výškách desky (přímé podpory a sousední desky,
    /// které už drží, opakovaně) a stavby v patře na podlahách, které drží. Extra = stavba navíc (zkouška před postavením).
    /// </summary>
    public static HashSet<int> Stable(Structures structures, Structure? extra = null)
    {
        var rules = GameData.Building.Roof;
        var all = structures.All.ToList();
        if (extra is not null)
        {
            all.Add(extra);
        }

        var stable = all.Where(s => s.Level == 0 && !IsDeck(s)).Select(s => s.Id).ToHashSet();
        for (var height = 1; height <= TopLevel + 1; height++)
        {
            var standing = all.Where(s => stable.Contains(s.Id)).ToList();
            var decks = all.Where(s => IsDeck(s) && DeckHeight(s) == height).ToList();
            var direct = decks.ToDictionary(d => d.Id, d => DirectSupports(standing, d));
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var deck in decks.Where(d => !stable.Contains(d.Id)))
                {
                    var neighbours = decks.Count(o => o.Id != deck.Id && stable.Contains(o.Id) && Neighbours(deck, o, rules));
                    if (direct[deck.Id] + neighbours >= rules.MinSupports)
                    {
                        stable.Add(deck.Id);
                        changed = true;
                    }
                }
            }

            // Stavby v patře stojí na podlaze patra, která drží.
            var floors = all.Where(s => s.Kind == BuildKind.Floor && s.Level == height && stable.Contains(s.Id)).ToList();
            foreach (var s in all.Where(s => s.Level == height && !IsDeck(s) && s.Kind != BuildKind.Floor))
            {
                if (Footprint(s).All(p => floors.Any(f => Covers(f, p.X, p.Y, 0.1))))
                {
                    stable.Add(s.Id);
                }
            }
        }

        return stable;
    }

    /// <summary>Body, které musí stát na podlaze: u zdi konce a střed, jinak střed.</summary>
    public static IEnumerable<Position> Footprint(Structure s)
    {
        if (s.Kind is BuildKind.StickWall or BuildKind.DoorFrame or BuildKind.Door)
        {
            var (x1, y1, x2, y2) = s.Ends;
            return [new Position(x1, y1), new Position(s.X, s.Y), new Position(x2, y2)];
        }

        return [new Position(s.X, s.Y)];
    }

    /// <summary>Kolik metrů zdi leží pod deskou (i kousek za okrajem, supportTolerance).</summary>
    private static double WallUnder(Structure deck, Structure wall, RoofRules rules)
    {
        var (x1, y1, x2, y2) = wall.Ends;
        var steps = Math.Max(1, (int)Math.Ceiling(wall.Length / 0.1));
        var inside = 0;
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            if (Covers(deck, x1 + (x2 - x1) * t, y1 + (y2 - y1) * t, rules.SupportTolerance))
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

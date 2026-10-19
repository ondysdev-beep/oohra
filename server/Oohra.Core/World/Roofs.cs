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
    /// Co drží (čísla staveb): podpůrný graf (StableCore) a nosnost. Co je přetížené (sloupek, zeď), neunese a
    /// počítá se znovu bez toho, dokud něco padá. Extra = stavba navíc (zkouška před postavením), people = lidé
    /// v patře (poloha a podlaží, váží). loads = false: jen podpůrný graf bez nosnosti.
    /// </summary>
    public static HashSet<int> Stable(Structures structures, Structure? extra = null, IReadOnlyList<(double X, double Y, int Level)>? people = null, bool loads = true)
    {
        var all = structures.All.ToList();
        if (extra is not null)
        {
            all.Add(extra);
        }

        var failed = new HashSet<int>();
        while (true)
        {
            var stable = StableCore(all, failed);
            if (!loads)
            {
                return stable;
            }

            var carried = Carried(all, stable, people ?? []);
            var over = all.Where(s => stable.Contains(s.Id) && Capacity(s) is { } capacity && carried[s.Id] > capacity + 1e-9).Select(s => s.Id).ToList();
            if (over.Count == 0)
            {
                return stable;
            }

            failed.UnionWith(over);
        }
    }

    /// <summary>Kolik stavba váží (zeď za metr, střecha podle materiálu).</summary>
    public static double Weight(Structure s)
    {
        var rules = GameData.Building.Load;
        if (s.Kind == BuildKind.Roof)
        {
            return rules.RoofWeights.GetValueOrDefault(s.Material, 1);
        }

        var weight = rules.Weights.GetValueOrDefault(s.Kind, 1);
        return s.Kind == BuildKind.StickWall ? weight * s.Length : weight;
    }

    /// <summary>Kolik unese (navíc ke své váze): sloupek a zdi. Ostatní nic nenese (null).</summary>
    public static double? Capacity(Structure s)
    {
        var rules = GameData.Building.Load;
        return s.Kind switch
        {
            BuildKind.Post => rules.PostCapacity,
            BuildKind.StickWall or BuildKind.DoorFrame => rules.WallCapacityPerMeter * s.Length,
            _ => null,
        };
    }

    /// <summary>
    /// Kolik která stavba nese shora (bez své váhy). Shora dolů: deska rozdělí svou váhu i vše, co na ní je, mezi
    /// podpory pod sebou (sloupky, zdi) a sousední desky, které stojí samy (kdo má málo vlastních podpor, opře se
    /// o sousedy). Stavby v patře a lidé v patře tlačí na podlahu patra pod sebou.
    /// </summary>
    public static Dictionary<int, double> Carried(IReadOnlyList<Structure> all, HashSet<int> stable, IReadOnlyList<(double X, double Y, int Level)> people)
    {
        var rules = GameData.Building.Roof;
        var standing = all.Where(s => stable.Contains(s.Id)).ToList();
        var total = standing.ToDictionary(s => s.Id, Weight);
        var carried = standing.ToDictionary(s => s.Id, _ => 0.0);

        void Pass(int from, IReadOnlyList<Structure> onto)
        {
            if (onto.Count == 0)
            {
                return;
            }

            var share = total[from] / onto.Count;
            foreach (var target in onto)
            {
                total[target.Id] += share;
                carried[target.Id] += share;
            }
        }

        foreach (var (x, y, level) in people.Where(p => p.Level > 0))
        {
            var floors = standing.Where(f => f.Kind == BuildKind.Floor && f.Level == level && Covers(f, x, y, 0.1)).ToList();
            foreach (var floor in floors)
            {
                total[floor.Id] += GameData.Building.Load.Person / floors.Count;
                carried[floor.Id] += GameData.Building.Load.Person / floors.Count;
            }
        }

        for (var height = TopLevel + 1; height >= 1; height--)
        {
            var decks = standing.Where(d => IsDeck(d) && DeckHeight(d) == height).ToList();
            var supports = decks.ToDictionary(d => d.Id, d => SupportsOf(standing, d));
            var grounded = decks.Where(d => supports[d.Id].Count >= rules.MinSupports).ToList();
            foreach (var deck in decks.OrderBy(d => supports[d.Id].Count))
            {
                var onto = supports[deck.Id].ToList();
                if (onto.Count < rules.MinSupports)
                {
                    onto.AddRange(grounded.Where(g => g.Id != deck.Id && Neighbours(deck, g, rules)));
                }

                Pass(deck.Id, onto);
            }

            // Stavby v patře (o podlaží níž než tyto desky) tlačí na podlahu patra, na které stojí.
            if (height - 1 >= 1)
            {
                var floors = standing.Where(f => f.Kind == BuildKind.Floor && f.Level == height - 1).ToList();
                foreach (var s in standing.Where(s => s.Level == height - 1 && !IsDeck(s)))
                {
                    Pass(s.Id, floors.Where(f => Covers(f, s.X, s.Y, 0.1)).ToList());
                }
            }
        }

        return carried;
    }

    /// <summary>Sloupky a zdi, které desku přímo podpírají (o podlaží níž).</summary>
    private static List<Structure> SupportsOf(IEnumerable<Structure> standing, Structure deck)
    {
        var rules = GameData.Building.Roof;
        var below = DeckHeight(deck) - 1;
        return standing.Where(s => s.Level == below
            && ((s.Kind == BuildKind.Post && Covers(deck, s.X, s.Y, rules.SupportTolerance))
                || (s.Kind is BuildKind.StickWall or BuildKind.DoorFrame && WallUnder(deck, s, rules) >= rules.MinWallSupport))).ToList();
    }

    /// <summary>Podpůrný graf bez nosnosti. Failed = přetížené stavby (nepočítají se jako stojící).</summary>
    private static HashSet<int> StableCore(List<Structure> all, HashSet<int> failed)
    {
        var rules = GameData.Building.Roof;
        var stable = all.Where(s => s.Level == 0 && !IsDeck(s) && !failed.Contains(s.Id)).Select(s => s.Id).ToHashSet();
        for (var height = 1; height <= TopLevel + 1; height++)
        {
            var standing = all.Where(s => stable.Contains(s.Id)).ToList();
            var decks = all.Where(s => IsDeck(s) && DeckHeight(s) == height).ToList();
            var direct = decks.ToDictionary(d => d.Id, d => DirectSupports(standing, d));
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var deck in decks.Where(d => !stable.Contains(d.Id) && !failed.Contains(d.Id)))
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
            foreach (var s in all.Where(s => s.Level == height && !IsDeck(s) && s.Kind != BuildKind.Floor && !failed.Contains(s.Id)))
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

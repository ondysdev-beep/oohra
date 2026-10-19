using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Rng;
using Oohra.Core.Social;

namespace Oohra.Core.World;

/// <summary>Jeskyně v podzemním pásu: velikost, obdélník na mapě a jestli v ní žije zvěř.</summary>
public sealed record Cave(int Id, CaveSize Size, int Left, int Top, int Width, int Height, IReadOnlyList<Position> Rooms)
{
    /// <summary>Zvěř, která v jeskyni žije (doupě), nebo null (prázdná jeskyně).</summary>
    public AnimalKind? Den { get; init; }

    /// <summary>Kolik zvířat v doupěti žije.</summary>
    public int DenCount { get; init; }
}

/// <summary>
/// Vchod do jeskyně: venku na ostrově (X, Y, u skály, Facing = směr ke skále) a uvnitř (InsideX, InsideY).
/// Nenápadný vchod (Hidden) vidí jen postava, která ho objevila.
/// </summary>
public sealed record CaveEntrance(int Id, int CaveId, double X, double Y, double Facing, double InsideX, double InsideY, bool Hidden);

/// <summary>
/// Jeskyně (návrh 0.8, kap. 4, caves.json): podzemní pás pod ostrovem ve stejné mapě, oddělený mořem. Každá
/// jeskyně má svůj čtverec: síně spojené chodbami, kaluže kapající vody a místa vrstvené skály (pazourek).
/// Vchody jsou ve skále na ostrově (u kamenité půdy vrchoviny), některé nenápadné, některé jeskyně mají druhý
/// východ. Všechno ze seedu: stejný svět = stejné jeskyně.
/// </summary>
public static class Caves
{
    private const int SeedSalt = 0xCA7E;
    private const int DenSalt = 0xDE4D;

    /// <summary>Ostrov s podzemím: řádky ostrova zůstanou (čísla dlaždic se nemění), pod ně se přidá pás jeskyní.</summary>
    public static Island Dig(Island surface)
    {
        var rules = GameData.Caves;
        var rng = new Mulberry32(surface.Seed ^ SeedSalt);
        var highland = surface.Regions.Count(r => r == RegionKind.Highland);
        var wanted = Math.Clamp((int)Math.Round((double)highland / rules.TilesPerCave), rules.MinCaves, rules.MaxCaves);
        var spots = EntranceSpots(surface, rng, wanted * 2);
        var count = Math.Min(wanted, spots.Count);
        if (count == 0)
        {
            return surface;
        }

        var (w, h) = (surface.Width, surface.Height);
        var perRow = Math.Max(1, w / rules.CellSize);
        var rows = (count + perRow - 1) / perRow;
        var height = h + rules.Gap + rows * rules.CellSize;
        var tiles = new TileType[w * height];
        var regions = new RegionKind[w * height];
        var shore = new bool[w * height];
        Array.Copy(surface.Tiles, tiles, surface.Tiles.Length);
        Array.Copy(surface.Regions, regions, surface.Regions.Length);
        Array.Copy(surface.Shore, shore, surface.Shore.Length);
        for (var i = surface.Tiles.Length; i < tiles.Length; i++)
        {
            var underground = i / w >= h + rules.Gap;
            tiles[i] = underground ? TileType.CaveWall : TileType.Sea;
            regions[i] = underground ? RegionKind.Cave : RegionKind.Sea;
        }

        var caves = new List<Cave>();
        var entrances = new List<CaveEntrance>();
        var spare = new Queue<(int X, int Y, double Facing)>(spots.Skip(count));
        for (var k = 0; k < count; k++)
        {
            var left = k % perRow * rules.CellSize;
            var top = h + rules.Gap + k / perRow * rules.CellSize;
            var cave = WithDen(DigCave(k + 1, PickSize(rng, rules), left, top, tiles, w, rng, rules), surface.Seed, rules);
            caves.Add(cave);
            AddEntrance(entrances, cave, spots[k], cave.Rooms[0], rng, rules);
            if (cave.Rooms.Count > 1 && spare.Count > 0 && rng.NextDouble() < rules.SecondExitChance)
            {
                AddEntrance(entrances, cave, spare.Dequeue(), cave.Rooms[^1], rng, rules);
            }
        }

        EnsureDens(caves, rules);
        return new Island(surface.Seed, w, height, tiles, shore, surface.Camps, surface.RuinSpots, surface.Trees, surface.Plants,
            surface.Species, surface.SpeciesSecrets, regions, h, caves, entrances);
    }

    /// <summary>Jeskyně má druhý východ (táhne v ní, kouř nevadí).</summary>
    public static bool Ventilated(Island island, Cave cave)
    {
        return island.CaveEntrances.Count(e => e.CaveId == cave.Id) >= 2;
    }

    /// <summary>
    /// Kouř (caves.json smoke): v jeskyni bez druhého východu s hořícím ohněm se postava dusí (zdraví, síla).
    /// </summary>
    public static void Smoke(Island island, Fires fires, Character c, double x, double y, double minutes, double now)
    {
        var cave = At(island, x, y);
        if (c.IsDead || cave is null || Ventilated(island, cave) || !fires.All.Any(f => f.Burning && At(island, f.X, f.Y) == cave))
        {
            return;
        }

        var rules = GameData.Caves.Smoke;
        c.Energy = Math.Max(0, c.Energy - rules.EnergyPerMinute * minutes);
        c.TakeDamage(rules.HealthPerMinute * minutes, DeathCause.Smoke);
        if (now - c.LastSmokeWarning >= rules.WarnEveryMinutes)
        {
            c.LastSmokeWarning = now;
            c.Report(now, BodyEvent.Smoke);
        }
    }

    /// <summary>Zátaras u vchodu (venku), nebo null.</summary>
    public static Structure? BarricadeAt(Island island, CaveEntrance entrance)
    {
        return island.Structures.All.FirstOrDefault(s => s.Kind == BuildKind.Barricade
            && Distance(s.X, s.Y, entrance.X, entrance.Y) <= GameData.Building.Barricade.EntranceReach);
    }

    /// <summary>Kde zátaras stojí: na hraně skály u vchodu, natočený podle skály.</summary>
    public static (double X, double Y, double Angle) BarricadeSpot(CaveEntrance entrance)
    {
        return (entrance.X + Math.Cos(entrance.Facing) * 0.45, entrance.Y + Math.Sin(entrance.Facing) * 0.45, entrance.Facing + Math.PI / 2);
    }

    /// <summary>Jeskyně, ve které je místo (x, y), nebo null (venku).</summary>
    public static Cave? At(Island island, double x, double y)
    {
        var (tx, ty) = ((int)Math.Floor(x), (int)Math.Floor(y));
        return island.Caves.FirstOrDefault(c => tx >= c.Left && tx < c.Left + c.Width && ty >= c.Top && ty < c.Top + c.Height);
    }

    /// <summary>Vchody, které postava vidí: všechny nenápadné, jen když je objevila.</summary>
    public static IEnumerable<CaveEntrance> Visible(Island island, Character c)
    {
        return island.CaveEntrances.Where(e => !e.Hidden || c.KnowsCave(e.Id));
    }

    /// <summary>Nenápadné vchody blíž než discoverRange si postava zapamatuje. Vrací true, když nějaký objevila.</summary>
    public static bool Discover(Island island, Character c, double x, double y, double now)
    {
        var range = GameData.Caves.DiscoverRange;
        var found = false;
        foreach (var e in island.CaveEntrances)
        {
            if (e.Hidden && !c.KnowsCave(e.Id) && Distance(e.X, e.Y, x, y) <= range)
            {
                c.LearnCave(e.Id);
                c.Report(now, BodyEvent.FoundCave);
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Projít vchodem: kdo stojí u vchodu venku, vejde dovnitř, kdo stojí u východu uvnitř, vyjde ven.
    /// Vrací kam (nová poloha), když to šlo.
    /// </summary>
    public static (CaveResult Result, Position To) Pass(Island island, Character c, int entranceId, double x, double y, int playerId = -1, Tribes? tribes = null)
    {
        var entrance = island.CaveEntrances.FirstOrDefault(e => e.Id == entranceId);
        if (entrance is null || (entrance.Hidden && !c.KnowsCave(entrance.Id) && Distance(entrance.X, entrance.Y, x, y) > GameData.Caves.DiscoverRange))
        {
            return (CaveResult.Unknown, new Position(x, y));
        }

        var reach = GameData.Caves.UseReach;
        if (Distance(entrance.X, entrance.Y, x, y) <= reach)
        {
            // Zátaras pustí dovnitř jen majitele a jeho kmen (ven smí každý, ať se nikdo nezavře).
            if (BarricadeAt(island, entrance) is { } barricade && !Structures.MayTouch(barricade, playerId, tribes))
            {
                return (CaveResult.Barricaded, new Position(x, y));
            }

            return (CaveResult.Entered, new Position(entrance.InsideX, entrance.InsideY));
        }

        if (Distance(entrance.InsideX, entrance.InsideY, x, y) <= reach)
        {
            return (CaveResult.Left, new Position(entrance.X, entrance.Y));
        }

        return (CaveResult.TooFar, new Position(x, y));
    }

    // ---------- vznik ----------

    /// <summary>
    /// Místa pro vchody: chodná souš vedle skály, mimo stromy a tábory, od sebe aspoň entranceSpacing.
    /// Nejdřív vrchovina, pak ostatní.
    /// </summary>
    private static List<(int X, int Y, double Facing)> EntranceSpots(Island island, Mulberry32 rng, int max)
    {
        var rules = GameData.Caves;
        var candidates = new List<(int X, int Y, double Facing)>();
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            if (island.Tiles[i] is not (TileType.Highland or TileType.Grass or TileType.Forest) || island.HasTree(i))
            {
                continue;
            }

            var (x, y) = (i % island.Width, i / island.Width);
            var facing = RockSide(island, x, y);
            if (facing is { } angle && island.Camps.All(c => Distance(c.X, c.Y, x, y) >= rules.CampDistance))
            {
                candidates.Add((x, y, angle));
            }
        }

        var ordered = rng.Shuffle(candidates).OrderByDescending(c => island.Regions[island.Index(c.X, c.Y)] == RegionKind.Highland);
        var picked = new List<(int X, int Y, double Facing)>();
        foreach (var c in ordered)
        {
            if (picked.Count >= max)
            {
                break;
            }

            if (picked.All(p => Distance(p.X, p.Y, c.X, c.Y) >= rules.EntranceSpacing))
            {
                picked.Add(c);
            }
        }

        return picked;
    }

    /// <summary>Směr ke skále vedle dlaždice (sousední skála bez hromady kamenů), nebo null.</summary>
    private static double? RockSide(Island island, int x, int y)
    {
        foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
        {
            var (nx, ny) = (x + dx, y + dy);
            if (island.InBounds(nx, ny) && island.TileAt(nx, ny) is TileType.Rock or TileType.LayeredRock
                && island.Rocks.All.All(p => p.TileIndex != island.Index(nx, ny)))
            {
                return Math.Atan2(dy, dx);
            }
        }

        return null;
    }

    /// <summary>Doupě: s šancí occupiedChance v jeskyni žije zvěř podle velikosti (vlastní náhoda, tvar jeskyně se nemění).</summary>
    private static Cave WithDen(Cave cave, int seed, CaveRules rules)
    {
        var rng = new Mulberry32(unchecked(seed ^ DenSalt ^ (cave.Id * 7919)));
        var den = rules.Dens.FirstOrDefault(d => d.Size == cave.Size);
        if (den is null || rng.NextDouble() >= rules.OccupiedChance)
        {
            return cave;
        }

        return cave with { Den = den.Kind, DenCount = den.Min + rng.NextInt(den.Max - den.Min + 1) };
    }

    /// <summary>Když je doupat méně než minDens, dostanou zvěř největší prázdné jeskyně (nejmenší počet zvířat).</summary>
    private static void EnsureDens(List<Cave> caves, CaveRules rules)
    {
        var missing = rules.MinDens - caves.Count(c => c.Den is not null);
        foreach (var cave in caves.Where(c => c.Den is null).OrderByDescending(c => c.Size).ThenBy(c => c.Id).Take(Math.Max(0, missing)).ToList())
        {
            var den = rules.Dens.Single(d => d.Size == cave.Size);
            caves[caves.IndexOf(cave)] = cave with { Den = den.Kind, DenCount = den.Min };
        }
    }

    private static CaveSizeRules PickSize(Mulberry32 rng, CaveRules rules)
    {
        var total = rules.Sizes.Sum(s => s.Weight);
        var roll = rng.NextDouble() * total;
        foreach (var size in rules.Sizes)
        {
            roll -= size.Weight;
            if (roll < 0)
            {
                return size;
            }
        }

        return rules.Sizes[^1];
    }

    /// <summary>Vyhloubí síně a chodby v jednom čtverci. První síň je u vchodu, poslední u druhého východu.</summary>
    private static Cave DigCave(int id, CaveSizeRules size, int left, int top, TileType[] tiles, int width, Mulberry32 rng, CaveRules rules)
    {
        var cell = rules.CellSize;
        var margin = 3 + size.RoomRadius[1];
        var rooms = new List<(Position Center, double Radius)>();
        var first = new Position(left + cell / 2.0 + (rng.NextDouble() * 2 - 1) * 4, top + cell / 2.0 + (rng.NextDouble() * 2 - 1) * 4);
        rooms.Add((Snap(first), Between(rng, size.RoomRadius)));
        for (var n = 0; rooms.Count < size.Rooms && n < 200; n++)
        {
            var from = rooms[rng.NextInt(rooms.Count)];
            var angle = rng.NextDouble() * Math.PI * 2;
            var step = Between(rng, size.RoomStep);
            var center = new Position(from.Center.X + Math.Cos(angle) * step, from.Center.Y + Math.Sin(angle) * step);
            if (center.X < left + margin || center.X > left + cell - margin || center.Y < top + margin || center.Y > top + cell - margin
                || rooms.Any(r => Distance(r.Center.X, r.Center.Y, center.X, center.Y) < step * 0.7))
            {
                continue;
            }

            rooms.Add((Snap(center), Between(rng, size.RoomRadius)));
            Corridor(tiles, width, from.Center, rooms[^1].Center, rules.CorridorRadius);
        }

        foreach (var (center, radius) in rooms)
        {
            Carve(tiles, width, center.X, center.Y, radius);
        }

        foreach (var (center, radius) in rooms)
        {
            if (radius >= 3 && rng.NextDouble() < rules.PoolChance)
            {
                Mark(tiles, width, center, radius - 1.5, rng, TileType.Pond);
            }

            if (rng.NextDouble() < rules.FlintChance)
            {
                Mark(tiles, width, center, radius - 1.2, rng, TileType.LayeredRock);
            }
        }

        return new Cave(id, size.Size, left, top, cell, cell, rooms.Select(r => r.Center).ToList());
    }

    private static void AddEntrance(List<CaveEntrance> entrances, Cave cave, (int X, int Y, double Facing) spot, Position inside, Mulberry32 rng, CaveRules rules)
    {
        var hidden = rng.NextDouble() < rules.HiddenChance;
        entrances.Add(new CaveEntrance(entrances.Count + 1, cave.Id, spot.X + 0.5, spot.Y + 0.5, spot.Facing, inside.X, inside.Y, hidden));
    }

    /// <summary>Kruh podlahy.</summary>
    private static void Carve(TileType[] tiles, int width, double cx, double cy, double radius)
    {
        var r = (int)Math.Ceiling(radius);
        for (var y = (int)cy - r; y <= (int)cy + r; y++)
        {
            for (var x = (int)cx - r; x <= (int)cx + r; x++)
            {
                if (Distance(x + 0.5, y + 0.5, cx, cy) <= radius && tiles[y * width + x] == TileType.CaveWall)
                {
                    tiles[y * width + x] = TileType.CaveFloor;
                }
            }
        }
    }

    private static void Corridor(TileType[] tiles, int width, Position a, Position b, double radius)
    {
        var length = Distance(a.X, a.Y, b.X, b.Y);
        for (var t = 0.0; t <= length; t += 0.5)
        {
            Carve(tiles, width, a.X + (b.X - a.X) * t / length, a.Y + (b.Y - a.Y) * t / length, radius);
        }
    }

    /// <summary>Jedna dlaždice v síni (kaluž, pazourek) kousek od středu, jen když je kolem podlaha (neucpe cestu).</summary>
    private static void Mark(TileType[] tiles, int width, Position center, double distance, Mulberry32 rng, TileType tile)
    {
        var angle = rng.NextDouble() * Math.PI * 2;
        var (x, y) = ((int)Math.Floor(center.X + Math.Cos(angle) * distance), (int)Math.Floor(center.Y + Math.Sin(angle) * distance));
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (tiles[(y + dy) * width + x + dx] != TileType.CaveFloor)
                {
                    return;
                }
            }
        }

        tiles[y * width + x] = tile;
    }

    private static Position Snap(Position p)
    {
        return new Position(Math.Floor(p.X) + 0.5, Math.Floor(p.Y) + 0.5);
    }

    private static double Between(Mulberry32 rng, double[] range)
    {
        return range[0] + rng.NextDouble() * (range[1] - range[0]);
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }
}

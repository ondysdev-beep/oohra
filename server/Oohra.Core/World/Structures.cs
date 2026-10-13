using Oohra.Core.Data;
using Oohra.Core.Items;

namespace Oohra.Core.World;

/// <summary>Úsečka v rovině mapy (část stavby, která blokuje nebo uzavírá úkryt).</summary>
public readonly record struct Segment(double X1, double Y1, double X2, double Y2);

/// <summary>
/// Stavba hráče s přesnou polohou (střed) a úhlem v radiánech (0 = na východ, roste po směru hodin).
/// Dveře si pamatují, ve které zdi s otvorem jsou (FrameId).
/// </summary>
public sealed class Structure
{
    public Structure(int id, BuildKind kind, double x, double y, double angle, int frameId = 0)
    {
        Id = id;
        Kind = kind;
        X = x;
        Y = y;
        Angle = angle;
        FrameId = frameId;
    }

    public int Id { get; }

    public BuildKind Kind { get; }

    public double X { get; }

    public double Y { get; }

    public double Angle { get; }

    /// <summary>Dveře: číslo zdi s otvorem, ve které jsou. Jinak 0.</summary>
    public int FrameId { get; }

    /// <summary>Konce celé stavby (zeď, zeď s otvorem i dveře leží na stejné úsečce dlouhé jako zeď).</summary>
    public (double X1, double Y1, double X2, double Y2) Ends
    {
        get { return WallEnds(X, Y, Angle); }
    }

    public static (double X1, double Y1, double X2, double Y2) WallEnds(double x, double y, double angle)
    {
        var half = GameData.Building.Wall.Length / 2;
        var dx = Math.Cos(angle) * half;
        var dy = Math.Sin(angle) * half;
        return (x - dx, y - dy, x + dx, y + dy);
    }

    /// <summary>Co blokuje pohyb: zeď celá, zeď s otvorem jen sloupky, dveře nic (otevřou se).</summary>
    public IEnumerable<Segment> CollisionSegments()
    {
        switch (Kind)
        {
            case BuildKind.StickWall:
                return [Full()];
            case BuildKind.DoorFrame:
                return Posts();
            default:
                return [];
        }
    }

    /// <summary>Co uzavírá úkryt: zeď celá, zeď s otvorem jen sloupky, dveře otvor.</summary>
    public IEnumerable<Segment> ShelterSegments()
    {
        switch (Kind)
        {
            case BuildKind.StickWall:
                return [Full()];
            case BuildKind.DoorFrame:
                return Posts();
            case BuildKind.Door:
                return [Opening()];
            default:
                return [];
        }
    }

    private Segment Full()
    {
        var (x1, y1, x2, y2) = Ends;
        return new Segment(x1, y1, x2, y2);
    }

    /// <summary>Dva sloupky zdi s otvorem (po krajích, mezi nimi otvor).</summary>
    private Segment[] Posts()
    {
        var (x1, y1, x2, y2) = Ends;
        var post = (GameData.Building.Wall.Length - GameData.Building.DoorFrame.OpeningWidth) / 2;
        var dx = Math.Cos(Angle) * post;
        var dy = Math.Sin(Angle) * post;
        return [new Segment(x1, y1, x1 + dx, y1 + dy), new Segment(x2 - dx, y2 - dy, x2, y2)];
    }

    /// <summary>Otvor ve zdi (kde jsou dveře).</summary>
    private Segment Opening()
    {
        var half = GameData.Building.DoorFrame.OpeningWidth / 2;
        var dx = Math.Cos(Angle) * half;
        var dy = Math.Sin(Angle) * half;
        return new Segment(X - dx, Y - dy, X + dx, Y + dy);
    }
}

/// <summary>
/// Stavby hráčů: zdi, zdi s otvorem a dveře. Na rozdíl od prototypu nejsou vázané na dlaždice: mají libovolnou
/// polohu a úhel a blokují jen tam, kde opravdu stojí. Pro rychlé hledání si každou stavbu pamatuje
/// u dlaždic, přes které vede.
/// </summary>
public sealed class Structures
{
    private readonly Dictionary<int, Structure> _byId = [];
    private readonly Dictionary<(int X, int Y), List<Structure>> _byTile = [];
    private int _nextId = 1;

    public IReadOnlyCollection<Structure> All => _byId.Values;

    /// <summary>Zvýší se při každé změně. Server pak pošle seznam staveb.</summary>
    public int Version { get; private set; }

    public Structure? Find(int id)
    {
        return _byId.TryGetValue(id, out var structure) ? structure : null;
    }

    /// <summary>Dveře v dané zdi s otvorem, nebo null.</summary>
    public Structure? DoorIn(int frameId)
    {
        return _byId.Values.FirstOrDefault(s => s.Kind == BuildKind.Door && s.FrameId == frameId);
    }

    /// <summary>Postaví zeď bez materiálu a kontrol (pro testy).</summary>
    public Structure Place(double x, double y, double angle)
    {
        return Place(BuildKind.StickWall, x, y, angle);
    }

    /// <summary>Postaví stavbu bez materiálu a kontrol (pro testy a později pro načtení uloženého světa).</summary>
    public Structure Place(BuildKind kind, double x, double y, double angle, int frameId = 0)
    {
        var structure = new Structure(_nextId++, kind, x, y, angle, frameId);
        _byId[structure.Id] = structure;
        foreach (var tile in TilesOf(structure))
        {
            if (!_byTile.TryGetValue(tile, out var list))
            {
                list = [];
                _byTile[tile] = list;
            }

            list.Add(structure);
        }

        Version++;
        return structure;
    }

    /// <summary>
    /// Postaví stavbu se středem (x, y) a úhlem angle, nejdál placeReach od postavy.
    /// Zeď a zeď s otvorem: jen na suché zemi, ne přes kmen stromu, oheň ani hráče (players: polohy všech hráčů).
    /// Dveře: vsadí se do nejbližší volné zdi s otvorem (jejich poloha a úhel se převezmou od ní).
    /// </summary>
    public BuildResult Build(
        Island island, Fires fires, Inventory inventory, double playerX, double playerY,
        BuildKind kind, double x, double y, double angle, IEnumerable<Position> players)
    {
        if (inventory.Count(ItemKind.Stick) < SticksCost(kind))
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        if (kind == BuildKind.Door)
        {
            var frame = FreeDoorway(x, y);
            if (frame is null)
            {
                return BuildResult.NoDoorway;
            }

            inventory.TryRemove(ItemKind.Stick, SticksCost(kind));
            Place(BuildKind.Door, frame.X, frame.Y, frame.Angle, frame.Id);
            return BuildResult.Built;
        }

        if (kind is not (BuildKind.StickWall or BuildKind.DoorFrame) || !WallFits(island, fires, x, y, angle, players))
        {
            return BuildResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, SticksCost(kind));
        Place(kind, x, y, angle);
        return BuildResult.Built;
    }

    /// <summary>Rozebere stavbu (zeď s otvorem i s dveřmi). Část klacků se vrátí do batohu (když se tam vejdou).</summary>
    public BuildResult Remove(int id, Inventory inventory, double playerX, double playerY)
    {
        var structure = Find(id);
        if (structure is null)
        {
            return BuildResult.NotFound;
        }

        var (x1, y1, x2, y2) = structure.Ends;
        if (Geometry.DistanceToSegment(playerX, playerY, x1, y1, x2, y2) > GameData.Building.Wall.RemoveReach)
        {
            return BuildResult.TooFar;
        }

        if (structure.Kind == BuildKind.DoorFrame && DoorIn(structure.Id) is { } door)
        {
            Take(door, inventory);
        }

        Take(structure, inventory);
        return BuildResult.Removed;
    }

    /// <summary>Kruh (postava, oheň) o daném poloměru se překrývá s něčím, co blokuje pohyb.</summary>
    public bool Blocks(double x, double y, double radius)
    {
        var reach = radius + GameData.Building.Wall.Thickness / 2;
        foreach (var structure in Near(x, y, reach))
        {
            foreach (var s in structure.CollisionSegments())
            {
                if (Geometry.DistanceToSegment(x, y, s.X1, s.Y1, s.X2, s.Y2) < reach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Stavby, které vedou přes dlaždice do vzdálenosti radius od bodu (každá jen jednou).</summary>
    public IEnumerable<Structure> Near(double x, double y, double radius)
    {
        var seen = new HashSet<int>();
        var minX = (int)Math.Floor(x - radius);
        var maxX = (int)Math.Floor(x + radius);
        var minY = (int)Math.Floor(y - radius);
        var maxY = (int)Math.Floor(y + radius);
        for (var ty = minY; ty <= maxY; ty++)
        {
            for (var tx = minX; tx <= maxX; tx++)
            {
                if (!_byTile.TryGetValue((tx, ty), out var list))
                {
                    continue;
                }

                foreach (var structure in list)
                {
                    if (seen.Add(structure.Id))
                    {
                        yield return structure;
                    }
                }
            }
        }
    }

    public static int SticksCost(BuildKind kind)
    {
        var rules = GameData.Building;
        switch (kind)
        {
            case BuildKind.DoorFrame: return rules.DoorFrame.SticksCost;
            case BuildKind.Door: return rules.Door.SticksCost;
            default: return rules.Wall.SticksCost;
        }
    }

    private static int SticksBack(BuildKind kind)
    {
        var rules = GameData.Building;
        switch (kind)
        {
            case BuildKind.DoorFrame: return rules.DoorFrame.SticksBack;
            case BuildKind.Door: return rules.Door.SticksBack;
            default: return rules.Wall.SticksBack;
        }
    }

    /// <summary>Nejbližší zeď s otvorem bez dveří, jejíž střed je do snapDistance od bodu.</summary>
    private Structure? FreeDoorway(double x, double y)
    {
        var snap = GameData.Building.Door.SnapDistance;
        return Near(x, y, snap)
            .Where(s => s.Kind == BuildKind.DoorFrame && DoorIn(s.Id) is null)
            .Select(s => (Frame: s, Distance: Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y))))
            .Where(f => f.Distance <= snap)
            .OrderBy(f => f.Distance)
            .Select(f => f.Frame)
            .FirstOrDefault();
    }

    private void Take(Structure structure, Inventory inventory)
    {
        _byId.Remove(structure.Id);
        foreach (var tile in TilesOf(structure))
        {
            _byTile[tile].Remove(structure);
        }

        Version++;
        for (var i = 0; i < SticksBack(structure.Kind); i++)
        {
            inventory.TryAdd(Item.Stick);
        }
    }

    private bool WallFits(Island island, Fires fires, double x, double y, double angle, IEnumerable<Position> players)
    {
        var (x1, y1, x2, y2) = Structure.WallEnds(x, y, angle);
        var halfThickness = GameData.Building.Wall.Thickness / 2;
        if (!Placement.SegmentOnDryLand(island, x1, y1, x2, y2) || !Placement.ClearOfTrunks(island, x1, y1, x2, y2, halfThickness))
        {
            return false;
        }

        var fireClearance = GameData.Fire.Fire.FootprintRadius + halfThickness;
        if (fires.All.Any(f => Geometry.DistanceToSegment(f.X, f.Y, x1, y1, x2, y2) < fireClearance))
        {
            return false;
        }

        var playerClearance = GameData.Movement.Radius + halfThickness;
        return !players.Any(p => Geometry.DistanceToSegment(p.X, p.Y, x1, y1, x2, y2) < playerClearance);
    }

    /// <summary>Dlaždice, přes které stavba vede (obdélník kolem celé délky i s tloušťkou).</summary>
    private static IEnumerable<(int X, int Y)> TilesOf(Structure structure)
    {
        var (x1, y1, x2, y2) = structure.Ends;
        var margin = GameData.Building.Wall.Thickness / 2;
        for (var ty = (int)Math.Floor(Math.Min(y1, y2) - margin); ty <= (int)Math.Floor(Math.Max(y1, y2) + margin); ty++)
        {
            for (var tx = (int)Math.Floor(Math.Min(x1, x2) - margin); tx <= (int)Math.Floor(Math.Max(x1, x2) + margin); tx++)
            {
                yield return (tx, ty);
            }
        }
    }
}

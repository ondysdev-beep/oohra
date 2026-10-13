using Oohra.Core.Data;
using Oohra.Core.Items;

namespace Oohra.Core.World;

/// <summary>Stavba hráče s přesnou polohou (střed) a úhlem v radiánech (0 = na východ, roste po směru hodin).</summary>
public sealed class Structure
{
    public Structure(int id, BuildKind kind, double x, double y, double angle)
    {
        Id = id;
        Kind = kind;
        X = x;
        Y = y;
        Angle = angle;
    }

    public int Id { get; }

    public BuildKind Kind { get; }

    public double X { get; }

    public double Y { get; }

    public double Angle { get; }

    /// <summary>Konce zdi (úsečka ve středu její tloušťky).</summary>
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
}

/// <summary>
/// Stavby hráčů (zatím zdi z klacků). Na rozdíl od prototypu nejsou vázané na dlaždice: zeď je tenká příčka
/// s libovolnou polohou a úhlem a blokuje jen tam, kde opravdu stojí. Pro rychlé hledání si každou
/// stavbu pamatuje u dlaždic, přes které vede.
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

    /// <summary>Postaví zeď bez materiálu a kontrol (pro testy a později pro načtení uloženého světa).</summary>
    public Structure Place(double x, double y, double angle)
    {
        var structure = new Structure(_nextId++, BuildKind.StickWall, x, y, angle);
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
    /// Postaví zeď z klacků se středem (x, y) a úhlem angle. Nejdál placeReach od postavy, jen na suché zemi,
    /// ne přes kmen stromu, oheň ani hráče (players: polohy všech hráčů včetně stavitele).
    /// </summary>
    public BuildResult Build(
        Island island, Fires fires, Inventory inventory, double playerX, double playerY,
        double x, double y, double angle, IEnumerable<Position> players)
    {
        var rules = GameData.Building.Wall;
        if (inventory.Count(ItemKind.Stick) < rules.SticksCost)
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        if (!WallFits(island, fires, x, y, angle, players))
        {
            return BuildResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, rules.SticksCost);
        Place(x, y, angle);
        return BuildResult.Built;
    }

    /// <summary>Rozebere stavbu. Část klacků se vrátí do batohu (když se tam vejdou).</summary>
    public BuildResult Remove(int id, Inventory inventory, double playerX, double playerY)
    {
        var structure = Find(id);
        if (structure is null)
        {
            return BuildResult.NotFound;
        }

        var rules = GameData.Building.Wall;
        var (x1, y1, x2, y2) = structure.Ends;
        if (Geometry.DistanceToSegment(playerX, playerY, x1, y1, x2, y2) > rules.RemoveReach)
        {
            return BuildResult.TooFar;
        }

        _byId.Remove(id);
        foreach (var tile in TilesOf(structure))
        {
            _byTile[tile].Remove(structure);
        }

        Version++;
        for (var i = 0; i < rules.SticksBack; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        return BuildResult.Removed;
    }

    /// <summary>Kruh (postava, oheň) o daném poloměru se překrývá s některou zdí.</summary>
    public bool Blocks(double x, double y, double radius)
    {
        var reach = radius + GameData.Building.Wall.Thickness / 2;
        foreach (var structure in Near(x, y, reach))
        {
            var (x1, y1, x2, y2) = structure.Ends;
            if (Geometry.DistanceToSegment(x, y, x1, y1, x2, y2) < reach)
            {
                return true;
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

    /// <summary>Dlaždice, přes které stavba vede (obdélník kolem zdi i s tloušťkou).</summary>
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

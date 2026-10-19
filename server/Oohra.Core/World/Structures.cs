using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.Social;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>Úsečka v rovině mapy (část stavby, která blokuje nebo uzavírá úkryt).</summary>
public readonly record struct Segment(double X1, double Y1, double X2, double Y2);

/// <summary>
/// Stavba hráče s přesnou polohou (střed) a úhlem v radiánech (0 = na východ, roste po směru hodin).
/// Dveře si pamatují, ve které zdi s otvorem jsou (FrameId).
/// </summary>
public sealed class Structure
{
    public Structure(int id, BuildKind kind, double x, double y, double angle, double length, int frameId = 0, int ownerId = -1, string? decorationId = null, int level = 0)
    {
        Level = level;
        OwnerId = ownerId;
        DecorationId = decorationId ?? string.Empty;
        Store = kind == BuildKind.Stockpile ? new Inventory(GameData.Building.Stockpile.Capacity) : null;
        Id = id;
        Kind = kind;
        X = x;
        Y = y;
        Angle = angle;
        Length = length;
        FrameId = frameId;
    }

    public int Id { get; }

    public BuildKind Kind { get; }

    /// <summary>Podlaží: 0 přízemí, 1 patro (stojí na podlaze patra, Roofs).</summary>
    public int Level { get; }

    /// <summary>Dekorace: který druh z decorations.json (id). U ostatních staveb prázdné.</summary>
    public string DecorationId { get; }

    public double X { get; }

    public double Y { get; }

    public double Angle { get; }

    /// <summary>Délka stavby v metrech (zeď může být kratší, zeď s otvorem a dveře mají plnou délku).</summary>
    public double Length { get; }

    /// <summary>Dveře: číslo zdi s otvorem, ve které jsou. Jinak 0.</summary>
    public int FrameId { get; }

    /// <summary>Kdo stavbu postavil (číslo hráče), -1 = nikdo (staré stavby a testy, smí na ni kdokoli).</summary>
    public int OwnerId { get; }

    /// <summary>Zásoby kmene: co je v koši. U jiných staveb null.</summary>
    public Inventory? Store { get; }

    /// <summary>Past: co se do ní chytilo (null = nic).</summary>
    public AnimalKind? Caught { get; internal set; }

    /// <summary>Střecha bez podpory: od kdy praská (reálné sekundy Structures), null = drží.</summary>
    public double? CrackingSince { get; internal set; }

    /// <summary>Střecha: z čeho je (Branches pro ostatní stavby).</summary>
    public RoofMaterial Material { get; internal set; }

    /// <summary>Střecha: kdy byla postavená nebo naposledy opravená (herní minuty).</summary>
    public double RepairedAt { get; internal set; }

    /// <summary>Hoří od (herní minuty), null = nehoří (BuildingFire).</summary>
    public double? BurningSince { get; internal set; }

    /// <summary>Konce celé stavby (zeď, zeď s otvorem i dveře leží na úsečce dlouhé Length).</summary>
    public (double X1, double Y1, double X2, double Y2) Ends
    {
        get { return WallEnds(X, Y, Angle, Length); }
    }

    public static (double X1, double Y1, double X2, double Y2) WallEnds(double x, double y, double angle, double length)
    {
        var half = length / 2;
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
        var post = (Length - GameData.Building.DoorFrame.OpeningWidth) / 2;
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

    /// <summary>Postaví zeď plné délky bez materiálu a kontrol (pro testy).</summary>
    public Structure Place(double x, double y, double angle)
    {
        return Place(BuildKind.StickWall, x, y, angle);
    }

    /// <summary>
    /// Postaví stavbu bez materiálu a kontrol (pro testy a později pro načtení uloženého světa).
    /// length 0 = plná délka zdi.
    /// </summary>
    public Structure Place(BuildKind kind, double x, double y, double angle, int frameId = 0, double length = 0, int ownerId = -1, string? decorationId = null, int level = 0)
    {
        var structure = new Structure(_nextId++, kind, x, y, angle, length > 0 ? length : GameData.Building.Wall.Length, frameId, ownerId, decorationId, level);
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

    /// <summary>Všechny stavby pro uložení světa (u koše obsah, u pasti úlovek).</summary>
    public List<StructureSave> Export()
    {
        return _byId.Values.OrderBy(s => s.Id).Select(s => new StructureSave(
            s.Id, s.Kind, s.X, s.Y, s.Angle, s.Length, s.FrameId, s.OwnerId, s.DecorationId, s.Store?.Stacks.ToList(), s.Caught, s.Level,
            s.Material, s.RepairedAt, s.BurningSince)).ToList();
    }

    /// <summary>Obnoví stavby z uloženého světa se stejnými čísly.</summary>
    public void Restore(IEnumerable<StructureSave> saved)
    {
        foreach (var s in saved.OrderBy(s => s.Id))
        {
            _nextId = s.Id;
            var structure = Place(s.Kind, s.X, s.Y, s.Angle, s.FrameId, s.Length, s.OwnerId, s.DecorationId, s.Level);
            structure.Store?.Restore(s.Store ?? []);
            structure.Caught = s.Caught;
            structure.Material = s.Material;
            structure.RepairedAt = s.RepairedAt;
            structure.BurningSince = s.BurningSince;
        }
    }

    /// <summary>
    /// Postaví stavbu se středem (x, y), úhlem angle a délkou length, nejdál placeReach od postavy.
    /// Zeď: délka od minLength po plnou délku (kratší zeď dorovná mezeru, aby zdi do sebe zapadly).
    /// Zeď a zeď s otvorem: jen na suché zemi, ne přes kmen stromu, oheň ani hráče (players: polohy všech hráčů).
    /// Zeď s otvorem má vždy plnou délku. Dveře se vsadí do nejbližší volné zdi s otvorem (převezmou její polohu).
    /// Past a zásoby kmene jsou bod (jako oheň): suchá zem, ne na kmeni, zdi, ohni, jiné pasti ani zásobách.
    /// ownerId: kdo staví (rozebrat smí jen on a jeho kmen).
    /// </summary>
    public BuildResult Build(
        Island island, Fires fires, Inventory inventory, double playerX, double playerY,
        BuildKind kind, double x, double y, double angle, double length, IEnumerable<Position> players, Character? builder = null,
        int ownerId = -1, string? decorationId = null, int level = 0)
    {
        if (level > 0)
        {
            return BuildUpper(island, fires, inventory, playerX, playerY, kind, x, y, angle, length, players, builder, ownerId, level, decorationId);
        }

        if (kind == BuildKind.Ladder)
        {
            return BuildLadder(inventory, playerX, playerY, x, y, angle, builder, ownerId);
        }
        // Jeskyně (návrh 0.8, kap. 4): stěny dává skála, zdi a dveře se tu nestaví.
        if (island.InCave(x, y) && kind is BuildKind.StickWall or BuildKind.DoorFrame or BuildKind.Door)
        {
            return BuildResult.NotInCave;
        }

        if (kind == BuildKind.Barricade)
        {
            return BuildBarricade(island, inventory, playerX, playerY, x, y, builder, ownerId);
        }

        if (kind is BuildKind.Post or BuildKind.Roof or BuildKind.Floor)
        {
            return BuildFrame(island, fires, inventory, playerX, playerY, kind, x, y, angle, players, builder, ownerId, 0, decorationId);
        }

        if (kind == BuildKind.Decoration)
        {
            return BuildDecoration(island, fires, inventory, playerX, playerY, x, y, angle, players, builder, ownerId, decorationId);
        }

        var rules = GameData.Building.Wall;
        length = kind == BuildKind.StickWall ? length : rules.Length;
        if (inventory.Count(ItemKind.Stick) < SticksCost(kind, length, builder))
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        if (kind is BuildKind.Trap or BuildKind.Stockpile)
        {
            var footprint = kind == BuildKind.Trap ? GameData.Building.Trap.FootprintRadius : GameData.Building.Stockpile.FootprintRadius;
            if (!PointFits(island, fires, x, y, footprint))
            {
                return BuildResult.CannotBuildHere;
            }

            inventory.TryRemove(ItemKind.Stick, SticksCost(kind, length, builder));
            Place(kind, x, y, 0, length: 0.01, ownerId: ownerId);
            builder?.Practice(SkillKind.Craft, kind == BuildKind.Trap ? GameData.Animals.Traps.PracticeBuild : GameData.Skills.Practice.Wall);
            return BuildResult.Built;
        }

        if (kind == BuildKind.Door)
        {
            var frame = FreeDoorway(x, y);
            if (frame is null)
            {
                return BuildResult.NoDoorway;
            }

            inventory.TryRemove(ItemKind.Stick, SticksCost(kind, length, builder));
            Place(BuildKind.Door, frame.X, frame.Y, frame.Angle, frame.Id, ownerId: ownerId);
            builder?.Practice(SkillKind.Build, GameData.Skills.Practice.Door);
            return BuildResult.Built;
        }

        var lengthOk = length >= rules.MinLength - 1e-6 && length <= rules.Length + 1e-6;
        if (kind is not (BuildKind.StickWall or BuildKind.DoorFrame) || !lengthOk || !WallFits(island, fires, x, y, angle, length, players))
        {
            return BuildResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, SticksCost(kind, length, builder));
        Place(kind, x, y, angle, length: length, ownerId: ownerId);
        builder?.Practice(SkillKind.Build, kind == BuildKind.DoorFrame ? GameData.Skills.Practice.DoorFrame : GameData.Skills.Practice.Wall);
        return BuildResult.Built;
    }

    /// <summary>
    /// Dekorace z decorations.json: stojí klacky a kameny podle seznamu, zabere kruh o svém poloměru
    /// (suchá zem, ne kmen, zeď, oheň ani jiná bodová stavba). Když se přes ni nedá projít, nesmí stát na hráči.
    /// </summary>
    private BuildResult BuildDecoration(
        Island island, Fires fires, Inventory inventory, double playerX, double playerY,
        double x, double y, double angle, IEnumerable<Position> players, Character? builder, int ownerId, string? decorationId)
    {
        var decoration = GameData.Decoration(decorationId);
        if (decoration is null)
        {
            return BuildResult.CannotBuildHere;
        }

        if (inventory.Count(ItemKind.Stick) < decoration.Sticks || inventory.Count(ItemKind.Stone) < decoration.Stones)
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        var onPlayer = decoration.Blocks && players.Any(p => Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y)) < decoration.Radius + GameData.Movement.Radius);
        // Kruh z kamenů se staví i kolem ohniště (pak oheň stavby nezapálí).
        var ring = decoration.Id == GameData.Building.BuildingFire.RingDecoration;
        if (onPlayer || !PointFits(island, ring ? new Fires() : fires, x, y, decoration.Radius))
        {
            return BuildResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, decoration.Sticks);
        inventory.TryRemove(ItemKind.Stone, decoration.Stones);
        Place(BuildKind.Decoration, x, y, angle, length: 0.01, ownerId: ownerId, decorationId: decoration.Id);
        builder?.Practice(SkillKind.Build, GameData.Skills.Practice.Wall);
        return BuildResult.Built;
    }

    /// <summary>
    /// Rozebere stavbu (zeď s otvorem i s dveřmi). Část klacků se vrátí do batohu (když se tam vejdou).
    /// Cizí stavbu smí rozebrat jen majitel a jeho kmen (návrh 14.2), zásoby jen prázdné.
    /// </summary>
    public BuildResult Remove(int id, Inventory inventory, double playerX, double playerY, int playerId = -1, Tribes? tribes = null)
    {
        var structure = Find(id);
        if (structure is null)
        {
            return BuildResult.NotFound;
        }

        if (!MayTouch(structure, playerId, tribes))
        {
            return BuildResult.NotYours;
        }

        var (x1, y1, x2, y2) = structure.Ends;
        if (Geometry.DistanceToSegment(playerX, playerY, x1, y1, x2, y2) > GameData.Building.Wall.RemoveReach)
        {
            return BuildResult.TooFar;
        }

        if (structure.Store is { Stacks.Count: > 0 })
        {
            return BuildResult.NotEmpty;
        }

        if (structure.Kind == BuildKind.DoorFrame && DoorIn(structure.Id) is { } door)
        {
            Take(door, inventory);
        }

        Take(structure, inventory);
        return BuildResult.Removed;
    }

    /// <summary>Roste, když se něco uloží do zásob nebo z nich vezme (server pak pošle obsah znovu).</summary>
    public int StockVersion { get; private set; }

    /// <summary>Uložit celou hromádku z batohu do zásob kmene.</summary>
    public TribeResult StoreItem(int stockpileId, Inventory inventory, int stackIndex, double playerX, double playerY, int playerId, Tribes tribes)
    {
        var (stockpile, result) = OpenStockpile(stockpileId, playerX, playerY, playerId, tribes);
        if (stockpile is null)
        {
            return result;
        }

        if (stackIndex < 0 || stackIndex >= inventory.Stacks.Count)
        {
            return TribeResult.NotFound;
        }

        if (inventory.MoveStack(stackIndex, stockpile.Store!) == 0)
        {
            return TribeResult.StockFull;
        }

        StockVersion++;
        return TribeResult.Stored;
    }

    /// <summary>Vzít celou hromádku ze zásob kmene do batohu.</summary>
    public TribeResult TakeItem(int stockpileId, Inventory inventory, int stackIndex, double playerX, double playerY, int playerId, Tribes tribes)
    {
        var (stockpile, result) = OpenStockpile(stockpileId, playerX, playerY, playerId, tribes);
        if (stockpile is null)
        {
            return result;
        }

        if (stackIndex < 0 || stackIndex >= stockpile.Store!.Stacks.Count)
        {
            return TribeResult.NotFound;
        }

        if (stockpile.Store.MoveStack(stackIndex, inventory) == 0)
        {
            return TribeResult.InventoryFull;
        }

        StockVersion++;
        return TribeResult.Taken;
    }

    /// <summary>Zásoby, do kterých hráč smí a dosáhne, nebo null a důvod.</summary>
    private (Structure? Stockpile, TribeResult Result) OpenStockpile(int stockpileId, double playerX, double playerY, int playerId, Tribes tribes)
    {
        var stockpile = Find(stockpileId);
        if (stockpile?.Store is null)
        {
            return (null, TribeResult.NotFound);
        }

        if (!MayTouch(stockpile, playerId, tribes))
        {
            return (null, TribeResult.NotYours);
        }

        var distance = Math.Sqrt((stockpile.X - playerX) * (stockpile.X - playerX) + (stockpile.Y - playerY) * (stockpile.Y - playerY));
        return distance > GameData.Building.Stockpile.Reach ? (null, TribeResult.TooFar) : (stockpile, TribeResult.Stored);
    }

    /// <summary>Smí hráč sahat na stavbu? Ano, když nemá majitele, nebo je to jeho stavba, nebo stavba jeho kmene.</summary>
    public static bool MayTouch(Structure structure, int playerId, Tribes? tribes)
    {
        return structure.OwnerId < 0 || tribes is null || tribes.SameTribe(structure.OwnerId, playerId);
    }

    /// <summary>Kruh (postava, oheň) o daném poloměru se překrývá s něčím, co blokuje pohyb.</summary>
    public bool Blocks(double x, double y, double radius, int level = 0)
    {
        var reach = radius + GameData.Building.Wall.Thickness / 2;
        foreach (var structure in Near(x, y, reach))
        {
            // Překáží jen stavby ve stejném podlaží.
            if (structure.Level != level)
            {
                continue;
            }

            // Sloupek je kruh.
            if (structure.Kind == BuildKind.Post
                && Math.Sqrt((structure.X - x) * (structure.X - x) + (structure.Y - y) * (structure.Y - y)) < radius + GameData.Building.Post.Radius)
            {
                return true;
            }

            // Dekorace, přes kterou se nedá projít, je kruh.
            if (GameData.Decoration(structure.DecorationId) is { Blocks: true } decoration
                && Math.Sqrt((structure.X - x) * (structure.X - x) + (structure.Y - y) * (structure.Y - y)) < radius + decoration.Radius)
            {
                return true;
            }

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

    /// <summary>
    /// Kolik klacků stavba stojí. Krátká zeď (do shortMaxLength) je levnější. Zkušený stavitel
    /// (cheapWallLevel v skills.json) postaví plnou zeď za méně klacků.
    /// </summary>
    public static int SticksCost(BuildKind kind, double length, Character? builder = null)
    {
        var rules = GameData.Building;
        switch (kind)
        {
            case BuildKind.DoorFrame: return rules.DoorFrame.SticksCost;
            case BuildKind.Door: return rules.Door.SticksCost;
            case BuildKind.Trap: return rules.Trap.SticksCost;
            case BuildKind.Barricade: return rules.Barricade.SticksCost;
            case BuildKind.Post: return rules.Post.SticksCost;
            case BuildKind.Roof: return rules.Roof.SticksCost;
            case BuildKind.Floor: return rules.Floor.SticksCost;
            case BuildKind.Ladder: return rules.Ladder.SticksCost;
            case BuildKind.Stockpile: return rules.Stockpile.SticksCost;
            case BuildKind.Decoration: return 0;
        }

        if (length <= rules.Wall.ShortMaxLength + 1e-6)
        {
            return rules.Wall.ShortSticksCost;
        }

        var effects = GameData.Skills.Effects;
        return builder is not null && builder.Skills.Level(SkillKind.Build) >= effects.CheapWallLevel
            ? effects.CheapWallSticks
            : rules.Wall.SticksCost;
    }

    /// <summary>Kolik klacků se vrátí rozebráním: u zdi polovina ceny (dolů), jinak podle building.json.</summary>
    private static int SticksBack(Structure structure)
    {
        var rules = GameData.Building;
        switch (structure.Kind)
        {
            case BuildKind.DoorFrame: return rules.DoorFrame.SticksBack;
            case BuildKind.Door: return rules.Door.SticksBack;
            case BuildKind.Trap: return rules.Trap.SticksBack;
            case BuildKind.Barricade: return rules.Barricade.SticksBack;
            case BuildKind.Post: return rules.Post.SticksBack;
            case BuildKind.Roof: return rules.Roof.SticksBack;
            case BuildKind.Floor: return rules.Floor.SticksBack;
            case BuildKind.Ladder: return rules.Ladder.SticksBack;
            case BuildKind.Stockpile: return rules.Stockpile.SticksBack;
            case BuildKind.Decoration: return (GameData.Decoration(structure.DecorationId)?.Sticks ?? 0) / 2;
            default: return SticksCost(structure.Kind, structure.Length) / 2;
        }
    }

    /// <summary>Do pasti se chytilo zvíře (volá Animals). Hráči pak uvidí plnou past.</summary>
    public void Catch(Structure trap, AnimalKind kind)
    {
        trap.Caught = kind;
        Version++;
    }

    /// <summary>Vezme úlovek z pasti: maso (jelen víc, králík méně). Past zůstane a chytá dál.</summary>
    public HuntResult TakeCatch(int trapId, Inventory inventory, double playerX, double playerY, double now, int playerId = -1, Tribes? tribes = null)
    {
        var trap = Find(trapId);
        if (trap is null || trap.Kind != BuildKind.Trap)
        {
            return HuntResult.NotFound;
        }

        if (!MayTouch(trap, playerId, tribes))
        {
            return HuntResult.NotYours;
        }

        if (Math.Sqrt((trap.X - playerX) * (trap.X - playerX) + (trap.Y - playerY) * (trap.Y - playerY)) > GameData.Building.Wall.RemoveReach)
        {
            return HuntResult.TooFar;
        }

        if (trap.Caught is not { } kind)
        {
            return HuntResult.TrapEmpty;
        }

        var rules = GameData.Animals.Traps;
        var added = 0;
        for (var i = 0; i < (kind == AnimalKind.Deer ? rules.DeerMeat : rules.OtherMeat); i++)
        {
            if (inventory.TryAdd(Item.RawMeat(GameClock.DayOf(now))))
            {
                added++;
            }
        }

        if (added == 0)
        {
            return HuntResult.InventoryFull;
        }

        trap.Caught = null;
        Version++;
        return HuntResult.Butchered;
    }

    /// <summary>Past a zásoby: suchá zem, ne na kmeni, zdi, ohni ani jiné pasti či zásobách.</summary>
    private bool PointFits(Island island, Fires fires, double x, double y, double footprint)
    {
        return Placement.OnDryLand(island, x, y)
            && Placement.ClearOfTrunks(island, x, y, x, y, footprint)
            && !Blocks(x, y, footprint)
            && !fires.All.Any(f => Math.Sqrt((f.X - x) * (f.X - x) + (f.Y - y) * (f.Y - y)) < 2 * footprint)
            && !Near(x, y, footprint).Any(s => RadiusOf(s) > 0 && Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y)) < footprint + RadiusOf(s));
    }

    /// <summary>
    /// Zátaras: postaví se na hranu skály u nejbližšího vchodu do jeskyně (do entranceReach od místa, kam hráč
    /// míří), jeden na vchod. Nepřekáží v chůzi, jen nepustí cizí dovnitř (Caves.Pass).
    /// </summary>
    private BuildResult BuildBarricade(Island island, Inventory inventory, double playerX, double playerY, double x, double y, Character? builder, int ownerId)
    {
        var rules = GameData.Building.Barricade;
        if (inventory.Count(ItemKind.Stick) < rules.SticksCost)
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        var entrance = island.CaveEntrances
            .Where(e => Math.Sqrt((e.X - x) * (e.X - x) + (e.Y - y) * (e.Y - y)) <= rules.EntranceReach)
            .OrderBy(e => (e.X - x) * (e.X - x) + (e.Y - y) * (e.Y - y))
            .FirstOrDefault();
        if (entrance is null || Caves.BarricadeAt(island, entrance) is not null)
        {
            return BuildResult.NoCaveEntrance;
        }

        inventory.TryRemove(ItemKind.Stick, rules.SticksCost);
        var (bx, by, angle) = Caves.BarricadeSpot(entrance);
        Place(BuildKind.Barricade, bx, by, angle, length: 1, ownerId: ownerId);
        builder?.Practice(SkillKind.Build, GameData.Skills.Practice.Wall);
        return BuildResult.Built;
    }

    /// <summary>
    /// Sloupek, podlaha, střecha (návrh 0.8, kap. 6). Sloupek jako bodová stavba. Podlaha a střecha jsou čtverce:
    /// podlaha na suché zemi a nepřekrývá jinou podlahu, střecha nepřekrývá jinou střechu a musí mít podporu
    /// (Roofs.Stable), jinak NoSupport.
    /// </summary>
    private BuildResult BuildFrame(Island island, Fires fires, Inventory inventory, double playerX, double playerY, BuildKind kind,
        double x, double y, double angle, IEnumerable<Position> players, Character? builder, int ownerId, int level, string? variant = null)
    {
        var rules = GameData.Building;
        var material = kind == BuildKind.Roof ? RoofMaterialOf(variant) : null;
        var cost = material?.Sticks ?? SticksCost(kind, 0, builder);
        if (inventory.Count(ItemKind.Stick) < cost || (material is { Count: > 0 } m && inventory.Count(m.Item) < m.Count))
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        double length;
        if (kind == BuildKind.Post)
        {
            var fits = level == 0 ? PointFits(island, fires, x, y, rules.Post.Radius) : Roofs.StandsOn(this, x, y, level) && !Blocks(x, y, rules.Post.Radius, level);
            if (!fits || players.Any(p => Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y)) < GameData.Movement.Radius + rules.Post.Radius))
            {
                return level == 0 || fits ? BuildResult.CannotBuildHere : BuildResult.NoFloor;
            }

            length = 0.01;
            angle = 0;
        }
        else
        {
            var size = kind == BuildKind.Roof ? rules.Roof.Size : rules.Floor.Size;
            var probe = new Structure(-1, kind, x, y, angle, size, level: level);
            // Ve stejné výšce se desky nepřekrývají (střecha přízemí a podlaha patra jsou obě ve výšce zdí).
            var deck = Roofs.IsDeck(probe);
            var overlaps = All.Any(s => (deck ? Roofs.IsDeck(s) && Roofs.DeckHeight(s) == Roofs.DeckHeight(probe) : s.Kind == kind && s.Level == level)
                && Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y)) < size * 0.95);
            if (overlaps || (!deck && !Corners(probe).All(c => Placement.OnDryLand(island, c.X, c.Y))))
            {
                return BuildResult.CannotBuildHere;
            }

            if (deck && !Roofs.Stable(this, probe).Contains(probe.Id))
            {
                return BuildResult.NoSupport;
            }

            length = size;
        }

        inventory.TryRemove(ItemKind.Stick, cost);
        if (material is { Count: > 0 })
        {
            inventory.TryRemove(material.Item, material.Count);
        }

        var placed = Place(kind, x, y, angle, length: length, ownerId: ownerId, level: level);
        if (material is not null)
        {
            placed.Material = material.Material;
            placed.RepairedAt = _now;
        }

        builder?.Practice(SkillKind.Build, GameData.Skills.Practice.Wall);
        return BuildResult.Built;
    }

    /// <summary>Materiál střechy podle názvu (z nabídky stavby), bez názvu větve.</summary>
    public static RoofMaterialRules RoofMaterialOf(string? variant)
    {
        var all = GameData.Building.RoofMaterials;
        return all.FirstOrDefault(m => string.Equals(m.Material.ToString(), variant, StringComparison.OrdinalIgnoreCase)) ?? all[0];
    }

    /// <summary>Herní čas (minuty) pro nové střechy a zatékání. Server ho nastavuje každý tick.</summary>
    private double _now;

    public void SetTime(double now)
    {
        _now = now;
    }

    /// <summary>Střecha zatéká (materiál dosloužil), před deštěm nechrání, dokud se neopraví.</summary>
    public static bool Leaking(Structure roof, double now)
    {
        return roof.Kind == BuildKind.Roof && now - roof.RepairedAt > RoofMaterialOf(roof.Material.ToString()).Days * 1440;
    }

    /// <summary>Opravit střechu: stojí repairShare materiálu, zatékání začne počítat znovu.</summary>
    public BuildResult Repair(int id, Inventory inventory, double playerX, double playerY, int playerId, Tribes? tribes, double now)
    {
        var roof = Find(id);
        if (roof is null)
        {
            return BuildResult.NotFound;
        }

        if (roof.Kind != BuildKind.Roof || roof.BurningSince is not null)
        {
            return BuildResult.CannotRepair;
        }

        if (!MayTouch(roof, playerId, tribes))
        {
            return BuildResult.NotYours;
        }

        if (Math.Sqrt((roof.X - playerX) * (roof.X - playerX) + (roof.Y - playerY) * (roof.Y - playerY)) > GameData.Building.PlaceReach)
        {
            return BuildResult.TooFar;
        }

        var material = RoofMaterialOf(roof.Material.ToString());
        var share = GameData.Building.RepairShare;
        var (sticks, items) = ((int)Math.Ceiling(material.Sticks * share), (int)Math.Ceiling(material.Count * share));
        if (inventory.Count(ItemKind.Stick) < sticks || (items > 0 && inventory.Count(material.Item) < items))
        {
            return BuildResult.NeedMaterials;
        }

        inventory.TryRemove(ItemKind.Stick, sticks);
        if (items > 0)
        {
            inventory.TryRemove(material.Item, items);
        }

        roof.RepairedAt = now;
        Version++;
        return BuildResult.Repaired;
    }

    /// <summary>Stavba chytla (BuildingFire).</summary>
    internal void Ignite(Structure s, double now)
    {
        s.BurningSince = now;
        Version++;
    }

    /// <summary>Stavba shořela: zmizí i s tím, co v ní bylo (zásoby).</summary>
    internal void Burn(Structure s)
    {
        Take(s, new Inventory(0));
    }

    /// <summary>
    /// Stavba v patře (level 1): jen na podlahu patra, která drží. Zeď a zeď s otvorem celou délkou na podlaze,
    /// dveře do zdi s otvorem ve stejném podlaží, sloupek, střecha (o patro výš) a další podlaha patra.
    /// </summary>
    private BuildResult BuildUpper(Island island, Fires fires, Inventory inventory, double playerX, double playerY, BuildKind kind,
        double x, double y, double angle, double length, IEnumerable<Position> players, Character? builder, int ownerId, int level, string? variant)
    {
        if (kind is BuildKind.Post or BuildKind.Roof or BuildKind.Floor)
        {
            return BuildFrame(island, fires, inventory, playerX, playerY, kind, x, y, angle, players, builder, ownerId, level, variant);
        }

        if (kind is not (BuildKind.StickWall or BuildKind.DoorFrame or BuildKind.Door))
        {
            return BuildResult.CannotBuildHere;
        }

        var rules = GameData.Building.Wall;
        length = kind == BuildKind.StickWall ? length : rules.Length;
        if (inventory.Count(ItemKind.Stick) < SticksCost(kind, length, builder))
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        if (kind == BuildKind.Door)
        {
            var frame = FreeDoorway(x, y, level);
            if (frame is null)
            {
                return BuildResult.NoDoorway;
            }

            inventory.TryRemove(ItemKind.Stick, SticksCost(kind, length, builder));
            Place(BuildKind.Door, frame.X, frame.Y, frame.Angle, frame.Id, ownerId: ownerId, level: level);
            return BuildResult.Built;
        }

        var probe = new Structure(-1, kind, x, y, angle, length, level: level);
        if (!Roofs.Footprint(probe).All(p => Roofs.StandsOn(this, p.X, p.Y, level, 0.1)))
        {
            return BuildResult.NoFloor;
        }

        var (x1, y1, x2, y2) = probe.Ends;
        if (players.Any(p => Geometry.DistanceToSegment(p.X, p.Y, x1, y1, x2, y2) < GameData.Movement.Radius + rules.Thickness / 2))
        {
            return BuildResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, SticksCost(kind, length, builder));
        Place(kind, x, y, angle, length: length, ownerId: ownerId, level: level);
        builder?.Practice(SkillKind.Build, GameData.Skills.Practice.Wall);
        return BuildResult.Built;
    }

    /// <summary>Žebřík: v přízemí u podlahy patra (do floorMargin od ní). Natočí se k ní, v chůzi nepřekáží.</summary>
    private BuildResult BuildLadder(Inventory inventory, double playerX, double playerY, double x, double y, double angle, Character? builder, int ownerId)
    {
        var rules = GameData.Building.Ladder;
        if (inventory.Count(ItemKind.Stick) < rules.SticksCost)
        {
            return BuildResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return BuildResult.TooFar;
        }

        var floor = Storeys.FloorAt(this, x, y, rules.FloorMargin);
        if (floor is null)
        {
            return BuildResult.NoFloor;
        }

        inventory.TryRemove(ItemKind.Stick, rules.SticksCost);
        Place(BuildKind.Ladder, x, y, Math.Atan2(floor.Y - y, floor.X - x), length: 0.6, ownerId: ownerId);
        builder?.Practice(SkillKind.Build, GameData.Skills.Practice.Wall);
        return BuildResult.Built;
    }

    /// <summary>Rohy čtverce (podlaha, střecha).</summary>
    private static IEnumerable<Position> Corners(Structure square)
    {
        var half = square.Length / 2;
        var (c, s) = (Math.Cos(square.Angle), Math.Sin(square.Angle));
        foreach (var (a, b) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
        {
            yield return new Position(square.X + (a * c - b * s) * half, square.Y + (a * s + b * c) * half);
        }
    }

    private double _time;
    private double _nextCheckAt;

    /// <summary>
    /// Statika (realSeconds reálného času): střecha bez podpory zapraská, za warnSeconds spadne. Vrací střechy,
    /// které právě začaly praskat (server varuje, kdo je pod nimi) a které spadly (zraní, kdo stál pod nimi).
    /// Když se podpora vrátí včas, praskání přestane.
    /// </summary>
    public (List<Structure> Cracked, List<Structure> Fallen) Update(double realSeconds)
    {
        var rules = GameData.Building.Roof;
        _time += realSeconds;
        var fallen = new List<Structure>();
        var cracked = new List<Structure>();
        if (_time < _nextCheckAt)
        {
            return (cracked, fallen);
        }

        _nextCheckAt = _time + rules.CheckSeconds;
        var stable = Roofs.Stable(this);
        foreach (var roof in All.Where(s => Roofs.IsDeck(s) || s.Level > 0).ToList())
        {
            if (stable.Contains(roof.Id))
            {
                if (roof.CrackingSince is not null)
                {
                    roof.CrackingSince = null;
                    Version++;
                }
            }
            else if (roof.CrackingSince is null)
            {
                roof.CrackingSince = _time;
                cracked.Add(roof);
                Version++;
            }
            else if (_time - roof.CrackingSince >= rules.WarnSeconds)
            {
                Take(roof, new Inventory(0));
                fallen.Add(roof);
            }
        }

        return (cracked, fallen);
    }

    /// <summary>Poloměr bodové stavby (past, zásoby, dekorace), u zdí a dveří 0.</summary>
    public static double RadiusOf(Structure structure)
    {
        switch (structure.Kind)
        {
            case BuildKind.Trap: return GameData.Building.Trap.FootprintRadius;
            case BuildKind.Stockpile: return GameData.Building.Stockpile.FootprintRadius;
            case BuildKind.Post: return GameData.Building.Post.Radius;
            case BuildKind.Decoration: return GameData.Decoration(structure.DecorationId)?.Radius ?? 0;
            default: return 0;
        }
    }

    /// <summary>Nejbližší zeď s otvorem bez dveří, jejíž střed je do snapDistance od bodu.</summary>
    private Structure? FreeDoorway(double x, double y, int level = 0)
    {
        var snap = GameData.Building.Door.SnapDistance;
        return Near(x, y, snap)
            .Where(s => s.Kind == BuildKind.DoorFrame && s.Level == level && DoorIn(s.Id) is null)
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
        for (var i = 0; i < SticksBack(structure); i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        for (var i = 0; i < (GameData.Decoration(structure.DecorationId)?.Stones ?? 0) / 2; i++)
        {
            inventory.TryAdd(Item.Stone);
        }
    }

    private bool WallFits(Island island, Fires fires, double x, double y, double angle, double length, IEnumerable<Position> players)
    {
        var (x1, y1, x2, y2) = Structure.WallEnds(x, y, angle, length);
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
        var margin = Math.Max(GameData.Building.Wall.Thickness / 2, RadiusOf(structure));
        if (structure.Kind is BuildKind.Roof or BuildKind.Floor)
        {
            // Čtverec: celý (úhlopříčka), ať ho Near najde odkudkoli zpod něj.
            margin = structure.Length / 2 * Math.Sqrt(2);
            (x1, y1, x2, y2) = (structure.X, structure.Y, structure.X, structure.Y);
        }
        for (var ty = (int)Math.Floor(Math.Min(y1, y2) - margin); ty <= (int)Math.Floor(Math.Max(y1, y2) + margin); ty++)
        {
            for (var tx = (int)Math.Floor(Math.Min(x1, x2) - margin); tx <= (int)Math.Floor(Math.Max(x1, x2) + margin); tx++)
            {
                yield return (tx, ty);
            }
        }
    }
}

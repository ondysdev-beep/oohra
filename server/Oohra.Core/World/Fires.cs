using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.Plants;

namespace Oohra.Core.World;

/// <summary>Oheň ve světě s přesnou polohou. Když dohoří, zůstane popel a dá se znovu zapálit.</summary>
public sealed class Fire
{
    public Fire(int id, double x, double y, double burnMinutes)
    {
        Id = id;
        X = x;
        Y = y;
        BurnMinutes = burnMinutes;
    }

    public int Id { get; }

    public double X { get; }

    public double Y { get; }

    /// <summary>Kolik herních minut ještě bude hořet.</summary>
    public double BurnMinutes { get; set; }

    /// <summary>Kolik herních minut už celkem hořel (popel je až po ashAfterMinutes).</summary>
    public double Burned { get; set; }

    /// <summary>Herní den, kdy se naposledy bral popel (jednou za den).</summary>
    public int AshDay { get; set; } = -1;

    public bool Burning => BurnMinutes > 0;
}

/// <summary>
/// Všechny ohně na ostrově (prototyp S.fires, makeFire, nearFire a cook v part4.js a part5.js).
/// Ohniště táborů hoří pořád a hřejí stejně jako oheň hráče.
/// </summary>
public sealed class Fires
{
    private readonly List<Fire> _fires = [];
    private int _nextId = 1;

    public IReadOnlyList<Fire> All => _fires;

    /// <summary>Zvýší se, když hráči uvidí změnu (nový oheň, vyhasnutí, znovu zapálení). Server pak pošle seznam ohňů.</summary>
    public int Version { get; private set; }

    public Fire? Find(int fireId)
    {
        return _fires.Find(f => f.Id == fireId);
    }

    /// <summary>Ohně hráčů pro uložení světa (ohniště táborů nejsou v seznamu, hoří pořád).</summary>
    public List<FireSave> Export()
    {
        return _fires.Select(f => new FireSave(f.Id, f.X, f.Y, f.BurnMinutes, f.Burned, f.AshDay)).ToList();
    }

    public int NextId => _nextId;

    /// <summary>Obnoví ohně z uloženého světa.</summary>
    public void Restore(IEnumerable<FireSave> saved, int nextId)
    {
        _fires.AddRange(saved.Select(f => new Fire(f.Id, f.X, f.Y, f.BurnMinutes) { Burned = f.Burned, AshDay = f.AshDay }));
        _nextId = Math.Max(nextId, _fires.Count == 0 ? 1 : _fires.Max(f => f.Id) + 1);
        Version++;
    }

    /// <summary>Ohně hoří a ubývá jim dřevo.</summary>
    public void Burn(double minutes)
    {
        foreach (var fire in _fires)
        {
            if (!fire.Burning)
            {
                continue;
            }

            fire.Burned += Math.Min(minutes, fire.BurnMinutes);
            fire.BurnMinutes = Math.Max(0, fire.BurnMinutes - minutes);
            if (!fire.Burning)
            {
                Version++;
            }
        }
    }

    /// <summary>
    /// Rozdělá oheň z klacků a kamene na místě (x, y) z režimu stavby: nejdál placeReach od postavy,
    /// na suché zemi, ne na kmeni stromu, jiném ohni ani zdi.
    /// </summary>
    public FireResult Light(Island island, Inventory inventory, double playerX, double playerY, double x, double y, Character? maker = null)
    {
        var rules = GameData.Fire.Fire;
        var sticks = FireSticks(maker);
        if (inventory.Count(ItemKind.Stick) < sticks || inventory.Count(ItemKind.Stone) < rules.StonesCost)
        {
            return FireResult.NeedMaterials;
        }

        if (!Placement.InReach(playerX, playerY, x, y))
        {
            return FireResult.TooFar;
        }

        var footprint = rules.FootprintRadius;
        if (!Placement.OnDryLand(island, x, y)
            || !Placement.LevelEnough(island, [new Position(x, y)])
            || !Placement.ClearOfTrunks(island, x, y, x, y, footprint)
            || _fires.Exists(f => Distance(f.X, f.Y, x, y) < 2 * footprint)
            || island.Structures.Blocks(x, y, footprint))
        {
            return FireResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, sticks);
        inventory.TryRemove(ItemKind.Stone, rules.StonesCost);
        _fires.Add(new Fire(_nextId++, x, y, rules.BurnMinutes));
        Version++;
        maker?.Practice(SkillKind.Craft, GameData.Skills.Practice.Fire);
        return FireResult.Lit;
    }

    /// <summary>
    /// Přiloží dřevo: díl dřeva (hoří déle, woodBurnMultiplier), jinak klacek, jinak třísku.
    /// Hořící oheň hoří déle, vyhaslý znovu chytí.
    /// </summary>
    public FireResult Feed(int fireId, Inventory inventory, double playerX, double playerY)
    {
        var fire = Find(fireId);
        if (fire is null || Distance(fire.X, fire.Y, playerX, playerY) > GameData.Fire.Reach.Fire)
        {
            return FireResult.TooFar;
        }

        var log = inventory.TryRemove(ItemKind.Wood, 1);
        if (!log && !inventory.TryRemove(ItemKind.Stick, 1) && !inventory.TryRemove(ItemKind.Splinter, 1))
        {
            return FireResult.NoStick;
        }

        var rules = GameData.Fire.Fire;
        if (fire.Burning)
        {
            fire.BurnMinutes += rules.FeedMinutes * (log ? GameData.Wood.WoodBurnMultiplier : 1);
            return FireResult.Fed;
        }

        fire.BurnMinutes = rules.RelightMinutes;
        Version++;
        return FireResult.Relit;
    }

    /// <summary>
    /// Nabere popel z ohniště: oheň musel hořet aspoň ashAfterMinutes (crafting.json), jednou za herní den.
    /// Popel je na činění kůže.
    /// </summary>
    public FireResult TakeAsh(int fireId, Inventory inventory, double playerX, double playerY, double now)
    {
        var fire = Find(fireId);
        if (fire is null || Distance(fire.X, fire.Y, playerX, playerY) > GameData.Fire.Reach.Fire)
        {
            return FireResult.TooFar;
        }

        var day = Time.GameClock.DayOf(now);
        if (fire.Burned < GameData.Crafting.AshAfterMinutes || fire.AshDay == day)
        {
            return FireResult.NoAsh;
        }

        if (!inventory.TryAdd(Item.Ash))
        {
            return FireResult.InventoryFull;
        }

        fire.AshDay = day;
        return FireResult.AshTaken;
    }

    /// <summary>V daném okruhu hoří oheň (hráče nebo ohniště tábora).</summary>
    public bool BurningNear(Island island, double x, double y, double radius)
    {
        foreach (var fire in _fires)
        {
            if (fire.Burning && Distance(fire.X, fire.Y, x, y) <= radius)
            {
                return true;
            }
        }

        foreach (var camp in island.Camps)
        {
            if (Distance(camp.X + 0.5, camp.Y + 0.5, x, y) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Hřeje tu oheň (pro teplo těla).</summary>
    public bool WarmsAt(Island island, double x, double y)
    {
        return BurningNear(island, x, y, GameData.Fire.Fire.WarmthRadius);
    }

    /// <summary>Upeče jeden kus syrové rostliny nebo masa z hromádky. Musí být u hořícího ohně.</summary>
    public FireResult Bake(Island island, Inventory inventory, int stackIndex, double playerX, double playerY, Character? baker = null)
    {
        if (!BurningNear(island, playerX, playerY, GameData.Fire.Fire.CookRadius))
        {
            return FireResult.NoFireNear;
        }

        if (stackIndex < 0 || stackIndex >= inventory.Stacks.Count || !CanBake(inventory.Stacks[stackIndex].Item))
        {
            return FireResult.CannotBake;
        }

        var raw = inventory.TakeOne(stackIndex)!;
        var baked = raw.IsFlesh ? raw with { Cooking = Cooking.Baked } : Item.Of(raw.Plant! with { Cooking = Cooking.Baked });
        if (!inventory.TryAdd(baked))
        {
            inventory.TryAdd(raw);
            return FireResult.InventoryFull;
        }

        baker?.Practice(SkillKind.Craft, GameData.Skills.Practice.Bake);
        return FireResult.Baked;
    }

    /// <summary>Péct jde syrovou část rostliny, syrové maso a rybu.</summary>
    private static bool CanBake(Item item)
    {
        return item.IsFlesh ? item.Cooking == Cooking.Raw : item.Plant is { Cooking: Cooking.Raw };
    }

    /// <summary>Kolik klacků stojí oheň. Zkušený řemeslník (cheapFireLevel v skills.json) potřebuje méně.</summary>
    public static int FireSticks(Character? maker)
    {
        var effects = GameData.Skills.Effects;
        return maker is not null && maker.Skills.Level(SkillKind.Craft) >= effects.CheapFireLevel
            ? effects.CheapFireSticks
            : GameData.Fire.Fire.SticksCost;
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

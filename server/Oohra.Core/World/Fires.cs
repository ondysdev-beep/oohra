using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
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

    /// <summary>Ohně hoří a ubývá jim dřevo.</summary>
    public void Burn(double minutes)
    {
        foreach (var fire in _fires)
        {
            if (!fire.Burning)
            {
                continue;
            }

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

    /// <summary>Přiloží klacek. Hořící oheň hoří déle, vyhaslý znovu chytí.</summary>
    public FireResult Feed(int fireId, Inventory inventory, double playerX, double playerY)
    {
        var fire = Find(fireId);
        if (fire is null || Distance(fire.X, fire.Y, playerX, playerY) > GameData.Fire.Reach.Fire)
        {
            return FireResult.TooFar;
        }

        if (!inventory.TryRemove(ItemKind.Stick, 1))
        {
            return FireResult.NoStick;
        }

        var rules = GameData.Fire.Fire;
        if (fire.Burning)
        {
            fire.BurnMinutes += rules.FeedMinutes;
            return FireResult.Fed;
        }

        fire.BurnMinutes = rules.RelightMinutes;
        Version++;
        return FireResult.Relit;
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

    /// <summary>Upeče jeden kus syrové rostliny z hromádky. Musí být u hořícího ohně.</summary>
    public FireResult Bake(Island island, Inventory inventory, int stackIndex, double playerX, double playerY, Character? baker = null)
    {
        if (!BurningNear(island, playerX, playerY, GameData.Fire.Fire.CookRadius))
        {
            return FireResult.NoFireNear;
        }

        if (stackIndex < 0 || stackIndex >= inventory.Stacks.Count
            || inventory.Stacks[stackIndex].Item.Plant is not { Cooking: Cooking.Raw })
        {
            return FireResult.CannotBake;
        }

        var raw = inventory.TakeOne(stackIndex)!;
        if (!inventory.TryAdd(Item.Of(raw.Plant! with { Cooking = Cooking.Baked })))
        {
            inventory.TryAdd(raw);
            return FireResult.InventoryFull;
        }

        baker?.Practice(SkillKind.Craft, GameData.Skills.Practice.Bake);
        return FireResult.Baked;
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

using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;

namespace Oohra.Core.World;

/// <summary>Oheň ve světě. Stojí uprostřed dlaždice. Když dohoří, zůstane popel a dá se znovu zapálit.</summary>
public sealed class Fire
{
    public Fire(int id, int tileX, int tileY, double burnMinutes)
    {
        Id = id;
        TileX = tileX;
        TileY = tileY;
        BurnMinutes = burnMinutes;
    }

    public int Id { get; }

    public int TileX { get; }

    public int TileY { get; }

    public double X => TileX + 0.5;

    public double Y => TileY + 0.5;

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

    /// <summary>Rozdělá oheň z klacků a kamene na volné dlaždici, kam se postava dívá.</summary>
    public FireResult Light(Island island, Inventory inventory, double playerX, double playerY, double facing)
    {
        var rules = GameData.Fire.Fire;
        if (inventory.Count(ItemKind.Stick) < rules.SticksCost || inventory.Count(ItemKind.Stone) < rules.StonesCost)
        {
            return FireResult.NeedMaterials;
        }

        var (tileX, tileY) = FacingTile(playerX, playerY, facing);
        if (Movement.IsBlocked(island, tileX, tileY) || _fires.Exists(f => f.TileX == tileX && f.TileY == tileY))
        {
            return FireResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, rules.SticksCost);
        inventory.TryRemove(ItemKind.Stone, rules.StonesCost);
        _fires.Add(new Fire(_nextId++, tileX, tileY, rules.BurnMinutes));
        Version++;
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
            return FireResult.NeedMaterials;
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
    public FireResult Bake(Island island, Inventory inventory, int stackIndex, double playerX, double playerY)
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

        return FireResult.Baked;
    }

    /// <summary>Dlaždice, kam se postava dívá (prototyp facingTile): sousední ve směru, který převažuje.</summary>
    public static (int X, int Y) FacingTile(double playerX, double playerY, double facing)
    {
        var fx = Math.Cos(facing);
        var fy = Math.Sin(facing);
        var x = (int)Math.Floor(playerX);
        var y = (int)Math.Floor(playerY);
        return Math.Abs(fx) > Math.Abs(fy) ? (x + (fx > 0 ? 1 : -1), y) : (x, y + (fy > 0 ? 1 : -1));
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

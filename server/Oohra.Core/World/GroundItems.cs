using Oohra.Core.Data;
using Oohra.Core.Items;

namespace Oohra.Core.World;

/// <summary>
/// Hromádka stejných věcí na zemi: kde leží (Level = podlaží, 0 země), co to je, kolik kusů a kdy se naposledy
/// něco přidalo (herní minuty, podle toho zmizí).
/// </summary>
public sealed record GroundPile(int Id, double X, double Y, int Level, Item Item, int Count, double DroppedAt);

/// <summary>
/// Věci na zemi (ground.json). Co hráč zahodí, nezmizí, ale leží tam, kde ho položil, a kdokoli ho může sebrat.
/// Z hromádek blízko postavy se dá vyrábět (Crafting s GroundAccess), takže i s plnýma rukama si člověk
/// snese materiál na jedno místo. Věc na zemi za lifetimeDays zmizí, maso a kůže dál hnijí podle dne získání.
/// </summary>
public sealed class GroundItems
{
    private const double MinutesPerDay = 1440;

    private readonly List<GroundPile> _piles = [];
    private int _nextId = 1;

    public IReadOnlyList<GroundPile> All => _piles;

    /// <summary>Roste s každou změnou (server podle toho pošle hromádky znovu).</summary>
    public int Version { get; private set; }

    public int NextId => _nextId;

    public GroundPile? Find(int pileId)
    {
        return _piles.Find(p => p.Id == pileId);
    }

    /// <summary>Hromádky ve stejném podlaží do dané vzdálenosti, nejbližší první.</summary>
    public List<GroundPile> Near(double x, double y, int level, double distance)
    {
        return _piles
            .Where(p => p.Level == level && Distance(p, x, y) <= distance)
            .OrderBy(p => Distance(p, x, y))
            .ThenBy(p => p.Id)
            .ToList();
    }

    /// <summary>
    /// Položí věci na zem kousek před postavu (do vody nebo skály ne, pak k nohám). Stejná věc, která už leží
    /// blízko, se přidá na tu hromádku (dokud se vejde).
    /// </summary>
    public GroundPile Put(Island island, Item item, int count, double playerX, double playerY, int level, double facing, double now)
    {
        var rules = GameData.Ground;
        var (x, y) = (playerX + Math.Cos(facing) * rules.DropDistance, playerY + Math.Sin(facing) * rules.DropDistance);
        if (level == 0 && !Placement.OnDryLand(island, x, y))
        {
            (x, y) = (playerX, playerY);
        }

        var same = _piles.FindIndex(p => p.Level == level && p.Item == item && p.Count + count <= rules.MaxPile && Distance(p, x, y) <= rules.MergeRadius);
        Version++;
        if (same >= 0)
        {
            _piles[same] = _piles[same] with { Count = _piles[same].Count + count, DroppedAt = now };
            return _piles[same];
        }

        var pile = new GroundPile(_nextId++, x, y, level, item, count, now);
        _piles.Add(pile);
        return pile;
    }

    /// <summary>Zahodí celou hromádku z batohu na zem (hráč, Zahodit).</summary>
    public GroundResult Drop(Island island, Inventory inventory, int stackIndex, double playerX, double playerY, int level, double facing, double now)
    {
        if (stackIndex < 0 || stackIndex >= inventory.Stacks.Count)
        {
            return GroundResult.NotFound;
        }

        var stack = inventory.Stacks[stackIndex];
        inventory.Drop(stackIndex);
        Put(island, stack.Item, stack.Count, playerX, playerY, level, facing, now);
        return GroundResult.Dropped;
    }

    /// <summary>Sebere hromádku do batohu (kolik se vejde, zbytek leží dál).</summary>
    public GroundResult PickUp(int pileId, Inventory inventory, double playerX, double playerY, int level)
    {
        var index = _piles.FindIndex(p => p.Id == pileId);
        if (index < 0)
        {
            return GroundResult.NotFound;
        }

        var pile = _piles[index];
        if (pile.Level != level || Distance(pile, playerX, playerY) > GameData.Ground.Reach)
        {
            return GroundResult.TooFar;
        }

        var taken = 0;
        while (taken < pile.Count && inventory.TryAdd(pile.Item))
        {
            taken++;
        }

        if (taken == 0)
        {
            return GroundResult.InventoryFull;
        }

        SetCount(index, pile.Count - taken);
        return GroundResult.Taken;
    }

    /// <summary>Vezme z hromádky jeden kus (materiál na výrobu). Vrací null, když hromádka není.</summary>
    public Item? TakeOne(int pileId)
    {
        var index = _piles.FindIndex(p => p.Id == pileId);
        if (index < 0)
        {
            return null;
        }

        var item = _piles[index].Item;
        SetCount(index, _piles[index].Count - 1);
        return item;
    }

    private void SetCount(int index, int count)
    {
        if (count <= 0)
        {
            _piles.RemoveAt(index);
        }
        else
        {
            _piles[index] = _piles[index] with { Count = count };
        }

        Version++;
    }

    /// <summary>Hromádky starší než lifetimeDays zmizí.</summary>
    public void Decay(double now)
    {
        var lifetime = GameData.Ground.LifetimeDays * MinutesPerDay;
        if (_piles.RemoveAll(p => now >= p.DroppedAt + lifetime) > 0)
        {
            Version++;
        }
    }

    /// <summary>
    /// Hromádky v patře, pod kterými už podlaha není (spadla, nebo ji někdo rozebral), spadnou na zem.
    /// </summary>
    public void Settle(Func<GroundPile, bool> supported)
    {
        for (var i = 0; i < _piles.Count; i++)
        {
            if (_piles[i].Level > 0 && !supported(_piles[i]))
            {
                _piles[i] = _piles[i] with { Level = 0 };
                Version++;
            }
        }
    }

    public List<GroundPile> Export()
    {
        return _piles.ToList();
    }

    public void Restore(IEnumerable<GroundPile> saved, int nextId)
    {
        _piles.AddRange(saved);
        _nextId = Math.Max(nextId, _piles.Count == 0 ? 1 : _piles.Max(p => p.Id) + 1);
        Version++;
    }

    private static double Distance(GroundPile pile, double x, double y)
    {
        return Math.Sqrt((pile.X - x) * (pile.X - x) + (pile.Y - y) * (pile.Y - y));
    }
}

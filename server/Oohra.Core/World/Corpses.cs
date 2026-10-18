using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.Social;

namespace Oohra.Core.World;

/// <summary>Tělo mrtvého hráče s věcmi, které měl u sebe (prototyp corpses).</summary>
public sealed class Corpse
{
    public Corpse(int id, double x, double y, int ownerId, int generation, double diedAt, Inventory items, Signature? signature)
    {
        Id = id;
        X = x;
        Y = y;
        OwnerId = ownerId;
        Generation = generation;
        DiedAt = diedAt;
        Items = items;
        Signature = signature;
    }

    public int Id { get; }

    public double X { get; }

    public double Y { get; }

    /// <summary>Číslo hráče, který zemřel.</summary>
    public int OwnerId { get; }

    /// <summary>Generace mrtvé postavy (Pradávný vypadá jinak).</summary>
    public int Generation { get; }

    /// <summary>Kdy zemřel (herní minuty).</summary>
    public double DiedAt { get; }

    public Inventory Items { get; }

    /// <summary>Podpis hráče v okamžiku smrti, nebo null.</summary>
    public Signature? Signature { get; }

    /// <summary>Do kdy smí brát jen jeho kmen (herní minuty).</summary>
    public double TribeOnlyUntil => DiedAt + GameData.Corpses.TribeOnlyHours * 60;
}

/// <summary>
/// Těla mrtvých hráčů (návrh 7.4 a 14.1). Věci z batohu zůstanou u těla: prvních 24 hodin si je smí vzít jen
/// mrtvý (svým dalším životem) a jeho kmen, potom kdokoli. Tělo zmizí po 4 dnech.
/// </summary>
public sealed class Corpses
{
    private const double MinutesPerDay = 1440;

    private readonly List<Corpse> _corpses = [];
    private int _nextId = 1;

    public IReadOnlyList<Corpse> All => _corpses;

    /// <summary>Roste s každou změnou (server podle toho pošle těla znovu).</summary>
    public int Version { get; private set; }

    public Corpse? Find(int corpseId)
    {
        return _corpses.Find(c => c.Id == corpseId);
    }

    /// <summary>Hráč zemřel: věci z batohu se přesunou k jeho tělu (batoh zůstane prázdný).</summary>
    public Corpse Add(double x, double y, int ownerId, int generation, double now, Inventory backpack, Signature? signature)
    {
        // I batoh ze zad (sundá se a přesune k tělu s ostatním).
        backpack.TakeOffBag();
        var items = new Inventory(Math.Max(backpack.Capacity, backpack.Stacks.Count));
        while (backpack.Stacks.Count > 0)
        {
            backpack.MoveStack(0, items);
        }

        var corpse = new Corpse(_nextId++, x, y, ownerId, generation, now, items, signature);
        _corpses.Add(corpse);
        Version++;
        return corpse;
    }

    /// <summary>Těla s věcmi pro uložení světa.</summary>
    public List<CorpseSave> Export()
    {
        return _corpses.Select(c => new CorpseSave(c.Id, c.X, c.Y, c.OwnerId, c.Generation, c.DiedAt, c.Items.Capacity, c.Items.Stacks.ToList(), c.Signature)).ToList();
    }

    public int NextId => _nextId;

    public void Restore(IEnumerable<CorpseSave> saved, int nextId)
    {
        foreach (var c in saved)
        {
            var items = new Inventory(c.Capacity);
            items.Restore(c.Items);
            _corpses.Add(new Corpse(c.Id, c.X, c.Y, c.OwnerId, c.Generation, c.DiedAt, items, c.Signature));
        }

        _nextId = Math.Max(nextId, _corpses.Count == 0 ? 1 : _corpses.Max(c => c.Id) + 1);
        Version++;
    }

    /// <summary>Smí hráč z těla brát? Mrtvý a jeho kmen hned, ostatní až po tribeOnlyHours.</summary>
    public static bool MayLoot(Corpse corpse, int playerId, Tribes tribes, double now)
    {
        return now >= corpse.TribeOnlyUntil || tribes.SameTribe(corpse.OwnerId, playerId);
    }

    /// <summary>Vzít z těla celou hromádku do batohu.</summary>
    public CorpseResult Take(int corpseId, int stackIndex, Inventory inventory, double playerX, double playerY, int playerId, Tribes tribes, double now)
    {
        var corpse = Find(corpseId);
        if (corpse is null)
        {
            return CorpseResult.NotFound;
        }

        if (Math.Sqrt((corpse.X - playerX) * (corpse.X - playerX) + (corpse.Y - playerY) * (corpse.Y - playerY)) > GameData.Corpses.Reach)
        {
            return CorpseResult.TooFar;
        }

        if (!MayLoot(corpse, playerId, tribes, now))
        {
            return CorpseResult.TribeOnly;
        }

        if (corpse.Items.Stacks.Count == 0)
        {
            return CorpseResult.Empty;
        }

        if (stackIndex < 0 || stackIndex >= corpse.Items.Stacks.Count)
        {
            return CorpseResult.NotFound;
        }

        if (corpse.Items.MoveStack(stackIndex, inventory) == 0)
        {
            return CorpseResult.InventoryFull;
        }

        Version++;
        return CorpseResult.Taken;
    }

    /// <summary>Těla starší než lifetimeDays zmizí (i s věcmi, které si nikdo nevzal).</summary>
    public void Decay(double now)
    {
        var lifetime = GameData.Corpses.LifetimeDays * MinutesPerDay;
        if (_corpses.RemoveAll(c => now >= c.DiedAt + lifetime) > 0)
        {
            Version++;
        }
    }
}

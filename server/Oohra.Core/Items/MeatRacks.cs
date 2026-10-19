using Oohra.Core.Data;
using Oohra.Core.Persistence;
using Oohra.Core.Plants;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Items;

/// <summary>Kus masa zavěšený v kouři nebo na sušáku: co to je a kolik herních minut se už udí nebo suší.</summary>
public sealed class RackPiece
{
    public RackPiece(Item item, double progress = 0)
    {
        Item = item;
        Progress = progress;
    }

    public Item Item { get; internal set; }

    public double Progress { get; internal set; }

    /// <summary>Hotovo (uzené nebo sušené).</summary>
    public bool Done => Item.Cooking is Cooking.Smoked or Cooking.Dried;
}

/// <summary>
/// Uzení a sušení masa (návrh 0.8, kap. 5, preserving.json): kusy syrového masa nebo ryby visí v kouři ohně
/// (uzení, jen když oheň hoří) nebo na sušáku (sušení, jen ve dne a za sucha). Hotové maso vydrží déle
/// (Spoilage) a počítá se jako nově získané. Kdo kus vezme dřív, dostane ho syrový.
/// </summary>
public sealed class MeatRacks
{
    private readonly Dictionary<(RackHolder Holder, int Id), List<RackPiece>> _racks = [];

    /// <summary>Zvedne se při změně, kterou vidí klient (pověšení, vzetí, desetina postupu, hotovo).</summary>
    public int Version { get; private set; }

    public IEnumerable<((RackHolder Holder, int Id) Key, IReadOnlyList<RackPiece> Pieces)> All =>
        _racks.Where(r => r.Value.Count > 0).OrderBy(r => r.Key.Holder).ThenBy(r => r.Key.Id).Select(r => (r.Key, (IReadOnlyList<RackPiece>)r.Value));

    public IReadOnlyList<RackPiece> On(RackHolder holder, int id)
    {
        return _racks.TryGetValue((holder, id), out var pieces) ? pieces : [];
    }

    public static int Slots(RackHolder holder)
    {
        return holder == RackHolder.Fire ? GameData.Preserving.FireSlots : GameData.Preserving.RackSlots;
    }

    /// <summary>Pověsí syrové nezkažené maso nebo rybu z hromádky (kolik se vejde).</summary>
    public PreserveResult Hang(RackHolder holder, int id, Inventory inventory, int stackIndex, double now)
    {
        if (stackIndex < 0 || stackIndex >= inventory.Stacks.Count)
        {
            return PreserveResult.NotMeat;
        }

        var item = inventory.Stacks[stackIndex].Item;
        if (!item.IsFlesh || item.Cooking != Cooking.Raw)
        {
            return PreserveResult.NotMeat;
        }

        if (Spoilage.IsSpoiled(item, now))
        {
            return PreserveResult.Spoiled;
        }

        var pieces = Rack(holder, id);
        if (pieces.Count >= Slots(holder))
        {
            return PreserveResult.RackFull;
        }

        while (pieces.Count < Slots(holder) && stackIndex < inventory.Stacks.Count && inventory.Stacks[stackIndex].Item == item)
        {
            pieces.Add(new RackPiece(inventory.TakeOne(stackIndex)!));
        }

        Version++;
        return PreserveResult.Hung;
    }

    /// <summary>Vezme kus (piece), nebo s piece = -1 nejdřív hotový, jinak ten, který je nejdál.</summary>
    public PreserveResult Take(RackHolder holder, int id, int piece, Inventory inventory)
    {
        var pieces = Rack(holder, id);
        if (piece < 0)
        {
            piece = pieces.Count == 0 ? -1 : pieces.IndexOf(pieces.OrderByDescending(p => p.Done).ThenByDescending(p => p.Progress).First());
        }

        if (piece < 0 || piece >= pieces.Count)
        {
            return PreserveResult.NothingHere;
        }

        if (!inventory.TryAdd(pieces[piece].Item))
        {
            return PreserveResult.InventoryFull;
        }

        pieces.RemoveAt(piece);
        Version++;
        return PreserveResult.Taken;
    }

    /// <summary>
    /// Čas běží: v kouři hořícího ohně se udí, na sušáku ve dne za sucha se suší. Stojany, jejichž oheň nebo sušák
    /// už není, zmizí i s masem.
    /// </summary>
    public void Update(Fires fires, Structures structures, GameClock clock, WeatherKind weather, double minutes)
    {
        var rules = GameData.Preserving;
        var drying = clock.Daylight >= rules.DryMinDaylight && weather is not (WeatherKind.Rain or WeatherKind.Storm);
        foreach (var ((holder, id), pieces) in _racks.ToList())
        {
            var exists = holder == RackHolder.Fire ? fires.Find(id) is not null : structures.All.Any(s => s.Id == id);
            if (!exists)
            {
                _racks.Remove((holder, id));
                Version++;
                continue;
            }

            var working = holder == RackHolder.Fire ? fires.Find(id)!.Burning : drying;
            if (!working)
            {
                continue;
            }

            var needed = holder == RackHolder.Fire ? rules.SmokeMinutes : rules.DryMinutes;
            var done = holder == RackHolder.Fire ? Cooking.Smoked : Cooking.Dried;
            foreach (var piece in pieces.Where(p => !p.Done))
            {
                var before = (int)(piece.Progress / needed * 10);
                piece.Progress += minutes;
                if (piece.Progress >= needed)
                {
                    // Hotové maso se počítá jako nově získané (kazí se od dneška).
                    piece.Item = piece.Item with { Cooking = done, Day = GameClock.DayOf(clock.TotalMinutes) };
                    Version++;
                }
                else if ((int)(piece.Progress / needed * 10) != before)
                {
                    Version++;
                }
            }
        }
    }

    /// <summary>Jak je kus hotový (0 až 1).</summary>
    public static double Fraction(RackHolder holder, RackPiece piece)
    {
        var needed = holder == RackHolder.Fire ? GameData.Preserving.SmokeMinutes : GameData.Preserving.DryMinutes;
        return piece.Done ? 1 : Math.Min(1, piece.Progress / needed);
    }

    public List<RackSave> Export()
    {
        return All.Select(r => new RackSave(r.Key.Holder, r.Key.Id, r.Pieces.Select(p => new RackPieceSave(p.Item, p.Progress)).ToList())).ToList();
    }

    public void Restore(IEnumerable<RackSave> saved)
    {
        foreach (var rack in saved)
        {
            Rack(rack.Holder, rack.Id).AddRange(rack.Pieces.Select(p => new RackPiece(p.Item, p.Progress)));
        }

        Version++;
    }

    private List<RackPiece> Rack(RackHolder holder, int id)
    {
        if (!_racks.TryGetValue((holder, id), out var pieces))
        {
            pieces = [];
            _racks[(holder, id)] = pieces;
        }

        return pieces;
    }
}

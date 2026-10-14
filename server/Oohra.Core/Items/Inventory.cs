using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Plants;

namespace Oohra.Core.Items;

/// <summary>Hromádka stejných věcí v batohu.</summary>
public sealed record ItemStack(Item Item, int Count);

/// <summary>
/// Batoh (prototyp S.inv, addItem, count a takeKind v part2.js). Stejné věci se skládají na hromádku,
/// každá hromádka zabere jedno místo. Počet míst závisí na generaci.
/// </summary>
public sealed class Inventory
{
    private readonly List<ItemStack> _stacks = [];

    public Inventory(int capacity)
    {
        Capacity = capacity;
    }

    /// <summary>Batoh pro postavu dané generace.</summary>
    public static Inventory For(GenerationTraits traits)
    {
        return new Inventory(SlotsFor(traits.Generation));
    }

    /// <summary>Počet míst: základ, Noví každé dvě generace jedno navíc (prototyp 10 + min(3, g / 2)).</summary>
    public static int SlotsFor(int generation)
    {
        var rules = GameData.Gathering.Inventory;
        var g = Math.Min(generation, GameData.Generations.Cap);
        return rules.BaseSlots + Math.Min(rules.MaxBonusSlots, g / rules.GenerationsPerBonusSlot);
    }

    public int Capacity { get; }

    public IReadOnlyList<ItemStack> Stacks => _stacks;

    /// <summary>Přidá věc. Vrací false, když je batoh plný.</summary>
    public bool TryAdd(Item item)
    {
        var stackSize = GameData.Gathering.Inventory.StackSize;
        for (var i = 0; i < _stacks.Count; i++)
        {
            if (_stacks[i].Item == item && _stacks[i].Count < stackSize)
            {
                _stacks[i] = _stacks[i] with { Count = _stacks[i].Count + 1 };
                return true;
            }
        }

        if (_stacks.Count >= Capacity)
        {
            return false;
        }

        _stacks.Add(new ItemStack(item, 1));
        return true;
    }

    /// <summary>Přidá část rostliny.</summary>
    public bool TryAdd(PlantItem plant)
    {
        return TryAdd(Item.Of(plant));
    }

    /// <summary>Vezme jeden kus z hromádky (třeba k snědení).</summary>
    public Item? TakeOne(int stackIndex)
    {
        if (stackIndex < 0 || stackIndex >= _stacks.Count)
        {
            return null;
        }

        var stack = _stacks[stackIndex];
        if (stack.Count <= 1)
        {
            _stacks.RemoveAt(stackIndex);
        }
        else
        {
            _stacks[stackIndex] = stack with { Count = stack.Count - 1 };
        }

        return stack.Item;
    }

    /// <summary>Kolik kusů daného druhu je v batohu (třeba klacků).</summary>
    public int Count(ItemKind kind)
    {
        return _stacks.Where(s => s.Item.Kind == kind).Sum(s => s.Count);
    }

    /// <summary>Odebere počet kusů daného druhu. Když jich tolik není, neodebere nic a vrátí false.</summary>
    public bool TryRemove(ItemKind kind, int count)
    {
        if (Count(kind) < count)
        {
            return false;
        }

        for (var i = _stacks.Count - 1; i >= 0 && count > 0; i--)
        {
            if (_stacks[i].Item.Kind != kind)
            {
                continue;
            }

            var taken = Math.Min(count, _stacks[i].Count);
            count -= taken;
            if (taken == _stacks[i].Count)
            {
                _stacks.RemoveAt(i);
            }
            else
            {
                _stacks[i] = _stacks[i] with { Count = _stacks[i].Count - taken };
            }
        }

        return true;
    }

    /// <summary>
    /// Přesune hromádku do jiného batohu (nebo zásob), kolik se tam vejde. Vrací počet přesunutých kusů.
    /// </summary>
    public int MoveStack(int stackIndex, Inventory target)
    {
        if (stackIndex < 0 || stackIndex >= _stacks.Count)
        {
            return 0;
        }

        var stack = _stacks[stackIndex];
        var moved = 0;
        while (moved < stack.Count && target.TryAdd(stack.Item))
        {
            moved++;
        }

        if (moved == stack.Count)
        {
            _stacks.RemoveAt(stackIndex);
        }
        else if (moved > 0)
        {
            _stacks[stackIndex] = stack with { Count = stack.Count - moved };
        }

        return moved;
    }

    /// <summary>Zahodí celou hromádku.</summary>
    public void Drop(int stackIndex)
    {
        if (stackIndex >= 0 && stackIndex < _stacks.Count)
        {
            _stacks.RemoveAt(stackIndex);
        }
    }
}

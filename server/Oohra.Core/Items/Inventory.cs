using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Plants;

namespace Oohra.Core.Items;

/// <summary>Hromádka stejných věcí v batohu.</summary>
public sealed record ItemStack(PlantItem Item, int Count);

/// <summary>
/// Batoh (prototyp S.inv a addItem v part2.js). Stejné věci se skládají na hromádku,
/// každá hromádka zabere jedno místo. Počet míst závisí na generaci.
/// Zatím jen části rostlin, klacky, kameny a nástroje přijdou později.
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
    public bool TryAdd(PlantItem item)
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

    /// <summary>Vezme jeden kus z hromádky (třeba k snědení).</summary>
    public PlantItem? TakeOne(int stackIndex)
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

    /// <summary>Zahodí celou hromádku.</summary>
    public void Drop(int stackIndex)
    {
        if (stackIndex >= 0 && stackIndex < _stacks.Count)
        {
            _stacks.RemoveAt(stackIndex);
        }
    }
}

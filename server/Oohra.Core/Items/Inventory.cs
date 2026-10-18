using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Plants;

namespace Oohra.Core.Items;

/// <summary>Hromádka stejných věcí v batohu.</summary>
public sealed record ItemStack(Item Item, int Count);

/// <summary>
/// Věci postavy, zásoby nebo věci u těla (prototyp S.inv, addItem, count a takeKind v part2.js). Stejné věci
/// se skládají na hromádku, každá hromádka zabere jedno místo.
/// Postava (For) unese bez batohu jen to, co má v rukou (hands v crafting.json). Batoh nosí na zádech (Bag):
/// nezabírá místo a přidá místa, Noví k tomu bonus podle generace. Batoh, který dostane, se sám nasadí (lepší
/// vymění za horší, ten pak nese v rukou). Když batoh sundá, věci mu zůstanou, ale nic dalšího nepobere,
/// dokud jich nebude míň než míst.
/// </summary>
public sealed class Inventory
{
    private readonly List<ItemStack> _stacks = [];
    private readonly int _baseCapacity;
    private readonly bool _usesBags;
    private readonly int _bagBonus;

    public Inventory(int capacity)
    {
        _baseCapacity = capacity;
    }

    private Inventory(int hands, int bagBonus)
    {
        _baseCapacity = hands;
        _usesBags = true;
        _bagBonus = bagBonus;
    }

    /// <summary>Věci postavy dané generace: ruce, batoh přidá místa.</summary>
    public static Inventory For(GenerationTraits traits)
    {
        return new Inventory(GameData.Crafting.Hands, BagBonusFor(traits.Generation));
    }

    /// <summary>Kolik míst navíc má v batohu generace: Noví každé dvě generace jedno (prototyp min(3, g / 2)).</summary>
    public static int BagBonusFor(int generation)
    {
        var rules = GameData.Gathering.Inventory;
        var g = Math.Min(generation, GameData.Generations.Cap);
        return Math.Min(rules.MaxBonusSlots, g / rules.GenerationsPerBonusSlot);
    }

    /// <summary>Počet míst: u postavy ruce a batoh na zádech (s bonusem generace), jinak pevný.</summary>
    public int Capacity => _usesBags ? _baseCapacity + BagSlots(Bag) : _baseCapacity;

    /// <summary>Batoh na zádech (nezabírá místo), nebo null.</summary>
    public Item? Bag { get; private set; }

    public bool HasBag => Bag is not null;

    /// <summary>Všechny věci i s batohem na zádech (třeba k tělu po smrti).</summary>
    public IEnumerable<ItemStack> AllStacks => Bag is null ? _stacks : _stacks.Append(new ItemStack(Bag, 1));

    private int BagSlots(Item? bag)
    {
        return bag is not null && GameData.Crafting.BagSlots.TryGetValue(bag.Kind, out var slots) ? slots + _bagBonus : 0;
    }

    private static bool IsBag(Item item)
    {
        return GameData.Crafting.BagSlots.ContainsKey(item.Kind);
    }

    /// <summary>Nasadí batoh z hromádky na záda (dosavadní batoh dá do rukou, i kdyby se nevešel).</summary>
    public bool WearBag(int stackIndex)
    {
        if (!_usesBags || stackIndex < 0 || stackIndex >= _stacks.Count || !IsBag(_stacks[stackIndex].Item))
        {
            return false;
        }

        var bag = TakeOne(stackIndex)!;
        if (Bag is not null)
        {
            _stacks.Add(new ItemStack(Bag, 1));
        }

        Bag = bag;
        return true;
    }

    /// <summary>Sundá batoh ze zad do rukou (i kdyby se nevešel). Věcí bude víc než míst, dokud něco neodloží.</summary>
    public bool TakeOffBag()
    {
        if (Bag is null)
        {
            return false;
        }

        _stacks.Add(new ItemStack(Bag, 1));
        Bag = null;
        return true;
    }

    /// <summary>Batoh na zádech z uloženého světa.</summary>
    public void RestoreBag(Item? bag)
    {
        Bag = bag;
    }

    public IReadOnlyList<ItemStack> Stacks => _stacks;

    /// <summary>Vloží hromádky z uloženého světa tak, jak byly (bez skládání a kontroly místa).</summary>
    public void Restore(IEnumerable<ItemStack> stacks)
    {
        _stacks.AddRange(stacks);
    }

    /// <summary>Přidá věc. Batoh se sám nasadí, když je lepší než ten na zádech. Vrací false, když je plno.</summary>
    public bool TryAdd(Item item)
    {
        if (_usesBags && IsBag(item) && BagSlots(item) > BagSlots(Bag))
        {
            var old = Bag;
            Bag = item;
            if (old is not null && !TryAdd(old))
            {
                _stacks.Add(new ItemStack(old, 1));
            }

            return true;
        }

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

    /// <summary>
    /// Vymění jeden kus z hromádky za jinou věc (třeba opotřebovanou sekeru). Když je kus na hromádce sám,
    /// zůstane na stejném místě, ať se batoh nepřeházel. Vrací false, když se nová věc nevejde.
    /// </summary>
    public bool ReplaceOne(int stackIndex, Item replacement)
    {
        if (stackIndex < 0 || stackIndex >= _stacks.Count)
        {
            return false;
        }

        if (_stacks[stackIndex].Count == 1)
        {
            _stacks[stackIndex] = new ItemStack(replacement, 1);
            return true;
        }

        var original = TakeOne(stackIndex)!;
        if (TryAdd(replacement))
        {
            return true;
        }

        TryAdd(original);
        return false;
    }

    /// <summary>Kolik kusů dané věci se ještě vejde (do rozpracovaných hromádek a na volná místa).</summary>
    public int RoomFor(Item item)
    {
        var stackSize = GameData.Gathering.Inventory.StackSize;
        var room = Math.Max(0, Capacity - _stacks.Count) * stackSize;
        foreach (var stack in _stacks)
        {
            if (stack.Item == item)
            {
                room += Math.Max(0, stackSize - stack.Count);
            }
        }

        return room;
    }

    /// <summary>
    /// Opotřebuje první nástroj daného druhu o jedno použití (na stejném místě). Vrací true, když se rozpadl.
    /// </summary>
    public bool WearOne(ItemKind kind)
    {
        var index = _stacks.FindIndex(s => s.Item.Kind == kind);
        if (index < 0)
        {
            return false;
        }

        var tool = _stacks[index].Item;
        if (tool.Uses <= 1)
        {
            TakeOne(index);
            return true;
        }

        ReplaceOne(index, tool with { Uses = tool.Uses - 1 });
        return false;
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

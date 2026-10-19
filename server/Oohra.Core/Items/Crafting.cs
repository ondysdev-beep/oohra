using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Items;

/// <summary>
/// Kde postava stojí (u vody na činění kůže, u ohně), kdy (herní minuty, kvůli hnití kůže) a kde se zakládají
/// deníky (vyrobený deník dostane číslo, bez Journals má číslo 0). Ground: hromádky na zemi u postavy, ze kterých
/// se smí vyrábět (bez něj jen z batohu).
/// </summary>
public readonly record struct CraftPlace(bool NearWater, bool NearFire, double Now, Journals? Journals = null, GroundAccess? Ground = null);

/// <summary>
/// Věci na zemi u postavy (ground.json, craftReach): materiál z nich se bere jako z rukou (napřed ze země)
/// a výrobek, který se do rukou nevejde, se položí na zem. Nástroj musí postava držet.
/// </summary>
public sealed record GroundAccess(GroundItems Items, Island Island, double X, double Y, int Level, double Facing)
{
    public List<GroundPile> Nearby() => Items.Near(X, Y, Level, GameData.Ground.CraftReach);
}

/// <summary>
/// Výroba podle receptů (návrh 0.8, kap. 3, crafting.json). Co postava umí, se naučí zkoušením: spojí věci z batohu
/// (Combine), a když z nich jde recept, který ještě neumí, může se napoprvé nepovést (failChance) a kus materiálu
/// přijde vniveč. Povede-li se, recept umí (Character.Learn), smrtí ho zapomene. Pradávní znají základní recepty.
/// </summary>
public static class Crafting
{
    /// <summary>Vyrobí podle receptu (i neznámého, pak se může nepovést). Vrací výsledek a recept.</summary>
    public static CraftResult Make(string recipeId, Inventory inventory, Character maker, CraftPlace place)
    {
        var recipe = GameData.Crafting.Find(recipeId);
        return recipe is null ? CraftResult.NothingCame : Make(recipe, inventory, maker, place);
    }

    /// <summary>
    /// Zkusí spojit vybrané hromádky z batohu (a ze země u postavy, groundPileIds). Hledá recept, jehož materiály jsou přesně tyhle druhy věcí
    /// (nástroj receptu se do výběru nepočítá). Když jich jde víc (kopí i sekera jsou z klacku a kamene),
    /// má přednost recept, který postava ještě neumí (zkoušením se hledá nové), pak ten s víc materiálem,
    /// a jen takový, na který materiál má. Když žádný není, nic se nestane.
    /// </summary>
    public static (CraftResult Result, Recipe? Recipe) Combine(
        IReadOnlyList<int> stackIndices, Inventory inventory, Character maker, CraftPlace place, IReadOnlyList<int>? groundPileIds = null)
    {
        var kinds = stackIndices
            .Where(i => i >= 0 && i < inventory.Stacks.Count)
            .Select(i => inventory.Stacks[i].Item.Kind)
            .ToHashSet();
        if (groundPileIds is not null && place.Ground is { } ground)
        {
            kinds.UnionWith(ground.Nearby().Where(p => groundPileIds.Contains(p.Id)).Select(p => p.Item.Kind));
        }

        var stock = new Stock(inventory, place.Ground);
        var matching = GameData.Crafting.Recipes.Where(r => Matches(r, kinds)).ToList();
        if (matching.Count == 0)
        {
            return (CraftResult.NothingCame, null);
        }

        var affordable = matching.Where(r => r.Inputs.All(i => stock.Count(i.Key) >= i.Value)).ToList();
        var recipe = affordable
            .OrderBy(r => maker.Knows(r.Id))
            .ThenByDescending(r => r.Inputs.Values.Sum())
            .FirstOrDefault() ?? matching[0];
        return (Make(recipe, inventory, maker, place), recipe);
    }

    private static bool Matches(Recipe recipe, HashSet<ItemKind> kinds)
    {
        var wanted = recipe.Inputs.Keys.ToHashSet();
        if (recipe.Repairs is { } repaired)
        {
            wanted.Add(repaired);
        }

        var selected = new HashSet<ItemKind>(kinds);
        if (recipe.Tool is { } tool)
        {
            selected.Remove(tool);
        }

        return wanted.SetEquals(selected);
    }

    public static CraftResult Make(Recipe recipe, Inventory inventory, Character maker, CraftPlace place)
    {
        var stock = new Stock(inventory, place.Ground);
        var check = Check(recipe, stock, place);
        if (check is not null)
        {
            return check.Value;
        }

        var known = maker.Knows(recipe.Id);
        if (!known && maker.Rng.NextDouble() < GameData.Crafting.FailChance)
        {
            // Nepovedlo se: jeden kus některého materiálu přijde vniveč.
            var inputs = recipe.Inputs.Keys.ToList();
            stock.Take(inputs[maker.Rng.NextInt(inputs.Count)], place.Now);
            return CraftResult.Failed;
        }

        var made = recipe.Repairs is null ? Produce(recipe, stock, place) : Repair(recipe, stock, place.Now);
        if (made is not null)
        {
            return made.Value;
        }

        if (recipe.Tool is { } tool)
        {
            inventory.WearOne(tool);
        }

        maker.Practice(SkillKind.Craft, GameData.Skills.Practice.Craft);
        maker.Learn(recipe.Id);
        return known ? CraftResult.Made : CraftResult.Learned;
    }

    /// <summary>
    /// Ukáže jinému hráči, jak se recept dělá (návrh 0.8, kap. 3): ten ho pak umí bez zkoušení. Ukazovat smí jen
    /// ten, kdo recept umí, a jen blízko (teachReach v crafting.json).
    /// </summary>
    public static CraftResult Teach(Character teacher, Character learner, string recipeId, double distance)
    {
        if (!teacher.Knows(recipeId) || GameData.Crafting.Find(recipeId) is null)
        {
            return CraftResult.DontKnow;
        }

        if (learner.IsDead || distance > GameData.Crafting.TeachReach)
        {
            return CraftResult.TooFarToTeach;
        }

        if (learner.Knows(recipeId))
        {
            return CraftResult.AlreadyKnows;
        }

        learner.Learn(recipeId);
        return CraftResult.Taught;
    }

    /// <summary>Jde to vyrobit tady a z toho, co má? Vrací důvod, proč ne, nebo null.</summary>
    private static CraftResult? Check(Recipe recipe, Stock stock, CraftPlace place)
    {
        var inventory = stock.Inventory;
        if (recipe.Unique && recipe.Output is { } output && (inventory.Count(output) > 0 || inventory.Bag?.Kind == output))
        {
            return CraftResult.AlreadyHave;
        }

        if (recipe.Repairs is { } repaired && !inventory.Stacks.Any(s => s.Item.Kind == repaired && s.Item.Uses < recipe.MaxUses))
        {
            return CraftResult.NothingToRepair;
        }

        if (recipe.NearWater && !place.NearWater)
        {
            return CraftResult.NeedWater;
        }

        if (recipe.NearFire && !place.NearFire)
        {
            return CraftResult.NeedFire;
        }

        if (recipe.Inputs.Any(input => stock.Count(input.Key) < input.Value))
        {
            return CraftResult.NeedMaterials;
        }

        if (recipe.Tool is { } tool && inventory.Count(tool) == 0)
        {
            return CraftResult.NeedTool;
        }

        if (recipe.Inputs.ContainsKey(ItemKind.Hide) && stock.Items(ItemKind.Hide).All(item => Spoilage.IsSpoiled(item, place.Now)))
        {
            return CraftResult.Rotten;
        }

        return null;
    }

    /// <summary>
    /// Spotřebuje materiál a přidá výrobek. Když se výrobek nevejde do rukou, položí ho na zem (bez země materiál
    /// vrátí a výroba se nepovede).
    /// </summary>
    private static CraftResult? Produce(Recipe recipe, Stock stock, CraftPlace place)
    {
        var now = place.Now;
        var taken = new List<Item>();
        foreach (var (kind, count) in recipe.Inputs)
        {
            for (var i = 0; i < count; i++)
            {
                taken.Add(stock.Take(kind, now));
            }
        }

        var output = recipe.Output == ItemKind.Journal && place.Journals is not null
            ? place.Journals.Create()
            : new Item(recipe.Output!.Value, Uses: recipe.Uses);
        if (stock.Inventory.TryAdd(output))
        {
            return null;
        }

        if (place.Ground is { } ground)
        {
            ground.Items.Put(ground.Island, output, 1, ground.X, ground.Y, ground.Level, ground.Facing, now);
            return null;
        }

        foreach (var item in taken)
        {
            stock.Inventory.TryAdd(item);
        }

        return CraftResult.InventoryFull;
    }

    /// <summary>Oprava: nástroj dostane repairUses použití navíc (nejvýš maxUses), materiál se spotřebuje.</summary>
    private static CraftResult? Repair(Recipe recipe, Stock stock, double now)
    {
        var inventory = stock.Inventory;
        var kind = recipe.Repairs!.Value;
        var index = inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == kind && s.Item.Uses < recipe.MaxUses);
        var tool = inventory.Stacks[index].Item;
        inventory.ReplaceOne(index, tool with { Uses = Math.Min(recipe.MaxUses, tool.Uses + recipe.RepairUses) });
        foreach (var (input, count) in recipe.Inputs)
        {
            for (var i = 0; i < count; i++)
            {
                stock.Take(input, now);
            }
        }

        return null;
    }

    /// <summary>
    /// Materiál na výrobu: batoh a hromádky na zemi u postavy. Bere se napřed ze země (to je pracovní místo),
    /// u kůže napřed nezahnilá.
    /// </summary>
    private sealed class Stock
    {
        private readonly List<GroundPile> _nearby;
        private readonly GroundItems? _ground;

        public Stock(Inventory inventory, GroundAccess? ground)
        {
            Inventory = inventory;
            _ground = ground?.Items;
            _nearby = ground?.Nearby() ?? [];
        }

        public Inventory Inventory { get; }

        public int Count(ItemKind kind)
        {
            return Inventory.Count(kind) + _nearby.Where(p => p.Item.Kind == kind).Sum(p => p.Count);
        }

        /// <summary>Všechny druhy kusů daného druhu (po jednom za hromádku, stačí na kontrolu hnití).</summary>
        public IEnumerable<Item> Items(ItemKind kind)
        {
            return _nearby.Select(p => p.Item).Concat(Inventory.Stacks.Select(s => s.Item)).Where(i => i.Kind == kind);
        }

        /// <summary>Vezme jeden kus (musí ho mít, Count se kontroluje předem). Napřed nezkažený, napřed ze země.</summary>
        public Item Take(ItemKind kind, double now)
        {
            var pile = _nearby.FindIndex(p => p.Item.Kind == kind && !Spoilage.IsSpoiled(p.Item, now));
            var stack = Inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == kind && !Spoilage.IsSpoiled(s.Item, now));
            if (pile < 0 && stack < 0)
            {
                pile = _nearby.FindIndex(p => p.Item.Kind == kind);
                stack = Inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == kind);
            }

            if (pile < 0)
            {
                return Inventory.TakeOne(stack)!;
            }

            var item = _ground!.TakeOne(_nearby[pile].Id)!;
            _nearby[pile] = _nearby[pile] with { Count = _nearby[pile].Count - 1 };
            if (_nearby[pile].Count <= 0)
            {
                _nearby.RemoveAt(pile);
            }

            return item;
        }
    }
}

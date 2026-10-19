using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Items;

/// <summary>
/// Kde postava stojí (u vody na činění kůže, u ohně), kdy (herní minuty, kvůli hnití kůže) a kde se zakládají
/// deníky (vyrobený deník dostane číslo, bez Journals má číslo 0).
/// </summary>
public readonly record struct CraftPlace(bool NearWater, bool NearFire, double Now, Journals? Journals = null);

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
    /// Zkusí spojit vybrané hromádky z batohu. Hledá recept, jehož materiály jsou přesně tyhle druhy věcí
    /// (nástroj receptu se do výběru nepočítá). Když jich jde víc (kopí i sekera jsou z klacku a kamene),
    /// má přednost recept, který postava ještě neumí (zkoušením se hledá nové), pak ten s víc materiálem,
    /// a jen takový, na který materiál má. Když žádný není, nic se nestane.
    /// </summary>
    public static (CraftResult Result, Recipe? Recipe) Combine(IReadOnlyList<int> stackIndices, Inventory inventory, Character maker, CraftPlace place)
    {
        var kinds = stackIndices
            .Where(i => i >= 0 && i < inventory.Stacks.Count)
            .Select(i => inventory.Stacks[i].Item.Kind)
            .ToHashSet();
        var matching = GameData.Crafting.Recipes.Where(r => Matches(r, kinds)).ToList();
        if (matching.Count == 0)
        {
            return (CraftResult.NothingCame, null);
        }

        var affordable = matching.Where(r => r.Inputs.All(i => inventory.Count(i.Key) >= i.Value)).ToList();
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
        var check = Check(recipe, inventory, place);
        if (check is not null)
        {
            return check.Value;
        }

        var known = maker.Knows(recipe.Id);
        if (!known && maker.Rng.NextDouble() < GameData.Crafting.FailChance)
        {
            // Nepovedlo se: jeden kus některého materiálu přijde vniveč.
            var inputs = recipe.Inputs.Keys.ToList();
            inventory.TryRemove(inputs[maker.Rng.NextInt(inputs.Count)], 1);
            return CraftResult.Failed;
        }

        var made = recipe.Repairs is null ? Produce(recipe, inventory, place) : Repair(recipe, inventory);
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
    private static CraftResult? Check(Recipe recipe, Inventory inventory, CraftPlace place)
    {
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

        if (recipe.Inputs.Any(input => inventory.Count(input.Key) < input.Value))
        {
            return CraftResult.NeedMaterials;
        }

        if (recipe.Tool is { } tool && inventory.Count(tool) == 0)
        {
            return CraftResult.NeedTool;
        }

        if (recipe.Inputs.ContainsKey(ItemKind.Hide) && inventory.Stacks.Where(s => s.Item.Kind == ItemKind.Hide).All(s => Spoilage.IsSpoiled(s.Item, place.Now)))
        {
            return CraftResult.Rotten;
        }

        return null;
    }

    /// <summary>Spotřebuje materiál a přidá výrobek. Když se výrobek nevejde, materiál vrátí.</summary>
    private static CraftResult? Produce(Recipe recipe, Inventory inventory, CraftPlace place)
    {
        var now = place.Now;
        var taken = new List<Item>();
        foreach (var (kind, count) in recipe.Inputs)
        {
            for (var i = 0; i < count; i++)
            {
                var index = BestStack(inventory, kind, now);
                taken.Add(inventory.TakeOne(index)!);
            }
        }

        var output = recipe.Output == ItemKind.Journal && place.Journals is not null
            ? place.Journals.Create()
            : new Item(recipe.Output!.Value, Uses: recipe.Uses);
        if (inventory.TryAdd(output))
        {
            return null;
        }

        foreach (var item in taken)
        {
            inventory.TryAdd(item);
        }

        return CraftResult.InventoryFull;
    }

    /// <summary>Oprava: nástroj dostane repairUses použití navíc (nejvýš maxUses), materiál se spotřebuje.</summary>
    private static CraftResult? Repair(Recipe recipe, Inventory inventory)
    {
        var kind = recipe.Repairs!.Value;
        var index = inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == kind && s.Item.Uses < recipe.MaxUses);
        var tool = inventory.Stacks[index].Item;
        inventory.ReplaceOne(index, tool with { Uses = Math.Min(recipe.MaxUses, tool.Uses + recipe.RepairUses) });
        foreach (var (input, count) in recipe.Inputs)
        {
            inventory.TryRemove(input, count);
        }

        return null;
    }

    /// <summary>Hromádka, ze které se bere: u kůže ta nezahnilá, jinak první.</summary>
    private static int BestStack(Inventory inventory, ItemKind kind, double now)
    {
        var stacks = inventory.Stacks.ToList();
        var fresh = stacks.FindIndex(s => s.Item.Kind == kind && !Spoilage.IsSpoiled(s.Item, now));
        return fresh >= 0 ? fresh : stacks.FindIndex(s => s.Item.Kind == kind);
    }
}

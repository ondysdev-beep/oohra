using Oohra.Core.Items;
using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Ruce, batohy, recepty a zdroje materiálu z Data/crafting.json (návrh 0.8, kap. 3).</summary>
public sealed record CraftingRules(
    int Hands,
    Dictionary<ItemKind, int> BagSlots,
    double FailChance,
    List<Recipe> Recipes,
    int JournalPages,
    double TeachReach,
    double FlintChance,
    Dictionary<TreeKind, int> BastPerMeter,
    Dictionary<AnimalKind, CarcassRules> Carcass,
    double HideRotDays,
    double AshAfterMinutes)
{
    public Recipe? Find(string id)
    {
        return Recipes.Find(r => r.Id == id);
    }

    public int IndexOf(Recipe recipe)
    {
        return Recipes.IndexOf(recipe);
    }
}

/// <summary>
/// Recept: co se spotřebuje (Inputs), čím se pracuje (Tool, opotřebuje se), co vznikne (Output s Uses),
/// nebo jaký nástroj se opraví (Repairs, o RepairUses, nejvýš do MaxUses). NearWater a NearFire: kde.
/// </summary>
public sealed record Recipe(
    string Id,
    string Name,
    Dictionary<ItemKind, int> Inputs,
    ItemKind? Output = null,
    int Uses = 0,
    ItemKind? Tool = null,
    ItemKind? Repairs = null,
    int RepairUses = 0,
    int MaxUses = 0,
    bool NearWater = false,
    bool NearFire = false,
    bool Unique = false,
    bool KnownByAncients = false);

/// <summary>Co dá zvíře kromě masa: kůže a kosti.</summary>
public sealed record CarcassRules(int Hides, int Bones);

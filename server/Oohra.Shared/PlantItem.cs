using Oohra.Core.World;

namespace Oohra.Core.Plants;

/// <summary>Zralost plodu. U listu a kořene se nepoužívá.</summary>
public enum Ripeness
{
    Unripe = 0,
    Ripe = 1,
    Overripe = 2,
}

/// <summary>Úprava jídla.</summary>
public enum Cooking
{
    Raw = 0,
    Baked = 1,
    Boiled = 2,

    /// <summary>Uzené maso nebo ryba (v kouři ohně).</summary>
    Smoked = 3,

    /// <summary>Sušené maso nebo ryba (na sušáku).</summary>
    Dried = 4,
}

/// <summary>
/// Utržená část rostliny, jak ji má hráč v batohu: odkud je a jak je upravená.
/// Tohle hráč vidí. Co v ní je (složení), počítá jen server. Day: herní den, kdy byl plod utržen
/// (plod se časem zkazí, plody z různých dnů se neskládají na jednu hromádku).
/// </summary>
public sealed record PlantItem(
    int SpeciesId,
    PlantPart Part,
    Ripeness Ripeness,
    Season Season,
    TileType Soil,
    Cooking Cooking = Cooking.Raw,
    int BredLevel = 0,
    int Day = 0);

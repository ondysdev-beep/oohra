namespace Oohra.Core.Data;

/// <summary>Pravidla sběru, batohu a pití z Data/gathering.json.</summary>
public sealed record GatheringRules(RegrowRules Regrow, InventoryRules Inventory, ReachRules Reach, WaterRules Water);

public sealed record RegrowRules(double LeafMinutes, double FruitMinutes, double RootMinutes);

public sealed record InventoryRules(int MaxBonusSlots, int GenerationsPerBonusSlot, int StackSize);

public sealed record ReachRules(double Plant, double Water);

public sealed record WaterRules(
    double CleanHydration,
    double MuddyHydration,
    int InstinctFromGeneration,
    double MuddyHydrationInstinct,
    double SicknessChance,
    double SicknessChanceInstinct,
    double SicknessDelayMin,
    double SicknessDelayRange,
    double SicknessStrengthMin,
    double SicknessStrengthRange,
    double DiarrheaChance,
    double DiarrheaDelayMin,
    double DiarrheaDelayRange,
    double DiarrheaStrength,
    double SaltyHydration);

namespace Oohra.Core.Data;

/// <summary>Pravidla generací z Data/generations.json.</summary>
public sealed record GenerationRules(
    int Cap,
    GenerationValues Ancient,
    GenerationValues NewPerGeneration,
    int ColdGenerationCap,
    double SpeedMultiplierMax);

public sealed record GenerationValues(
    double ToxinMultiplier,
    double HealMultiplier,
    double EnergyDrainMultiplier,
    double ColdBonus,
    double SpeedMultiplier);

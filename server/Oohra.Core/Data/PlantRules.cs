using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Pravidla změn složení rostliny z Data/plant_rules.json.</summary>
public sealed class PlantRules
{
    public required Dictionary<TileType, double[]> Soil { get; init; }

    public required double UnripeNutrition { get; init; }

    public required double DefaultUnripeIrritant { get; init; }

    public required double OverripeNutrition { get; init; }

    public required double OverripeIrritant { get; init; }

    public required double BoiledIrritant { get; init; }

    public required double BoiledToxin { get; init; }

    public required double BreedingToxinPerLevel { get; init; }

    public required double BreedingNeurotoxinPerLevel { get; init; }

    public required double BreedingNutritionPerLevel { get; init; }
}

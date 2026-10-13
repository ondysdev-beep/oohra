using Oohra.Core.Data;
using Oohra.Core.Plants;

namespace Oohra.Core.Secret;

/// <summary>
/// Spočítá skryté složení utržené části rostliny (přepis comp z prototype/src/part2.js).
/// Postup: základ archetypu, roční období, zralost plodu, půda, odchylky druhu, vaření, šlechtění.
/// </summary>
public static class CompositionCalculator
{
    private const int Nutrition = 0;
    private const int Irritant = 1;
    private const int Toxin = 2;
    private const int Neurotoxin = 3;

    public static Composition Compute(PlantItem item, SpeciesSecret species)
    {
        var archetype = GameData.Archetypes[species.ArchetypeIndex];
        var rules = GameData.PlantRules;

        var v = archetype.BaseFor(item.Part);
        MultiplyBy(v, archetype.SeasonalFor(item.Part, item.Season));
        if (item.Part == PlantPart.Fruit)
        {
            ApplyRipeness(v, item.Ripeness, archetype, rules);
        }

        MultiplyBy(v, rules.Soil.GetValueOrDefault(item.Soil));
        MultiplyBy(v, species.JitterFor(item.Part));
        ApplyCooking(v, item.Cooking, archetype, rules);
        ApplyBreeding(v, item.BredLevel, rules);

        return Composition.FromArray(v);
    }

    private static void ApplyRipeness(double[] v, Ripeness ripeness, Archetype archetype, PlantRules rules)
    {
        if (ripeness == Ripeness.Unripe)
        {
            v[Nutrition] *= rules.UnripeNutrition;
            v[Irritant] += archetype.UnripeIrritant ?? rules.DefaultUnripeIrritant;
        }
        else if (ripeness == Ripeness.Overripe)
        {
            v[Nutrition] *= rules.OverripeNutrition;
            v[Irritant] += rules.OverripeIrritant;
        }
    }

    private static void ApplyCooking(double[] v, Cooking cooking, Archetype archetype, PlantRules rules)
    {
        if (cooking == Cooking.Raw)
        {
            return;
        }

        MultiplyBy(v, archetype.Cook);
        if (cooking == Cooking.Boiled)
        {
            v[Irritant] *= rules.BoiledIrritant;
            v[Toxin] *= rules.BoiledToxin;
        }
    }

    private static void ApplyBreeding(double[] v, int level, PlantRules rules)
    {
        if (level == 0)
        {
            return;
        }

        v[Toxin] *= 1 - rules.BreedingToxinPerLevel * level;
        v[Neurotoxin] *= 1 - rules.BreedingNeurotoxinPerLevel * level;
        v[Nutrition] *= 1 + rules.BreedingNutritionPerLevel * level;
    }

    private static void MultiplyBy(double[] v, double[]? factors)
    {
        if (factors is null)
        {
            return;
        }

        for (var k = 0; k < v.Length; k++)
        {
            v[k] *= factors[k];
        }
    }
}

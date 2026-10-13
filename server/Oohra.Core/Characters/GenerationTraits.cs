using Oohra.Core.Data;

namespace Oohra.Core.Characters;

/// <summary>
/// Skryté vlastnosti podle generace (prototyp G()): Pradávný (0) je odolný a má železný žaludek,
/// Noví jsou citlivější na jed, ale hojí se rychleji a jsou o něco rychlejší. Hodnoty z Data/generations.json.
/// </summary>
public sealed record GenerationTraits(
    int Generation,
    double ToxinMultiplier,
    double HealMultiplier,
    double EnergyDrainMultiplier,
    double ColdBonus,
    double SpeedMultiplier)
{
    public bool IsAncient => Generation == 0;

    public static GenerationTraits For(int generation)
    {
        var rules = GameData.Generations;
        if (generation == 0)
        {
            var a = rules.Ancient;
            return new GenerationTraits(0, a.ToxinMultiplier, a.HealMultiplier, a.EnergyDrainMultiplier, a.ColdBonus, a.SpeedMultiplier);
        }

        var g = Math.Min(generation, rules.Cap);
        var p = rules.NewPerGeneration;
        return new GenerationTraits(
            generation,
            1 + p.ToxinMultiplier * g,
            1 + p.HealMultiplier * g,
            1 + p.EnergyDrainMultiplier * g,
            p.ColdBonus * Math.Min(g, rules.ColdGenerationCap),
            Math.Min(rules.SpeedMultiplierMax, 1 + p.SpeedMultiplier * g));
    }
}

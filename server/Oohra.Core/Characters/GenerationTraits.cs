using Oohra.Core.Data;

namespace Oohra.Core.Characters;

/// <summary>
/// Skryté vlastnosti podle generace (prototyp G()): Pradávný (0) je odolný a má železný žaludek,
/// Noví jsou citlivější na jed, ale hojí se rychleji, jsou o něco rychlejší a rychleji se učí. Hodnoty z Data/generations.json.
/// </summary>
public sealed record GenerationTraits(
    int Generation,
    double ToxinMultiplier,
    double HealMultiplier,
    double EnergyDrainMultiplier,
    double ColdBonus,
    double SpeedMultiplier,
    double LearnMultiplier)
{
    public bool IsAncient => Generation == 0;

    /// <summary>Generace po smrti: vždy Nový, o jednu výš, nejvýš strop (prototyp nextGen).</summary>
    public static int NextAfterDeath(int generation)
    {
        return Math.Min(GameData.Generations.Cap, Math.Max(1, generation + 1));
    }

    public static GenerationTraits For(int generation)
    {
        var rules = GameData.Generations;
        if (generation == 0)
        {
            var a = rules.Ancient;
            return new GenerationTraits(0, a.ToxinMultiplier, a.HealMultiplier, a.EnergyDrainMultiplier, a.ColdBonus, a.SpeedMultiplier, a.LearnMultiplier);
        }

        var g = Math.Min(generation, rules.Cap);
        var p = rules.NewPerGeneration;
        return new GenerationTraits(
            generation,
            1 + p.ToxinMultiplier * g,
            1 + p.HealMultiplier * g,
            1 + p.EnergyDrainMultiplier * g,
            p.ColdBonus * Math.Min(g, rules.ColdGenerationCap),
            Math.Min(rules.SpeedMultiplierMax, 1 + p.SpeedMultiplier * g),
            1 + p.LearnMultiplier * g);
    }
}

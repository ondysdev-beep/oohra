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
    double LearnMultiplier,
    double WoundMultiplier,
    double SpoiledMultiplier)
{
    public bool IsAncient => Generation == 0;

    /// <summary>
    /// Generace po smrti podle prototypu (nextGen): vždy Nový, o jednu výš, nejvýš strop. Server ji nepoužívá,
    /// po smrti se rodí podle generace světa (WorldGeneration), aby se evoluce nedala nafarmit umíráním.
    /// </summary>
    public static int NextAfterDeath(int generation)
    {
        return Math.Min(GameData.Generations.Cap, Math.Max(1, generation + 1));
    }

    /// <summary>
    /// Generace světa (návrh 8.2): počítadlo patří světu, ne hráči. Roste o jednu každých daysPerGeneration
    /// skutečných dní od vzniku světa (asi půl roku). Prvních půl roku je 0 (Pradávní), pak 1 (Noví), pak 2 (třetí
    /// generace) a tak dál, nejvýš strop. Kdo do světa přijde poprvé, narodí se jako tahle generace.
    /// </summary>
    public static int WorldGeneration(double realDaysSinceStart)
    {
        var rules = GameData.Generations;
        var passed = (int)Math.Floor(Math.Max(0, realDaysSinceStart) / rules.DaysPerGeneration);
        return Math.Min(rules.Cap, passed);
    }

    /// <summary>
    /// Generace po smrti: generace světa, ale aspoň Nový. Status Pradávného se smrtí ztrácí navždy (návrh 8.6),
    /// i když svět je ještě v době Pradávných.
    /// </summary>
    public static int AfterDeath(int worldGeneration)
    {
        return Math.Max(1, worldGeneration);
    }

    public static GenerationTraits For(int generation)
    {
        var rules = GameData.Generations;
        if (generation == 0)
        {
            var a = rules.Ancient;
            return new GenerationTraits(0, a.ToxinMultiplier, a.HealMultiplier, a.EnergyDrainMultiplier, a.ColdBonus, a.SpeedMultiplier, a.LearnMultiplier, a.WoundMultiplier, a.SpoiledMultiplier);
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
            1 + p.LearnMultiplier * g,
            1 + p.WoundMultiplier * g,
            1 + p.SpoiledMultiplier * g);
    }
}

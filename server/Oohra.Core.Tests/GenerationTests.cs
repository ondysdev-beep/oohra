using Oohra.Core.Characters;
using Oohra.Core.Data;

namespace Oohra.Core.Tests;

/// <summary>Hodnoty Pradávných a Nových podle návrhu 8.4 a prototypu G().</summary>
public class GenerationTests
{
    [Fact]
    public void AncientHasIronStomachAndColdResistance()
    {
        var t = GenerationTraits.For(0);

        Assert.True(t.IsAncient);
        Assert.Equal(0.6, t.ToxinMultiplier, 9);
        Assert.Equal(1.05, t.HealMultiplier, 9);
        Assert.Equal(7, t.ColdBonus, 9);
        Assert.Equal(1, t.EnergyDrainMultiplier, 9);
    }

    [Fact]
    public void NewGenerationIsMoreSensitive()
    {
        var t = GenerationTraits.For(1);

        Assert.False(t.IsAncient);
        Assert.Equal(1.04, t.ToxinMultiplier, 9);
        Assert.Equal(-1.3, t.ColdBonus, 9);
    }

    [Fact]
    public void ValuesStopAtCap()
    {
        var capped = GenerationTraits.For(6);
        var beyond = GenerationTraits.For(20);

        Assert.Equal(capped.ToxinMultiplier, beyond.ToxinMultiplier);
        Assert.Equal(1.24, beyond.ToxinMultiplier, 9);
        Assert.Equal(0.82, beyond.EnergyDrainMultiplier, 9);
        Assert.Equal(-1.3 * 4, beyond.ColdBonus, 9);
    }

    /// <summary>
    /// Generace světa (návrh 8.2): prvních půl roku skutečného času Pradávní (0), pak Noví (1), pak třetí generace (2),
    /// roste s časem, ne smrtí, a zastaví se na stropu.
    /// </summary>
    [Fact]
    public void WorldGenerationGrowsWithRealTime()
    {
        var days = GameData.Generations.DaysPerGeneration;

        Assert.Equal(182.5, days);
        Assert.Equal(0, GenerationTraits.WorldGeneration(0));
        Assert.Equal(0, GenerationTraits.WorldGeneration(days - 0.01));
        Assert.Equal(1, GenerationTraits.WorldGeneration(days));
        Assert.Equal(2, GenerationTraits.WorldGeneration(days * 2.5));
        Assert.Equal(GameData.Generations.Cap, GenerationTraits.WorldGeneration(days * 100));
    }

    /// <summary>Po smrti se status Pradávného ztrácí (8.6): i v době Pradávných se narodí Nový. Později generace světa.</summary>
    [Fact]
    public void AfterDeathAtLeastNew()
    {
        Assert.Equal(1, GenerationTraits.AfterDeath(0));
        Assert.Equal(1, GenerationTraits.AfterDeath(1));
        Assert.Equal(3, GenerationTraits.AfterDeath(3));
    }
}

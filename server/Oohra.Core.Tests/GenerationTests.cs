using Oohra.Core.Characters;

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
}

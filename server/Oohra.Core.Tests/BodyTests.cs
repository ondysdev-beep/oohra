using Oohra.Core.Characters;
using Oohra.Core.Data;
using static Oohra.Core.Tests.TestPlants;

namespace Oohra.Core.Tests;

/// <summary>Potřeby, uzdravování a smrt (prototyp physiology, part4.js).</summary>
public class BodyTests
{
    [Fact]
    public void NewCharacterStartsWithValuesFromData()
    {
        var c = Person();
        var start = GameData.Body.Start;

        Assert.Equal(start.Satiety, c.Satiety);
        Assert.Equal(start.Hydration, c.Hydration);
        Assert.Equal(100, c.Health);
        Assert.Empty(c.Symptoms());
    }

    [Fact]
    public void HungerAndThirstGrowWithTime()
    {
        var c = Person();
        var clock = Clock();

        Physiology.Run(c, clock, 60);

        Assert.Equal(70 - 60 * 100.0 / 3600, c.Satiety, 6);
        Assert.Equal(75 - 60 * 100.0 / 1728, c.Hydration, 6);
    }

    [Fact]
    public void ThirstKillsAndIsNamedAsCause()
    {
        var c = Person();
        var clock = Clock();
        c.Hydration = 0;

        Physiology.Run(c, clock, 3 * 1440);

        Assert.True(c.IsDead);
        Assert.Equal(DeathCause.Thirst, c.DeathCause);
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.Died);
    }

    [Fact]
    public void HealthReturnsWhenWellFedAndWarm()
    {
        var c = Person();
        var clock = Clock();
        c.Health = 50;
        c.Satiety = 100;
        c.Hydration = 100;

        Physiology.Run(c, clock, 60);

        Assert.True(c.Health > 50);
    }

    [Fact]
    public void ToxinFadesSlowly()
    {
        var c = Person();
        var clock = Clock();
        c.Toxin = 10;

        Physiology.Run(c, clock, 100);

        Assert.Equal(10 - 100 * 0.03, c.Toxin, 6);
    }

    [Fact]
    public void HighToxinCausesFeverAndFeverIsReported()
    {
        var c = Person();
        var clock = Clock();
        c.Toxin = 100;

        Physiology.Run(c, clock, 300);

        Assert.True(c.Fever > 30);
        Assert.Contains(Symptom.Fever, c.Symptoms());
        Assert.Contains(Symptom.HeavyBody, c.Symptoms());
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.FeverStarted);
    }

    [Fact]
    public void AncientIsWarmerThanLaterGenerations()
    {
        var ancient = Person(generation: 0);
        var later = Person(generation: 4);
        var clock = Clock();

        Physiology.Run(ancient, clock, 600);
        var clock2 = Clock();
        Physiology.Run(later, clock2, 600);

        Assert.True(ancient.Warmth > later.Warmth);
    }
}

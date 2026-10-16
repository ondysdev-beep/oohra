using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Rng;
using Oohra.Core.Time;

namespace Oohra.Core.Tests;

/// <summary>Nováček v zimě (návrh 4.1): rodí se v teple, první hodiny mu zima neubližuje a má instinkt na zimu.</summary>
public class NewcomerWarmthTests
{
    // Pozdější generace snáší zimu hůř (teplo těla v zimní noci klesá pod hranici, kdy zima škodí).
    private const int ColdGeneration = 3;

    [Fact]
    public void NewcomerIsWarmFirstHoursThenColdHurts()
    {
        var clock = WinterNight();
        var c = Frozen(clock);
        c.BornAsNewcomer(clock.TotalMinutes);

        Physiology.Run(c, clock, 60);

        Assert.True(c.Warmth > GameData.Body.Damage.ColdBelow);
        Assert.False(c.Blame.ContainsKey(DeathCause.Cold));
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.ColdInstinct);

        // Po prvních hodinách už zima ubližuje (bez ohně a úkrytu).
        Physiology.Run(c, clock, GameData.Body.Warmth.Newborn.WarmHours * 60);
        c.Warmth = 5;
        Physiology.Run(c, clock, 30);
        Assert.True(c.Blame.ContainsKey(DeathCause.Cold));
    }

    /// <summary>Kdo se nenarodil jako nováček (další životy), tomu zima ubližuje hned a instinkt nemá.</summary>
    [Fact]
    public void OthersGetNoProtection()
    {
        var clock = WinterNight();
        var c = Frozen(clock);

        Physiology.Run(c, clock, 30);

        Assert.True(c.Blame.ContainsKey(DeathCause.Cold));
        Assert.DoesNotContain(c.TakeMessages(), m => m.Event == BodyEvent.ColdInstinct);
    }

    /// <summary>Instinkt se neozývá pořád, nejvýš jednou za instinctEveryMinutes.</summary>
    [Fact]
    public void InstinctIsNotSpam()
    {
        var clock = WinterNight();
        var c = Frozen(clock);
        c.BornAsNewcomer(clock.TotalMinutes);
        var every = GameData.Body.Warmth.Newborn.InstinctEveryMinutes;

        Physiology.Run(c, clock, every * 3);

        var warnings = c.TakeMessages().Count(m => m.Event == BodyEvent.ColdInstinct);
        Assert.InRange(warnings, 3, 4);
    }

    private static Character Frozen(GameClock clock)
    {
        return new Character(ColdGeneration, new Mulberry32(1)) { Warmth = 5 };
    }

    private static GameClock WinterNight()
    {
        var clock = GameClock.FromSettings(22 * 60);
        // Zima je poslední čtvrtina roku (měsíce 9 až 11).
        clock.Restore(22 * 60, 9.5);
        return clock;
    }
}

using Oohra.Core.Data;
using Oohra.Core.Time;

namespace Oohra.Core.Tests;

public class ClockTests
{
    [Fact]
    public void SettingsHaveAgreedTimes()
    {
        var settings = GameData.Settings;

        Assert.Equal(30, settings.TicksPerSecond);
        Assert.Equal(90 * 60, settings.DayLengthSeconds);
        Assert.Equal(4.5 * 24 * 3600, settings.MonthLengthSeconds * settings.MonthsPerYear);
    }

    [Fact]
    public void NinetyRealMinutesAreOneGameDay()
    {
        var clock = new GameClock(0, 5400, 32400, 12);

        var minutes = clock.Advance(5400);

        Assert.Equal(1440, minutes, 9);
        Assert.Equal(1, clock.Day);
    }

    [Fact]
    public void SeasonFollowsCalendar()
    {
        var clock = new GameClock(0, 5400, 32400, 12);
        Assert.Equal(Season.Spring, clock.Season);

        clock.Advance(3 * 32400);
        Assert.Equal(Season.Summer, clock.Season);

        clock.Advance(6 * 32400);
        Assert.Equal(Season.Winter, clock.Season);

        clock.Advance(3 * 32400);
        Assert.Equal(Season.Spring, clock.Season);
        Assert.Equal(1, clock.Year);
    }

    [Fact]
    public void DaylightIsFullAtNoonAndZeroAtMidnight()
    {
        Assert.Equal(1, new GameClock(12 * 60, 5400, 32400, 12).Daylight, 9);
        Assert.Equal(0, new GameClock(0, 5400, 32400, 12).Daylight, 9);
    }

    [Fact]
    public void RealSecondsAndGameMinutesConvertBothWays()
    {
        var clock = new GameClock(0, 5400, 32400, 12);

        Assert.Equal(123, clock.GameMinutesFor(clock.RealSecondsFor(123)), 9);
    }
}

using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

public class WeatherTests
{
    private const int Seed = 12345;

    [Fact]
    public void SameSeedAndTimeGiveSameWeather()
    {
        for (var minute = 0.0; minute < 5000; minute += 37)
        {
            Assert.Equal(Weather.At(Seed, minute, Season.Autumn), Weather.At(Seed, minute, Season.Autumn));
        }
    }

    [Fact]
    public void FogIsMostCommonInTheMorning()
    {
        var morning = Share(WeatherKind.Fog, Season.Spring, hour: 8);
        var day = Share(WeatherKind.Fog, Season.Spring, hour: 14);

        Assert.True(morning > 0.4, $"ráno mlha {morning}");
        Assert.True(day > 0.05 && day < 0.2, $"přes den mlha {day}");
    }

    [Fact]
    public void RainIsRarerThanFogAndStormIsRarest()
    {
        var fog = Share(WeatherKind.Fog, Season.Spring);
        var rain = Share(WeatherKind.Rain, Season.Spring) + Share(WeatherKind.Storm, Season.Spring);
        var storm = Share(WeatherKind.Storm, Season.Spring);

        Assert.True(rain > 0.1 && rain < fog, $"déšť {rain}, mlha {fog}");
        Assert.True(storm > 0.01 && storm < rain / 3, $"bouřka {storm}");
    }

    [Fact]
    public void AutumnIsWetterThanSummer()
    {
        var summer = Share(WeatherKind.Rain, Season.Summer);
        var autumn = Share(WeatherKind.Rain, Season.Autumn);

        Assert.True(autumn > summer * 1.5, $"podzim {autumn}, léto {summer}");
    }

    [Fact]
    public void WeatherFadesInAtTheStartOfASegment()
    {
        var length = GameData.Weather.SegmentMinutes;
        var segment = FindSegment(s => Weather.KindOf(Seed, s, Season.Spring) == WeatherKind.Rain
            && Weather.KindOf(Seed, s - 1, Season.Spring) == WeatherKind.Clear);

        var start = Weather.At(Seed, segment * length + 1, Season.Spring);
        var middle = Weather.At(Seed, segment * length + length / 2, Season.Spring);

        Assert.Equal(WeatherKind.Rain, start.Kind);
        Assert.True(start.Intensity < 0.05, $"začátek {start.Intensity}");
        Assert.True(middle.Intensity > 0.5, $"prostředek {middle.Intensity}");
        Assert.True(middle.Wet);
    }

    [Fact]
    public void RainMakesItColderAndFiresBurnFaster()
    {
        var rain = new WeatherState(WeatherKind.Rain, 1, 0.5);
        var storm = new WeatherState(WeatherKind.Storm, 1, 1);

        Assert.Equal(0, Weather.WarmthPenalty(WeatherState.Calm));
        Assert.Equal(GameData.Weather.RainWarmthPenalty, Weather.WarmthPenalty(rain));
        Assert.True(Weather.WarmthPenalty(storm) > Weather.WarmthPenalty(rain));
        Assert.Equal(1, Weather.BurnMultiplier(new WeatherState(WeatherKind.Fog, 1, 0.1)));
        Assert.Equal(GameData.Weather.StormBurnMultiplier, Weather.BurnMultiplier(storm));
    }

    /// <summary>Podíl úseků s daným počasím (volitelně jen úseky začínající v danou hodinu).</summary>
    private static double Share(WeatherKind kind, Season season, int? hour = null)
    {
        var perDay = (int)(1440 / GameData.Weather.SegmentMinutes);
        int count = 0, total = 0;
        for (var segment = 0; segment < 40000; segment++)
        {
            var startHour = segment % perDay * GameData.Weather.SegmentMinutes / 60;
            if (hour is { } h && startHour != h)
            {
                continue;
            }

            total++;
            if (Weather.KindOf(Seed, segment, season) == kind)
            {
                count++;
            }
        }

        return (double)count / total;
    }

    private static int FindSegment(Func<int, bool> match)
    {
        for (var segment = 1; segment < 10000; segment++)
        {
            if (match(segment))
            {
                return segment;
            }
        }

        throw new InvalidOperationException("úsek nenalezen");
    }
}

using Oohra.Core.Data;
using Oohra.Core.Rng;

namespace Oohra.Core.World;

/// <summary>Počasí v jednu chvíli: druh, síla 0 až 1 (rozjíždí se a doznívá) a vítr 0 až 1.</summary>
public readonly record struct WeatherState(WeatherKind Kind, double Intensity, double Wind)
{
    public static readonly WeatherState Calm = new(WeatherKind.Clear, 0, 0.2);

    /// <summary>Prší (déšť i bouřka)?</summary>
    public bool Wet => Kind is WeatherKind.Rain or WeatherKind.Storm && Intensity > 0;
}

/// <summary>
/// Počasí na ostrově (ponurý svět: mlha, déšť, bouřky). Čas se dělí na úseky (segmentMinutes) a pro každý úsek
/// se ze seedu světa vylosuje počasí: mlha nejčastěji ráno, déšť méně často, bouřka výjimečně, podle ročního období.
/// Na začátku úseku se počasí rozjíždí a na konci doznívá, pokud další úsek je jiný. Žádný stav: stejný seed a čas
/// dají stejné počasí, takže se nemusí ukládat a všichni hráči vidí totéž.
/// </summary>
public static class Weather
{
    public static WeatherState At(int worldSeed, double totalMinutes, Season season)
    {
        var rules = GameData.Weather;
        var segment = (int)Math.Floor(totalMinutes / rules.SegmentMinutes);
        var kind = KindOf(worldSeed, segment, season);
        if (kind == WeatherKind.Clear)
        {
            return WeatherState.Calm;
        }

        var u = totalMinutes / rules.SegmentMinutes - segment;
        var fadeIn = KindOf(worldSeed, segment - 1, season) == kind ? 1 : Smooth(u / rules.FadeShare);
        var fadeOut = KindOf(worldSeed, segment + 1, season) == kind ? 1 : Smooth((1 - u) / rules.FadeShare);
        var strength = 0.55 + 0.45 * Hash.Hash2(segment, 2, worldSeed ^ rules.SeedSalt);
        var wind = kind switch
        {
            WeatherKind.Storm => 0.85 + 0.15 * strength,
            WeatherKind.Rain => 0.35 + 0.3 * strength,
            _ => 0.1,
        };
        return new WeatherState(kind, strength * Math.Min(fadeIn, fadeOut), wind);
    }

    /// <summary>Co je v úseku: bouřka, déšť, mlha, nebo jasno.</summary>
    public static WeatherKind KindOf(int worldSeed, int segment, Season season)
    {
        var rules = GameData.Weather;
        var hour = segment * rules.SegmentMinutes % 1440 / 60;
        var rain = rules.Rain * rules.SeasonRain[(int)season];
        var storm = rain * rules.StormShare;
        var fogBase = hour >= 5 && hour < 10 ? rules.FogMorning : hour >= 10 && hour < 18 ? rules.FogDay : rules.FogNight;
        var fog = fogBase * rules.SeasonFog[(int)season];
        var roll = Hash.Hash2(segment, 1, worldSeed ^ rules.SeedSalt);
        if (roll < storm) return WeatherKind.Storm;
        if (roll < rain) return WeatherKind.Rain;
        return roll < rain + fog ? WeatherKind.Fog : WeatherKind.Clear;
    }

    /// <summary>Kolik méně tepla (cíl tepla těla) dá počasí postavě bez úkrytu.</summary>
    public static double WarmthPenalty(WeatherState weather)
    {
        var rules = GameData.Weather;
        return weather.Kind switch
        {
            WeatherKind.Storm => rules.StormWarmthPenalty * weather.Intensity,
            WeatherKind.Rain => rules.RainWarmthPenalty * weather.Intensity,
            _ => 0,
        };
    }

    /// <summary>Jak rychleji dohořívá oheň a pochodeň (1 = normálně).</summary>
    public static double BurnMultiplier(WeatherState weather)
    {
        var rules = GameData.Weather;
        return weather.Kind switch
        {
            WeatherKind.Storm => 1 + (rules.StormBurnMultiplier - 1) * weather.Intensity,
            WeatherKind.Rain => 1 + (rules.RainBurnMultiplier - 1) * weather.Intensity,
            _ => 1,
        };
    }

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }
}

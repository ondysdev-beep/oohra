using Oohra.Core.World;

namespace Oohra.Server;

/// <summary>Počasí: počítá se ze seedu a času (Weather.At), správce ho může pro zkoušení vynutit příkazem pocasi.</summary>
public sealed partial class GameServer
{
    // Počasí vynucené příkazem správce (null = podle seedu a času). Neukládá se.
    private WeatherKind? _forcedWeather;

    /// <summary>Počasí v tomto ticku (pro tělo, ohně, pochodně a snímek pro klienty).</summary>
    public WeatherState CurrentWeather { get; private set; } = WeatherState.Calm;

    private WeatherState WeatherNow()
    {
        if (_forcedWeather is { } forced)
        {
            return forced == WeatherKind.Clear ? WeatherState.Calm : new WeatherState(forced, 1, forced == WeatherKind.Storm ? 1 : 0.5);
        }

        return Weather.At(_config.Seed, Clock.TotalMinutes, Clock.Season);
    }

    /// <summary>Příkaz pocasi: jasno, mlha, dest, bourka, nebo auto (zase podle seedu a času).</summary>
    private string ForceWeather(string argument)
    {
        switch (argument.ToLowerInvariant())
        {
            case "jasno": _forcedWeather = WeatherKind.Clear; break;
            case "mlha": _forcedWeather = WeatherKind.Fog; break;
            case "dest" or "déšť": _forcedWeather = WeatherKind.Rain; break;
            case "bourka" or "bouřka": _forcedWeather = WeatherKind.Storm; break;
            case "auto": _forcedWeather = null; break;
            default: return $"Teď je {Describe(CurrentWeather)}. Změna: pocasi jasno | mlha | dest | bourka | auto";
        }

        CurrentWeather = WeatherNow();
        return _forcedWeather is null ? "Počasí zase jde samo (podle seedu a času)." : $"Počasí nastaveno: {Describe(CurrentWeather)}.";
    }

    private static string Describe(WeatherState weather)
    {
        var name = weather.Kind switch
        {
            WeatherKind.Fog => "mlha",
            WeatherKind.Rain => "déšť",
            WeatherKind.Storm => "bouřka",
            _ => "jasno",
        };
        return weather.Kind == WeatherKind.Clear ? name : $"{name} (síla {weather.Intensity:0.00})";
    }
}

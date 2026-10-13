using Oohra.Core.Data;

namespace Oohra.Core.Time;

/// <summary>
/// Herní čas ve dvou rytmech (jako S.t a S.cal v prototypu, part2.js):
/// den a noc řídí světlo a tempo potřeb těla, kalendář řídí měsíce a roční období.
/// </summary>
public sealed class GameClock
{
    public const double MinutesPerDay = 1440;

    private readonly double _dayLengthSeconds;
    private readonly double _monthLengthSeconds;
    private readonly int _monthsPerYear;

    public GameClock(double startMinutes, double dayLengthSeconds, double monthLengthSeconds, int monthsPerYear)
    {
        TotalMinutes = startMinutes;
        _dayLengthSeconds = dayLengthSeconds;
        _monthLengthSeconds = monthLengthSeconds;
        _monthsPerYear = monthsPerYear;
    }

    /// <summary>Hodiny podle nastavení, začínají v 7:00 prvního dne.</summary>
    public static GameClock FromSettings(double startMinutes = 7 * 60)
    {
        var s = GameData.Settings;
        return new GameClock(startMinutes, s.DayLengthSeconds, s.MonthLengthSeconds, s.MonthsPerYear);
    }

    /// <summary>Herní minuty od začátku světa (den a noc).</summary>
    public double TotalMinutes { get; private set; }

    /// <summary>Měsíce kalendáře od začátku světa (desetinné číslo).</summary>
    public double CalendarMonths { get; private set; }

    public int Day => (int)Math.Floor(TotalMinutes / MinutesPerDay);

    /// <summary>Hodina dne 0 až 24 (desetinné číslo).</summary>
    public double Hour => TotalMinutes % MinutesPerDay / 60;

    /// <summary>Měsíc v roce 0 až MonthsPerYear - 1.</summary>
    public int MonthOfYear => (int)Math.Floor(CalendarMonths) % _monthsPerYear;

    public int Year => (int)Math.Floor(CalendarMonths) / _monthsPerYear;

    public Season Season => (Season)(MonthOfYear / (_monthsPerYear / 4) % 4);

    /// <summary>Světlo 0 (noc) až 1 (den). Noc je kratší než den.</summary>
    public double Daylight => Math.Clamp(Math.Sin((Hour - 6) / 24 * Math.Tau) * 1.6 + 0.35, 0, 1);

    /// <summary>Kolik herních minut uplyne za danou dobu reálného času.</summary>
    public double GameMinutesFor(double realSeconds)
    {
        return realSeconds * MinutesPerDay / _dayLengthSeconds;
    }

    /// <summary>Kolik reálných sekund trvá daný počet herních minut.</summary>
    public double RealSecondsFor(double gameMinutes)
    {
        return gameMinutes * _dayLengthSeconds / MinutesPerDay;
    }

    /// <summary>Posune čas o reálné sekundy. Vrací, kolik herních minut uplynulo.</summary>
    public double Advance(double realSeconds)
    {
        var minutes = GameMinutesFor(realSeconds);
        TotalMinutes += minutes;
        CalendarMonths += realSeconds / _monthLengthSeconds;
        return minutes;
    }
}

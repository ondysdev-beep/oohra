namespace Oohra.Server;

/// <summary>
/// Měří výkon serveru: jak dlouho trvá jeden tick, kolik času server pracuje a kolik dat posílá.
/// Program z toho každých pár sekund vypíše řádek do konzole (pro zátěžový test s roboty).
/// </summary>
public sealed class ServerStats
{
    private int _ticks;
    private double _tickTotalMs;
    private double _tickMaxMs;
    private double _busyMs;
    private double _lastReportSeconds;
    private long _lastBytesSent;

    /// <summary>Jeden tick trval tolik milisekund.</summary>
    public void AddTick(double milliseconds)
    {
        _ticks++;
        _tickTotalMs += milliseconds;
        _tickMaxMs = Math.Max(_tickMaxMs, milliseconds);
    }

    /// <summary>Server pracoval tolik milisekund (příjem zpráv i ticky, bez čekání).</summary>
    public void AddBusy(double milliseconds)
    {
        _busyMs += milliseconds;
    }

    /// <summary>Souhrn od minulého volání. Potom začne počítat znovu.</summary>
    public string Report(double nowSeconds, int players, long bytesSent, double tickBudgetMs)
    {
        var seconds = Math.Max(nowSeconds - _lastReportSeconds, 0.001);
        var average = _ticks > 0 ? _tickTotalMs / _ticks : 0;
        var load = _busyMs / (seconds * 1000) * 100;
        var kilobytesPerSecond = (bytesSent - _lastBytesSent) / 1024.0 / seconds;
        var text = $"Výkon: hráčů {players}, ticků {_ticks / seconds:0.0}/s, tick průměr {average:0.00} ms, " +
                   $"nejdéle {_tickMaxMs:0.00} ms (limit {tickBudgetMs:0} ms), vytížení {load:0.0} %, " +
                   $"odesláno {kilobytesPerSecond:0.0} kB/s";

        _ticks = 0;
        _tickTotalMs = 0;
        _tickMaxMs = 0;
        _busyMs = 0;
        _lastReportSeconds = nowSeconds;
        _lastBytesSent = bytesSent;
        return text;
    }
}

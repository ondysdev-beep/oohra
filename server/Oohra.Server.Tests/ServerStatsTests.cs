namespace Oohra.Server.Tests;

public class ServerStatsTests
{
    [Fact]
    public void ReportAveragesTicksAndResets()
    {
        var stats = new ServerStats();
        stats.AddTick(1);
        stats.AddTick(3);
        stats.AddBusy(500);

        var report = stats.Report(nowSeconds: 10, players: 2, bytesSent: 10240, tickBudgetMs: 33);

        Assert.Contains("hráčů 2", report);
        Assert.Contains("tick průměr 2.00 ms", report.Replace(',', '.'));
        Assert.Contains("nejdéle 3.00 ms", report.Replace(',', '.'));
        Assert.Contains("vytížení 5.0 %", report.Replace(',', '.'));
        Assert.Contains("odesláno 1.0 kB/s", report.Replace(',', '.'));

        var empty = stats.Report(nowSeconds: 20, players: 2, bytesSent: 10240, tickBudgetMs: 33);
        Assert.Contains("tick průměr 0.00 ms", empty.Replace(',', '.'));
        Assert.Contains("odesláno 0.0 kB/s", empty.Replace(',', '.'));
    }
}

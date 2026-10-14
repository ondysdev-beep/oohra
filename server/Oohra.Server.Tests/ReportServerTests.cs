using Oohra.Core.Social;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Tlačítko Nahlásit (návrh 14.3): záznam pro správce a vyloučení hráče.</summary>
public sealed class ReportServerTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"oohra-reports-{Guid.NewGuid():N}.jsonl");

    [Fact]
    public void ReportPlayerWritesRecordAndLimitsSpam()
    {
        using var net = new TestNetwork(configure: c => c.ReportsFile = _file);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");

        a.Report(ReportTarget.Player, b.PlayerId, ReportReason.Behavior);

        Assert.True(net.Pump(() => HasReportEvent(a, ReportResult.Sent)));
        var line = Assert.Single(File.ReadAllLines(_file));
        Assert.Contains("\"Reporter\":\"Ondra\"", line);
        Assert.Contains("\"Player\":\"Petr\"", line);
        Assert.Contains("\"Reason\":\"Behavior\"", line);
        Assert.DoesNotContain("SignRows", line);

        // Hned další nahlášení je moc brzy.
        a.Report(ReportTarget.General, 0, ReportReason.Bug);
        Assert.True(net.Pump(() => HasReportEvent(a, ReportResult.TooOften)));
        Assert.Single(File.ReadAllLines(_file));
    }

    [Fact]
    public void ReportSignStoresDrawingAndSelfIsNotFound()
    {
        using var net = new TestNetwork(configure: c =>
        {
            c.ReportsFile = _file;
            c.ReportCooldownSeconds = 0;
        });
        var a = net.Join("Ondra");
        Assert.True(net.Pump(() => a.Signs.Length > 0));

        a.Report(ReportTarget.Player, a.PlayerId, ReportReason.Cheating);
        Assert.True(net.Pump(() => HasReportEvent(a, ReportResult.NotFound)));

        a.Report(ReportTarget.Sign, a.Signs[0].Id, ReportReason.OffensiveSign);
        Assert.True(net.Pump(() => HasReportEvent(a, ReportResult.Sent)));
        var line = Assert.Single(File.ReadAllLines(_file));
        Assert.Contains("\"SignRows\":[", line);
        Assert.Contains("#", line);
        Assert.Equal(1, net.Server.Reports.Count);
    }

    [Fact]
    public void BannedPlayerCannotJoin()
    {
        using var net = new TestNetwork(configure: c => c.BannedNicknames = ["Zlobil"]);

        var banned = net.Join("zlobil");
        var other = net.Join("Ondra");

        Assert.False(banned.LoggedIn);
        Assert.Contains("vyloučil", banned.LoginError);
        Assert.True(other.LoggedIn);
    }

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }

    private static bool HasReportEvent(GameConnection connection, ReportResult result)
    {
        return connection.TakeEvents().Any(e => e.Kind == EventKind.Report && e.Code == (byte)result);
    }
}

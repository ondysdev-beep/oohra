using Oohra.Core.Characters;

namespace Oohra.Server.Tests;

/// <summary>Statistiky hraní pro ladění: co kdo dělal, na co umřel, uložení a načtení po restartu.</summary>
public sealed class PlayStatsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"oohra-stats-{Guid.NewGuid():N}.json");

    [Fact]
    public void CountsSessionsIntentsAndDeathsAndSurvivesRestart()
    {
        using (var net = new TestNetwork(configure: c => c.StatsFile = _file))
        {
            var a = net.Join("Ondra");
            var player = net.Server.FindPlayer("Ondra")!;
            player.Character.Hydration = 0;
            player.Character.Health = 0.0001;
            Assert.True(net.Pump(() => a.Self!.Dead));
            a.Reborn();
            Assert.True(net.Pump(() => a.Self is { Dead: false }));

            var stats = net.Server.PlayStats.Of("Ondra")!;
            Assert.Equal(1, stats.Sessions);
            Assert.Equal(1, stats.Deaths);
            Assert.Equal(1, stats.DeathCauses[nameof(DeathCause.Thirst)]);
            Assert.Equal(1, stats.Intents["Reborn"]);
            Assert.True(stats.Results.ContainsKey("Body.Died"));
            Assert.True(stats.MinutesOnline > 0);
        }

        // Server se vypnul (uložil) a po startu pokračuje se starými čísly.
        Assert.True(File.Exists(_file));
        using var restarted = new TestNetwork(configure: c => c.StatsFile = _file);
        restarted.Join("Ondra");

        var again = restarted.Server.PlayStats.Of("Ondra")!;
        Assert.Equal(2, again.Sessions);
        Assert.Equal(1, again.Deaths);
    }

    [Fact]
    public void ResultNamesAreReadable()
    {
        Assert.Equal("Built", PlayStats.CodeName(Oohra.Protocol.EventKind.Build, 0));
        Assert.Equal("Died", PlayStats.CodeName(Oohra.Protocol.EventKind.Body, (byte)BodyEvent.Died));
    }

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }
}

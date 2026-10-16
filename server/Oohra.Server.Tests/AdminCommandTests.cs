namespace Oohra.Server.Tests;

/// <summary>Příkazy správce v okně serveru: vyhodit, vyloučit, povolit, odemknout, oznámit, hráči.</summary>
public class AdminCommandTests
{
    [Fact]
    public void BanKicksAndBlocksLogin()
    {
        using var net = new TestNetwork();
        var a = net.Join("Petr");

        var answer = net.Server.RunCommand("vyloucit Petr");

        Assert.Contains("vyloučený", answer);
        Assert.True(net.Pump(() => net.Server.FindPlayer("Petr") is { Online: false }));
        Assert.False(net.Join("petr").LoggedIn);

        net.Server.RunCommand("povolit Petr");
        Assert.True(net.Join("Petr").LoggedIn);
    }

    [Fact]
    public void AnnouncementReachesPlayers()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");

        net.Server.RunCommand("oznamit Za 5 minut restart");

        var heard = new List<string>();
        Assert.True(net.Pump(() =>
        {
            heard.AddRange(a.TakeAnnouncements());
            return heard.Contains("Za 5 minut restart");
        }));
    }

    /// <summary>Hráč přišel o klíč: správce postavu odemkne a dostane ji ten, kdo přijde první.</summary>
    [Fact]
    public void UnlockGivesCharacterToNextLogin()
    {
        using var net = new TestNetwork();
        net.Join("Ondra", playerKey: "stary").Dispose();
        Assert.True(net.Pump(() => net.Server.FindPlayer("Ondra") is { Online: false }));
        Assert.False(net.Join("Ondra", playerKey: "novy").LoggedIn);

        net.Server.RunCommand("odemknout Ondra");

        Assert.True(net.Join("Ondra", playerKey: "novy").LoggedIn);
        Assert.True(PlayerKeys.Matches(net.Server.FindPlayer("Ondra")!.KeyHash, "novy"));
    }

    [Fact]
    public void ListsPlayersAndKnowsHelp()
    {
        using var net = new TestNetwork();
        net.Join("Ondra");

        Assert.Contains("Ondra", net.Server.RunCommand("hraci"));
        Assert.Contains("vyloucit", net.Server.RunCommand("pomoc"));
        Assert.Contains("Neznámý", net.Server.RunCommand("xyz"));
        Assert.Contains("neukládá", net.Server.RunCommand("ulozit"));
        net.Server.RunCommand("konec");
        Assert.True(net.Server.StopRequested);
    }
}

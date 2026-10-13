using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Zátěž: plný server (20 hráčů) chodí najednou a server stíhá svůj tick.</summary>
public class LoadTests
{
    private const int Players = 20;

    [Fact]
    public void TwentyPlayersSeeEachOtherAndServerKeepsUp()
    {
        using var net = new TestNetwork();
        var clients = Enumerable.Range(1, Players).Select(i => net.Join($"Bot{i:00}")).ToList();
        Assert.All(clients, c => Assert.True(c.LoggedIn, c.LoginError));

        // Každý jde jiným směrem.
        for (var i = 0; i < clients.Count; i++)
        {
            var angle = i * Math.PI * 2 / clients.Count;
            clients[i].SendInput((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        net.TickMilliseconds.Clear();
        net.PumpFor(2000);

        Assert.All(clients, c => Assert.Equal(Players, c.Snapshot!.Players.Length));
        Assert.All(clients, c => Assert.Equal(Players, c.Players.Count));
        Assert.Equal(Players, net.Server.OnlinePlayers);

        // Tick má na sebe 33 ms (30 ticků za sekundu). S 20 hráči musí zbýt velká rezerva.
        var average = net.TickMilliseconds.Average();
        Assert.True(average < 5, $"Průměrný tick trval {average:0.00} ms.");
    }

    [Fact]
    public void FullServerRejectsOneMore()
    {
        using var net = new TestNetwork();
        for (var i = 1; i <= Players; i++)
        {
            net.Join($"Bot{i:00}");
        }

        var extra = new GameConnection();
        extra.Connect("127.0.0.1", net.Server.Port, TestNetwork.InviteCode, "Navic");
        net.Pump(() =>
        {
            extra.Poll();
            return extra.Disconnected;
        });

        Assert.True(extra.Disconnected);
        Assert.False(extra.LoggedIn);
        Assert.Equal(Players, net.Server.OnlinePlayers);
        extra.Dispose();
    }
}

using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Jeskyně přes síť: klient zná vchody, projde dovnitř a ven, v jeskyni nepostaví zeď.</summary>
public class CaveServerTests
{
    [Fact]
    public void PlayerEntersAndLeavesCave()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 10);
        var a = net.Join("Ondra");
        var island = net.Server.Island;
        var entrance = island.CaveEntrances.First(e => !e.Hidden);
        Assert.True(net.Pump(() => a.CaveEntrances.Length == island.CaveEntrances.Count(e => !e.Hidden)));

        var player = net.Server.FindPlayer("Ondra")!;
        (player.X, player.Y) = (entrance.X, entrance.Y);
        a.UseCave(entrance.Id);
        Assert.True(net.Pump(() => island.InCave(player.X, player.Y)
            && a.TakeEvents().Any(e => e.Kind == EventKind.Cave && e.Code == (byte)CaveResult.Entered)));
        Assert.True(net.Pump(() => a.SelfSnapshot() is { } s && island.InCave(s.X, s.Y)));

        a.Place(BuildKind.StickWall, (float)player.X + 1, (float)player.Y, 0, 1);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.NotInCave)));

        a.UseCave(entrance.Id);
        Assert.True(net.Pump(() => !island.InCave(player.X, player.Y)));
        Assert.Equal((entrance.X, entrance.Y), (player.X, player.Y));
    }

    /// <summary>Příkaz správce jeskyne přenese hráče k vchodu. Zátaras nepustí cizího dovnitř.</summary>
    [Fact]
    public void AdminMovesToCaveAndBarricadeStopsStrangers()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 10);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var ondra = net.Server.FindPlayer("Ondra")!;
        var petr = net.Server.FindPlayer("Petr")!;

        Assert.Contains("stojí u vchodu", net.Server.RunCommand("jeskyne Ondra"));
        var entrance = net.Server.Island.CaveEntrances.Single(e => e.X == ondra.X && e.Y == ondra.Y);

        a.Place(BuildKind.Barricade, (float)entrance.X, (float)entrance.Y, 0);
        Assert.True(net.Pump(() => b.Structures.Any(s => s.Kind == BuildKind.Barricade && s.OwnerId == a.PlayerId)));

        (petr.X, petr.Y) = (entrance.X, entrance.Y);
        b.UseCave(entrance.Id);
        Assert.True(net.Pump(() => b.TakeEvents().Any(e => e.Kind == EventKind.Cave && e.Code == (byte)CaveResult.Barricaded)));
        Assert.False(net.Server.Island.InCave(petr.X, petr.Y));

        a.UseCave(entrance.Id);
        Assert.True(net.Pump(() => net.Server.Island.InCave(ondra.X, ondra.Y)));
    }
}

using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Počasí přes síť (všichni vidí stejné, správce ho může vynutit) a pochodeň.</summary>
public class WeatherServerTests
{
    [Fact]
    public void EveryoneSeesForcedRain()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");

        Assert.Contains("déšť", net.Server.RunCommand("pocasi dest"));

        Assert.True(net.Pump(() => a.Snapshot?.Weather == WeatherKind.Rain && b.Snapshot?.Weather == WeatherKind.Rain));
        Assert.True(a.Snapshot!.WeatherIntensity > 0.9f);
        Assert.Contains("samo", net.Server.RunCommand("pocasi auto"));
        Assert.Contains("pocasi", net.Server.RunCommand("pocasi"));
    }

    [Fact]
    public void TorchIsLitAtFireAndOthersSeeIt()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var camp = net.Server.Island.Camps[0];
        player.X = camp.X + 0.5;
        player.Y = camp.Y + 0.5;
        player.Inventory.TryAdd(Item.Stick);

        a.LightTorch();

        // Snímek (nespolehlivý kanál) může přijít dřív než hláška, proto se čeká na obojí.
        var events = new List<GameEvent>();
        Assert.True(net.Pump(() =>
        {
            events.AddRange(a.TakeEvents());
            return b.Snapshot?.Players.Any(p => p.Id == player.Id && p.Torch) == true
                && events.Any(e => e.Kind == EventKind.Fire && e.Code == (byte)FireResult.TorchLit);
        }));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 0));
    }
}

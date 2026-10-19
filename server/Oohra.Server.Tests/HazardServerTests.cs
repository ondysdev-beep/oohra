using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>
/// Nebezpečí přes síť: pijavice a jejich sundání popelem, natření bahnem proti komárům, prohlídka (klíšťata),
/// vosí hnízda (štípou, pochodeň je vykouří).
/// </summary>
public class HazardServerTests
{
    [Fact]
    public void LeechesInBogAndSmearAgainstMosquitoes()
    {
        // Zrychlený čas, ať se pijavice přisají rychle.
        using var net = new TestNetwork(timeSpeed: 120);
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var island = net.Server.Island;
        var bog = Enumerable.Range(0, island.Tiles.Length).First(i => island.Tiles[i] == TileType.Bog);
        player.X = bog % island.Width + 0.5;
        player.Y = bog / island.Width + 0.5;

        Assert.True(net.Pump(() => a.Self!.Symptoms.Contains(Symptom.Leeches), timeoutMs: 15000));

        player.Inventory.TryAdd(Item.Ash);
        a.RemoveLeeches();
        Assert.True(net.Pump(() => player.Character.Leeches == 0 || !a.Self!.Symptoms.Contains(Symptom.Leeches)));

        a.Smear(ash: false);
        Assert.True(net.Pump(() => a.Self!.Symptoms.Contains(Symptom.Smeared)));
    }

    [Fact]
    public void InspectionFindsTicks()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        player.Character.AddTick(net.Server.Clock.TotalMinutes);

        a.InspectBody();
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.TicksFound)));
        Assert.Empty(player.Character.Ticks);

        a.InspectBody();
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.NoTicks)));
    }

    [Fact]
    public void WaspsStingAndTorchSmokesNestOut()
    {
        using var net = new TestNetwork(configure: c => c.Wasps = true);
        var a = net.Join("Ondra");
        Assert.True(net.Pump(() => a.WaspNests.Length > 0));
        var nest = a.WaspNests[0];
        var player = net.Server.FindPlayer("Ondra")!;
        player.X = nest.X + 1;
        player.Y = nest.Y;

        Assert.True(net.Pump(() => player.Character.Toxin > 0 && a.WaspNests.Any(n => n.TileIndex == nest.TileIndex && n.Angry)));

        a.SmokeOutNest(nest.TileIndex);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Wasp && e.Code == (byte)WaspResult.NeedTorch)));

        player.Character.TorchMinutes = 60;
        a.SmokeOutNest(nest.TileIndex);
        Assert.True(net.Pump(() => a.WaspNests.All(n => n.TileIndex != nest.TileIndex)));
        Assert.Contains(net.Server.CaptureWorld().Island.Wasps, w => w.TileIndex == nest.TileIndex);
    }
}

using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.World;

namespace Oohra.Server.Tests;

/// <summary>Bažina přes síť: pijavice a jejich sundání popelem, natření bahnem proti komárům.</summary>
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
}

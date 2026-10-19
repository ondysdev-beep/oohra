namespace Oohra.Server.Tests;

/// <summary>Klient dostane se světem i výšky terénu (kopce), stejné jako na serveru.</summary>
public class TerrainServerTests
{
    [Fact]
    public void ClientGetsTerrainHeights()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var island = net.Server.Island;

        Assert.True(net.Pump(() => a.World is not null));
        var world = a.World!;
        Assert.Equal((world.Width + 1) * (world.Height + 1), world.Elevation.Length);
        Assert.Contains(world.Elevation, e => e > 1);

        var player = net.Server.FindPlayer("Ondra")!;
        Assert.Equal(island.Elevation.At(player.X, player.Y), world.ElevationAt(player.X, player.Y), 1);
    }
}

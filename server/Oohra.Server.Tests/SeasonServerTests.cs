using Oohra.Core.World;

namespace Oohra.Server.Tests;

/// <summary>Sezónní nebezpečí přes síť: jarní záplava a stopy v bahně, které vidí i ostatní.</summary>
public class SeasonServerTests
{
    [Fact]
    public void SpringFloodIsSentToClients()
    {
        using var net = new TestNetwork(configure: c => c.StartMonth = 1.5);
        var a = net.Join("Ondra");

        Assert.True(net.Pump(() => a.FloodedTiles.Length > 0));
        Assert.Equal(net.Server.Island.Flood.FloodedTiles().Count(), a.FloodedTiles.Length);
    }

    [Fact]
    public void NoFloodInSummer()
    {
        using var net = new TestNetwork(configure: c => c.StartMonth = 4.5);
        var a = net.Join("Ondra");
        net.PumpFor(300);

        Assert.Empty(a.FloodedTiles);
    }

    /// <summary>Ondra jde bažinou, Petr kousek vedle vidí jeho stopy.</summary>
    [Fact]
    public void TracksInBogAreSeenByOthersNearby()
    {
        using var net = new TestNetwork(configure: c => c.StartMonth = 4.5);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var island = net.Server.Island;
        var start = Enumerable.Range(0, island.Tiles.Length).First(i =>
            Enumerable.Range(0, 5).All(k => i % island.Width + k < island.Width && island.Tiles[i + k] == TileType.Bog));
        var (x, y) = (start % island.Width + 0.2, start / island.Width + 0.5);
        var ondra = net.Server.FindPlayer("Ondra")!;
        var petr = net.Server.FindPlayer("Petr")!;
        petr.X = x;
        petr.Y = y - 3;

        for (var step = 0; step <= 8; step++)
        {
            ondra.X = x + step * 0.5;
            ondra.Y = y;
            net.PumpFor(60);
        }

        Assert.True(net.Pump(() => b.Tracks.Length >= 3, timeoutMs: 3000));
        Assert.All(b.Tracks, t => Assert.Equal(TrackMaker.Human, t.Maker));
        Assert.All(b.Tracks, t => Assert.True(t.Fresh > 200));
    }
}

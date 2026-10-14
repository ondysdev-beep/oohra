using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Dekorace přes síť: seznam ze serveru a postavení lavice.</summary>
public class DecorationServerTests
{
    [Fact]
    public void ListIsSentAndBenchIsBuilt()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 3);
        var client = net.Join("Ondra");
        Assert.True(net.Pump(() => client.DecorationTypes.Length > 0));
        var bench = client.DecorationTypes.Single(d => d.Id == "bench");
        Assert.Equal("Lavice", bench.Name);
        Assert.True(bench.Blocks);

        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = OpenSpot(net.Server.Island);
        player.X = x;
        player.Y = y;
        client.Place(BuildKind.Decoration, (float)x + 1.5f, (float)y, 0.5f, decoration: "bench");

        Assert.True(net.Pump(() => client.Structures.Length == 1));
        Assert.Equal(BuildKind.Decoration, client.Structures[0].Kind);
        Assert.Equal("bench", client.Structures[0].Decoration);
        Assert.Equal(client.PlayerId, client.Structures[0].OwnerId);
    }

    private static (double X, double Y) OpenSpot(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-3, 7).All(dy => Enumerable.Range(-3, 7).All(dx => !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

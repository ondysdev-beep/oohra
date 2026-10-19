using Oohra.Core.Characters;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Střecha přes síť: postaví se na sloupky, bez podpory praská a spadne na toho, kdo je pod ní.</summary>
public class RoofServerTests
{
    [Fact]
    public void RoofOnPostsCracksAndFallsWhenPostIsRemoved()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 10);
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = OpenSpot(net.Server.Island);
        (player.X, player.Y) = (x, y);

        a.Place(BuildKind.Roof, (float)x, (float)y + 2, 0);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.NoSupport)));

        a.Place(BuildKind.Post, (float)x - 0.8f, (float)y + 2, 0);
        a.Place(BuildKind.Post, (float)x + 0.8f, (float)y + 2, 0);
        Assert.True(net.Pump(() => a.Structures.Count(s => s.Kind == BuildKind.Post) == 2));
        a.Place(BuildKind.Roof, (float)x, (float)y + 2, 0);
        Assert.True(net.Pump(() => a.Structures.Any(s => s.Kind == BuildKind.Roof && !s.Cracking)));

        // Postava stojí pod střechou a rozebere jeden sloupek.
        player.Y = y + 2;
        var post = a.Structures.First(s => s.Kind == BuildKind.Post);
        player.X = post.X + 0.4;
        a.RemoveStructure(post.Id);
        Assert.True(net.Pump(() => a.Structures.Any(s => s.Kind == BuildKind.Roof && s.Cracking)));
        var health = player.Character.Health;
        Assert.True(net.Pump(() => a.Structures.All(s => s.Kind != BuildKind.Roof), timeoutMs: 10000));
        Assert.True(player.Character.Health < health);
    }

    /// <summary>Střecha z kůry: klient vidí materiál, oprava stojí polovinu materiálu.</summary>
    [Fact]
    public void BarkRoofIsSeenAndRepaired()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 10);
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = OpenSpot(net.Server.Island);
        (player.X, player.Y) = (x, y);
        for (var i = 0; i < 5; i++)
        {
            player.Inventory.TryAdd(Oohra.Core.Items.Item.Bast);
        }

        a.Place(BuildKind.Post, (float)x - 0.8f, (float)y + 1.2f, 0);
        a.Place(BuildKind.Post, (float)x + 0.8f, (float)y + 2.8f, 0);
        Assert.True(net.Pump(() => a.Structures.Count(s => s.Kind == BuildKind.Post) == 2));
        a.Place(BuildKind.Roof, (float)x, (float)y + 2, 0, decoration: "Bark");
        Assert.True(net.Pump(() => a.Structures.Any(s => s.Kind == BuildKind.Roof && s.Decoration == "Bark" && !s.Leaking)));

        var roof = a.Structures.Single(s => s.Kind == BuildKind.Roof);
        a.RepairStructure(roof.Id);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.Repaired)));
        // 5 lýk: 3 za střechu, 2 za opravu (polovina ze 3, nahoru).
        Assert.Equal(0, player.Inventory.Count(Oohra.Core.Items.ItemKind.Bast));
    }

    private static (double X, double Y) OpenSpot(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass && y < island.SurfaceHeight - 6
                && Enumerable.Range(-1, 6).All(dy => Enumerable.Range(-3, 7).All(dx => island.TileAt(x + dx, y + dy) == TileType.Grass && !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

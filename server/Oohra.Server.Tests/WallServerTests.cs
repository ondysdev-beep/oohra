using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Zdi a úkryt přes síť.</summary>
public class WallServerTests
{
    [Fact]
    public void WallIsSeenByEveryoneAndCanBeRemoved()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 2);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = StandWithFreeEast(net, player);

        a.BuildWall();

        var wall = net.Server.Island.Index(x + 1, y);
        Assert.True(net.Pump(() => b.Walls.Contains(wall)));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 0));

        a.RemoveWall(x + 1, y);
        Assert.True(net.Pump(() => b.Walls.Length == 0));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 1 && a.Inventory.Stacks[0].Kind == ItemKind.Stick));
    }

    [Fact]
    public void NoWallOnAnotherPlayer()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 2);
        var a = net.Join("Ondra");
        net.Join("Petr");
        var builder = net.Server.FindPlayer("Ondra")!;
        var other = net.Server.FindPlayer("Petr")!;
        var (x, y) = StandWithFreeEast(net, builder);
        other.X = x + 1.5;
        other.Y = y + 0.5;

        a.BuildWall();

        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.CannotBuildHere)));
        Assert.Empty(net.Server.Island.Walls.Tiles);
    }

    [Fact]
    public void WallsAroundMakeShelter()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = StandWithFreeEast(net, player);
        Assert.True(net.Pump(() => client.Self is { Sheltered: false }));

        var walls = net.Server.Island.Walls;
        walls.Place(net.Server.Island.Index(x + 1, y));
        walls.Place(net.Server.Island.Index(x - 1, y));
        walls.Place(net.Server.Island.Index(x, y + 1));
        walls.Place(net.Server.Island.Index(x, y - 1));

        Assert.True(net.Pump(() => client.Self!.Sheltered && client.Walls.Length == 4));
    }

    /// <summary>Postaví hráče na trávu, východně od které je volno, a otočí ho na východ. Vrací jeho dlaždici.</summary>
    private static (int X, int Y) StandWithFreeEast(TestNetwork net, PlayerSession player)
    {
        var island = net.Server.Island;
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass && !Movement.IsBlocked(island, x, y) && !Movement.IsBlocked(island, x + 1, y)
                && Movement.CanStand(island, x + 0.5, y + 0.5, 0.28))
            {
                player.X = x + 0.5;
                player.Y = y + 0.5;
                player.Facing = 0;
                return (x, y);
            }
        }

        throw new InvalidOperationException("Na ostrově není volná tráva.");
    }
}

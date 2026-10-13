using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Režim stavby přes síť: zdi s libovolným úhlem a úkryt.</summary>
public class WallServerTests
{
    [Fact]
    public void WallIsSeenByEveryoneAndCanBeRemoved()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 2);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = StandInOpen(net, player);

        a.Place(BuildKind.StickWall, x + 1, y, 0.3f);

        Assert.True(net.Pump(() => b.Structures.Length == 1));
        Assert.Equal((x + 1, y, 0.3f), (b.Structures[0].X, b.Structures[0].Y, b.Structures[0].Angle));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 0));

        a.RemoveStructure(b.Structures[0].Id);
        Assert.True(net.Pump(() => b.Structures.Length == 0));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 1 && a.Inventory.Stacks[0].Kind == ItemKind.Stick));
    }

    [Fact]
    public void DoorGoesIntoDoorFrameForEveryone()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 4);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = StandInOpen(net, player);

        a.Place(BuildKind.DoorFrame, x + 2, y, (float)(Math.PI / 2));
        Assert.True(net.Pump(() => b.Structures.Length == 1));
        a.Place(BuildKind.Door, x + 2.2f, y + 0.1f, 0);

        Assert.True(net.Pump(() => b.Structures.Length == 2));
        var door = b.Structures.Single(s => s.Kind == BuildKind.Door);
        Assert.Equal((x + 2, y), (door.X, door.Y));
    }

    [Fact]
    public void NoWallOnAnotherPlayer()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 2);
        var a = net.Join("Ondra");
        net.Join("Petr");
        var builder = net.Server.FindPlayer("Ondra")!;
        var other = net.Server.FindPlayer("Petr")!;
        var (x, y) = StandInOpen(net, builder);
        other.X = x + 1;
        other.Y = y;

        a.Place(BuildKind.StickWall, x + 1, y, (float)(Math.PI / 2));

        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.CannotBuildHere)));
        Assert.Empty(net.Server.Island.Structures.All);
    }

    [Fact]
    public void WallsAroundMakeShelter()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = StandInOpen(net, player);
        Assert.True(net.Pump(() => client.Self is { Sheltered: false }));

        var structures = net.Server.Island.Structures;
        structures.Place(x + 0.5, y, Math.PI / 2);
        structures.Place(x - 0.5, y, Math.PI / 2);
        structures.Place(x, y + 0.5, 0);
        structures.Place(x, y - 0.5, 0);

        Assert.True(net.Pump(() => client.Self!.Sheltered && client.Structures.Length == 4));
    }

    /// <summary>Postaví hráče doprostřed trávy, kolem které je ve čtverci 5 × 5 volno. Vrací jeho polohu.</summary>
    private static (float X, float Y) StandInOpen(TestNetwork net, PlayerSession player)
    {
        var island = net.Server.Island;
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-2, 5).All(dy => Enumerable.Range(-2, 5).All(dx => !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                player.X = x + 0.5;
                player.Y = y + 0.5;
                return (x + 0.5f, y + 0.5f);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

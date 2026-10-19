using Oohra.Core.Characters;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Patro přes síť: podlaha patra ze země, žebřík, výlez nahoru (ostatní vidí podlaží), seskočení.</summary>
public class StoreyServerTests
{
    [Fact]
    public void ClimbUpLadderAndJumpDown()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 20);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = OpenSpot(net.Server.Island);
        (player.X, player.Y) = (x, y + 2);

        a.Place(BuildKind.Post, (float)x - 0.8f, (float)y - 0.8f, 0);
        a.Place(BuildKind.Post, (float)x + 0.8f, (float)y + 0.8f, 0);
        Assert.True(net.Pump(() => a.Structures.Count(s => s.Kind == BuildKind.Post) == 2));
        a.Place(BuildKind.Floor, (float)x, (float)y, 0, level: 1);
        Assert.True(net.Pump(() => b.Structures.Any(s => s.Kind == BuildKind.Floor && s.Level == 1)));

        a.Place(BuildKind.Ladder, (float)x, (float)y + 1.4f, 0);
        Assert.True(net.Pump(() => a.Structures.Any(s => s.Kind == BuildKind.Ladder)));
        var ladder = a.Structures.Single(s => s.Kind == BuildKind.Ladder);

        a.UseLadder(ladder.Id);
        Assert.True(net.Pump(() => b.Snapshot?.Players.Any(p => p.Id == player.Id && p.Level == 1) == true));
        Assert.Equal(1, player.Level);

        a.JumpDown();
        Assert.True(net.Pump(() => player.Level == 0 && a.TakeEvents().Any(e => e.Kind == EventKind.Body && e.Code == (byte)BodyEvent.JumpedDown)));
        Assert.True(player.Character.Health < 100);
    }

    private static (double X, double Y) OpenSpot(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (x > 4 && y > 4 && island.Tiles[i] == TileType.Grass && y < island.SurfaceHeight - 6
                && Enumerable.Range(-3, 7).All(dy => Enumerable.Range(-3, 7).All(dx => island.TileAt(x + dx, y + dy) == TileType.Grass && !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

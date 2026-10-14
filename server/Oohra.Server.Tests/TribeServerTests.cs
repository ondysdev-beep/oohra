using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Kmen pozvánkou, zásoby kmene a cizí stavby přes síť.</summary>
public class TribeServerTests
{
    [Fact]
    public void InviteAndJoin()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        Together(net, "Ondra", "Petr");

        a.InviteToTribe(b.PlayerId);

        Assert.True(net.Pump(() => b.TakeInvites().Contains(a.PlayerId)));
        Assert.Contains(b.TakeGestures(), g => g.PlayerId == a.PlayerId && g.Gesture == Gesture.Invite);
        b.AnswerInvite(a.PlayerId, accept: true);

        Assert.True(net.Pump(() => a.TribeMembers.Length == 2 && b.TribeMembers.Length == 2));
        Assert.Equal(new[] { a.PlayerId, b.PlayerId }, b.TribeMembers);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Tribe && e.Code == (byte)TribeResult.TheyJoined)));
    }

    /// <summary>Kmen patří hráči, ne postavě: po smrti a novém narození v něm hráč zůstane.</summary>
    [Fact]
    public void TribeSurvivesDeath()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        MakeTribe(net, a, b);
        var player = net.Server.FindPlayer("Petr")!;
        player.Character.Hydration = 0;
        player.Character.Health = 0.0001;
        Assert.True(net.Pump(() => b.Self!.Dead));

        b.Reborn();

        Assert.True(net.Pump(() => b.Self is { Dead: false }));
        net.PumpFor(200);
        Assert.Equal(2, b.TribeMembers.Length);
    }

    [Fact]
    public void StockpileSharedWithTribeOnly()
    {
        using var net = new TestNetwork(configure: c =>
        {
            c.StartSticks = 4;
            c.StartStones = 1;
        });
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var c = net.Join("Jana");
        var (x, y) = OpenSpot(net.Server.Island);
        foreach (var nick in new[] { "Ondra", "Petr", "Jana" })
        {
            Put(net, nick, x, y);
        }

        MakeTribe(net, a, b);
        a.Place(BuildKind.Stockpile, (float)x + 1, (float)y, 0);
        Assert.True(net.Pump(() => a.Stockpiles.Length == 1 && b.Stockpiles.Length == 1));
        var stockpileId = a.Stockpiles[0].StructureId;
        Assert.Equal(a.PlayerId, a.Structures.Single().OwnerId);

        var stone = Array.FindIndex(a.Inventory.Stacks, s => s.Kind == ItemKind.Stone);
        a.StoreItem(stockpileId, stone);
        Assert.True(net.Pump(() => b.Stockpiles[0].Contents.Stacks.Any(s => s.Kind == ItemKind.Stone)));

        // Cizí zásoby nevidí a vzít z nich nemůže.
        Assert.Empty(c.Stockpiles);
        c.TakeItem(stockpileId, 0);
        Assert.True(net.Pump(() => c.TakeEvents().Any(e => e.Kind == EventKind.Tribe && e.Code == (byte)TribeResult.NotYours)));

        b.TakeItem(stockpileId, 0);
        Assert.True(net.Pump(() => b.Inventory.Stacks.Any(s => s.Kind == ItemKind.Stone) && a.Stockpiles[0].Contents.Stacks.Length == 0));
    }

    [Fact]
    public void ForeignWallCannotBeRemoved()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 2);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var (x, y) = OpenSpot(net.Server.Island);
        Put(net, "Ondra", x, y);
        Put(net, "Petr", x, y + 1);
        a.Place(BuildKind.StickWall, (float)x + 1.5f, (float)y, 0);
        Assert.True(net.Pump(() => b.Structures.Length == 1));

        b.RemoveStructure(b.Structures[0].Id);

        Assert.True(net.Pump(() => b.TakeEvents().Any(e => e.Kind == EventKind.Build && e.Code == (byte)BuildResult.NotYours)));
        Assert.Single(net.Server.Island.Structures.All);
    }

    private static void MakeTribe(TestNetwork net, GameConnection inviter, GameConnection invitee)
    {
        MakeTribe(net, inviter, "Ondra", invitee, "Petr");
    }

    private static void MakeTribe(TestNetwork net, GameConnection inviter, string inviterName, GameConnection invitee, string inviteeName)
    {
        Together(net, inviterName, inviteeName);
        inviter.InviteToTribe(invitee.PlayerId);
        Assert.True(net.Pump(() => invitee.TakeInvites().Contains(inviter.PlayerId)));
        invitee.AnswerInvite(inviter.PlayerId, true);
        Assert.True(net.Pump(() => inviter.TribeMembers.Length == 2));
    }

    private static void Together(TestNetwork net, string first, string second)
    {
        var a = net.Server.FindPlayer(first)!;
        Put(net, second, a.X, a.Y);
    }

    private static void Put(TestNetwork net, string nickname, double x, double y)
    {
        var player = net.Server.FindPlayer(nickname)!;
        player.X = x;
        player.Y = y;
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

using Oohra.Core;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Oheň přes síť: materiál, rozdělání, všichni ho vidí, hřeje a peče se u něj.</summary>
public class FireServerTests
{
    [Fact]
    public void StoneFromRockGoesToBackpack()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var island = net.Server.Island;
        var rock = Array.FindIndex(island.Tiles, t => t == TileType.Rock);
        var (x, y) = (rock % island.Width, rock / island.Width);
        player.X = x + 0.5;
        player.Y = y + 0.5;

        client.TakeStone(x, y);

        Assert.True(net.Pump(() => client.Inventory.Stacks.Length == 1));
        Assert.Equal(ItemKind.Stone, client.Inventory.Stacks[0].Kind);
        Assert.Contains(client.TakeEvents(), e => e.Kind == EventKind.Material && e.Code == (byte)MaterialResult.GotStone);
    }

    [Fact]
    public void FireIsSeenByEveryoneAndWarms()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        StandWithFreeEast(net, player);
        GiveMaterials(player, sticks: 4, stones: 1);

        a.LightFire();

        Assert.True(net.Pump(() => b.Fires.Length == 1 && a.Self!.NearFire));
        Assert.True(b.Fires[0].Burning);
        Assert.Equal(((int)player.X + 1, (int)player.Y), (b.Fires[0].TileX, b.Fires[0].TileY));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 1 && a.Inventory.Stacks[0].Kind == ItemKind.Stick));
        Assert.Contains(a.TakeEvents(), e => e.Kind == EventKind.Fire && e.Code == (byte)FireResult.Lit);
    }

    [Fact]
    public void FireBurnsOutForEveryoneAndCanBeRelit()
    {
        using var net = new TestNetwork(timeSpeed: 600);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        StandWithFreeEast(net, player);
        GiveMaterials(player, sticks: 4, stones: 1);
        a.LightFire();
        Assert.True(net.Pump(() => b.Fires.Length == 1));

        // 600× zrychlený čas: 720 minut ohně uběhne asi za 3 sekundy.
        Assert.True(net.Pump(() => b.Fires.Length == 1 && !b.Fires[0].Burning, timeoutMs: 8000));

        a.FeedFire(b.Fires[0].Id);
        Assert.True(net.Pump(() => b.Fires[0].Burning));
    }

    [Fact]
    public void BakingByFire()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        StandWithFreeEast(net, player);
        player.Inventory.TryAdd(new PlantItem(0, PlantPart.Root, Ripeness.Ripe, Season.Spring, TileType.Grass));
        GiveMaterials(player, sticks: 3, stones: 1);

        a.Bake(0);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Fire && e.Code == (byte)FireResult.NoFireNear)));

        a.LightFire();
        a.Bake(0);

        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 1 && a.Inventory.Stacks[0].Cooking == Cooking.Baked));
    }

    /// <summary>Nastavení pro zkoušení: svět začne v noci a postava má materiál na oheň.</summary>
    [Fact]
    public void TestingConfigStartsAtNightWithFireKit()
    {
        using var net = new TestNetwork(configure: c =>
        {
            c.StartHour = 21;
            c.StartSticks = 4;
            c.StartStones = 1;
        });

        var client = net.Join("Ondra");

        Assert.True(net.Pump(() => client.Snapshot is not null));
        Assert.Equal(21, client.Snapshot!.TotalMinutes / 60, 1);
        Assert.Equal(new[] { (ItemKind.Stick, 4), (ItemKind.Stone, 1) }, client.Inventory.Stacks.Select(s => (s.Kind, s.Count)));
    }

    private static void GiveMaterials(PlayerSession player, int sticks, int stones)
    {
        for (var i = 0; i < sticks; i++)
        {
            player.Inventory.TryAdd(Item.Stick);
        }

        for (var i = 0; i < stones; i++)
        {
            player.Inventory.TryAdd(Item.Stone);
        }
    }

    /// <summary>Postaví hráče na trávu, východně od které je volno, a otočí ho na východ.</summary>
    private static void StandWithFreeEast(TestNetwork net, PlayerSession player)
    {
        var island = net.Server.Island;
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass && !Movement.IsBlocked(island, x, y) && !Movement.IsBlocked(island, x + 1, y))
            {
                player.X = x + 0.5;
                player.Y = y + 0.5;
                player.Facing = 0;
                return;
            }
        }
    }
}

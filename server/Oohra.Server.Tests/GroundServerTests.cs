using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Věci na zemi přes síť: zahodit, vidět, sebrat a vyrábět z hromádek u sebe.</summary>
public class GroundServerTests
{
    [Fact]
    public void DroppedItemLiesOnGroundAndOtherPlayerPicksItUp()
    {
        using var net = new TestNetwork(configure: c =>
        {
            c.StartBag = false;
            c.StartSticks = 3;
        });
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var ondra = net.Server.FindPlayer("Ondra")!;
        var petr = net.Server.FindPlayer("Petr")!;
        petr.X = ondra.X;
        petr.Y = ondra.Y;
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Stick)));

        a.Drop(Array.FindIndex(a.Inventory.Stacks, s => s.Kind == ItemKind.Stick));

        // Klacky nezmizí: oba je vidí na zemi.
        Assert.True(net.Pump(() => a.GroundItems.Length == 1 && b.GroundItems.Length == 1));
        var pile = b.GroundItems[0];
        Assert.Equal(ItemKind.Stick, pile.Stack.Kind);
        Assert.Equal(3, pile.Stack.Count);
        Assert.DoesNotContain(a.Inventory.Stacks, s => s.Kind == ItemKind.Stick);

        // Petr měl své 3 klacky, s těmi ze země jich má 6.
        b.PickUpItem(pile.Id);
        Assert.True(net.Pump(() => b.Inventory.Stacks.Where(s => s.Kind == ItemKind.Stick).Sum(s => s.Count) == 6 && a.GroundItems.Length == 0));
        Assert.Contains(b.TakeEvents(), e => e.Kind == EventKind.Ground && e.Code == (byte)GroundResult.Taken);
    }

    /// <summary>
    /// Nový člověk (dvě ruce, bez batohu): nůž v ruce, lýko a klacky na zemi, a malý batoh z toho jde udělat
    /// zkoušením (Spojit se zaškrtnutými hromádkami ze země).
    /// </summary>
    [Fact]
    public void NewcomerCombinesMaterialFromGroundIntoBag()
    {
        using var net = new TestNetwork(configure: c => c.StartBag = false);
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        player.Inventory.TryAdd(new Item(ItemKind.Knife, Uses: 40));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Knife)));

        for (var attempt = 0; attempt < 20 && player.Inventory.Bag is null; attempt++)
        {
            var island = net.Server.Island;
            var bast = island.Ground.Near(player.X, player.Y, 0, 3).Sum(p => p.Item.Kind == ItemKind.Bast ? p.Count : 0);
            if (bast < 4)
            {
                island.Ground.Put(island, Item.Bast, 4 - bast, player.X, player.Y, 0, 0, net.Server.Clock.TotalMinutes);
            }

            var sticks = island.Ground.Near(player.X, player.Y, 0, 3).Sum(p => p.Item.Kind == ItemKind.Stick ? p.Count : 0);
            if (sticks < 2)
            {
                island.Ground.Put(island, Item.Stick, 2 - sticks, player.X, player.Y, 0, Math.PI, net.Server.Clock.TotalMinutes);
            }

            Assert.True(net.Pump(() => a.GroundItems.Length == 2));
            a.Combine([], a.GroundItems.Select(g => g.Id).ToArray());
            Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Craft)));
        }

        Assert.True(net.Pump(() => a.Inventory.Bag == ItemKind.SmallBag));
        Assert.Equal(GameData.Crafting.Hands + GameData.Crafting.BagSlots[ItemKind.SmallBag], player.Inventory.Capacity);
    }
}

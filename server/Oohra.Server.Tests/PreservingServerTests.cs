using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Uzení a sušení přes síť: maso do kouře ohně a zpátky uzené, cizí sušák nejde použít.</summary>
public class PreservingServerTests
{
    [Fact]
    public void MeatIsSmokedOverFireAndTakenBack()
    {
        // Zrychlený čas: uzení (4 herní hodiny) trvá pár sekund.
        using var net = new TestNetwork(timeSpeed: 300);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var (x, y) = OpenSpot(net.Server.Island);
        (player.X, player.Y) = (x, y);
        var fire = LightFire(net, x + 1, y);
        player.Inventory.TryAdd(Item.RawMeat(net.Server.Clock.Day));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Meat)));

        a.HangMeat(RackHolder.Fire, fire.Id, 0);
        Assert.True(net.Pump(() => b.Racks.Any(r => r.Holder == RackHolder.Fire && r.Id == fire.Id && r.Pieces.Length == 1)));
        Assert.True(net.Pump(() => b.Racks.Any(r => r.Id == fire.Id && r.Pieces[0].Cooking == Cooking.Smoked), timeoutMs: 15000));

        a.TakeFromRack(RackHolder.Fire, fire.Id);
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Meat && s.Cooking == Cooking.Smoked)));
        Assert.True(net.Pump(() => b.Racks.Length == 0));
    }

    [Fact]
    public void ForeignDryingRackCannotBeUsed()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 5);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var ondra = net.Server.FindPlayer("Ondra")!;
        var petr = net.Server.FindPlayer("Petr")!;
        var (x, y) = OpenSpot(net.Server.Island);
        (ondra.X, ondra.Y) = (x, y);
        a.Place(BuildKind.Decoration, (float)x + 1.5f, (float)y, 0, decoration: GameData.Preserving.RackDecoration);
        Assert.True(net.Pump(() => b.Structures.Length == 1));
        var rack = b.Structures[0];

        (petr.X, petr.Y) = (x + 1.5, y + 1.2);
        petr.Inventory.TryAdd(Item.RawMeat(net.Server.Clock.Day));
        b.HangMeat(RackHolder.Structure, rack.Id, 0);
        Assert.True(net.Pump(() => b.TakeEvents().Any(e => e.Kind == EventKind.Preserve && e.Code == (byte)PreserveResult.NotYours)));

        ondra.Inventory.TryAdd(Item.RawMeat(net.Server.Clock.Day));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Meat)));
        a.HangMeat(RackHolder.Structure, rack.Id, Array.FindIndex(a.Inventory.Stacks, s => s.Kind == ItemKind.Meat));
        Assert.True(net.Pump(() => a.Racks.Any(r => r.Holder == RackHolder.Structure && r.Id == rack.Id && r.Pieces.Length == 1)));
    }

    private static Fire LightFire(TestNetwork net, double x, double y)
    {
        var materials = new Inventory(10);
        for (var i = 0; i < 3; i++)
        {
            materials.TryAdd(Item.Stick);
        }

        materials.TryAdd(Item.Stone);
        Assert.Equal(FireResult.Lit, net.Server.Fires.Light(net.Server.Island, materials, x - 1, y, x, y));
        return net.Server.Fires.All.Last();
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

using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Kácení přes síť: sekera, pád stromu (všichni vidí pařez a kmen), klady, nošení a položení kladu.</summary>
public class WoodServerTests
{
    [Fact]
    public void ChopTreeCutPieceCarryAndDrop()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var player = net.Server.FindPlayer("Ondra")!;
        var island = net.Server.Island;
        var tree = island.Trees.First(t => island.HasTree(t.TileIndex) && Movement.CanStand(island, t.X - 1, t.Y, 0.28));
        player.X = tree.X - 1;
        player.Y = tree.Y;
        player.Inventory.TryAdd(Item.Stick);
        player.Inventory.TryAdd(Item.Stone);
        player.Inventory.TryAdd(Item.Stone);

        a.MakeAxe();
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Axe && s.Uses == GameData.Crafting.Find("axe")!.Uses)));
        Assert.Contains(a.TakeEvents(), e => e.Kind == EventKind.Craft && e.Code == (byte)CraftResult.Made);

        // Seká v rytmu, dokud strom nepadne (pád trvá warnSeconds).
        var hits = GameData.Trees.For(tree.Kind).Hits;
        for (var i = 0; i < hits; i++)
        {
            a.ChopTree((int)tree.X, (int)tree.Y);
            net.PumpFor((int)(GameData.Wood.ChopCooldownSeconds * 1000) + 100);
        }

        Assert.True(net.Pump(() => b.TreeStates.Any(t => t.TileIndex == tree.TileIndex && t.State == TreeState.Stump)
            && b.Timbers.Any(t => t.Kind == TimberKind.Trunk)));
        var trunk = b.Timbers.Single(t => t.Kind == TimberKind.Trunk);
        Assert.Equal(tree.Kind, trunk.Tree);

        player.X = trunk.X;
        player.Y = trunk.Y + 0.6;
        a.ChopTimber(trunk.Id);
        Assert.True(net.Pump(() => b.Timbers.Any(t => t.Kind == TimberKind.Piece)));
        var piece = b.Timbers.First(t => t.Kind == TimberKind.Piece);
        player.X = piece.X;
        player.Y = piece.Y + 0.5;

        a.PickUpTimber(piece.Id);
        // Snímek (nespolehlivý kanál) může přijít dřív než batoh, proto se čeká na obojí.
        Assert.True(net.Pump(() => b.Snapshot?.Players.Any(p => p.Id == player.Id && p.Carry) == true
            && a.Inventory.Stacks.Any(s => s.Kind == ItemKind.LogPiece && s.Tree == tree.Kind)));

        a.DropTimber();
        Assert.True(net.Pump(() => b.Snapshot?.Players.Any(p => p.Id == player.Id && !p.Carry) == true
            && b.Timbers.Count(t => t.Kind == TimberKind.Piece) == 1));
    }
}

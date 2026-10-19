using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;

namespace Oohra.Server.Tests;

/// <summary>Příkazy správce na zkoušení: dát věc, čas, měsíc, tělo, uzdravit, přenést, recepty, oheň.</summary>
public class TestingCommandTests
{
    [Fact]
    public void GiveAddsItemsAndPutsRestOnGround()
    {
        using var net = new TestNetwork(configure: c => c.StartBag = false);
        net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;

        Assert.Contains("sekera", net.Server.RunCommand("dat Ondra sekera"));
        var axe = Assert.Single(player.Inventory.Stacks).Item;
        Assert.Equal(ItemKind.Axe, axe.Kind);
        Assert.Equal(GameData.Crafting.Find("axe")!.Uses, axe.Uses);

        // Ruce mají dvě místa: 9 kamenů do druhé ruky, zbytek na zem.
        Assert.Contains("na zemi", net.Server.RunCommand("dat Ondra kamen 12"));
        Assert.Equal(GameData.Gathering.Inventory.StackSize, player.Inventory.Count(ItemKind.Stone));
        Assert.Equal(12 - GameData.Gathering.Inventory.StackSize, net.Server.Island.Ground.All.Sum(p => p.Count));

        Assert.Contains("Neznám věc", net.Server.RunCommand("dat Ondra kladivo"));
    }

    [Fact]
    public void TimeMonthAndBody()
    {
        using var net = new TestNetwork();
        net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var day = net.Server.Clock.Day;

        net.Server.RunCommand("cas 21");
        Assert.Equal(21, net.Server.Clock.Hour, 3);
        Assert.True(net.Server.Clock.Day <= day + 1);

        net.Server.RunCommand("mesic 1");
        Assert.Equal(0, net.Server.Clock.MonthOfYear);

        net.Server.RunCommand("telo Ondra sytost 20");
        Assert.Equal(20, player.Character.Satiety);
        net.Server.RunCommand("uzdravit Ondra");
        Assert.Equal(100, player.Character.Satiety);
        Assert.Equal(100, player.Character.Health);
    }

    [Fact]
    public void TeleportRecipesAndFire()
    {
        using var net = new TestNetwork();
        net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        var island = net.Server.Island;

        Assert.Contains("je na", net.Server.RunCommand("presun Ondra more"));
        var (x, y) = ((int)player.X, (int)player.Y);
        Assert.Equal(TileType.Sand, island.TileAt(x, y));

        Assert.Contains("je na", net.Server.RunCommand("presun Ondra bazina"));
        Assert.Equal(TileType.Bog, island.TileAt((int)player.X, (int)player.Y));
        Assert.Contains("je na", net.Server.RunCommand("presun Ondra skala"));
        Assert.Contains(island.Rocks.All, p => Math.Abs(p.X - player.X) <= 2 && Math.Abs(p.Y - player.Y) <= 2);

        Assert.Contains("všechny recepty", net.Server.RunCommand("recepty Ondra"));
        Assert.All(GameData.Crafting.Recipes, r => Assert.True(player.Character.Knows(r.Id)));

        net.Server.RunCommand("presun Ondra louka");
        Assert.Contains("Oheň hoří", net.Server.RunCommand("ohen Ondra"));
        Assert.Contains(net.Server.Fires.All, f => f.Burning);
        Assert.Contains("dat <přezdívka>", net.Server.RunCommand("pomoc"));
    }
}

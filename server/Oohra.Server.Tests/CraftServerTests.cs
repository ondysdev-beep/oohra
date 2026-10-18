using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Ruce, batoh na zádech a recepty přes síť (návrh 0.8, kap. 3).</summary>
public class CraftServerTests
{
    [Fact]
    public void NewcomerHasHandsLearnsBagByTryingAndWearsIt()
    {
        using var net = new TestNetwork(configure: c => c.StartBag = false);
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;

        Assert.True(net.Pump(() => a.Inventory.Capacity == GameData.Crafting.Hands && a.Inventory.Bag is null));
        // Svět je v době Pradávných: základní recepty postava zná, malý batoh ne.
        Assert.True(net.Pump(() => a.Recipes.Any(r => r.Id == "axe")));
        Assert.DoesNotContain(a.Recipes, r => r.Id == "smallBag");

        for (var attempt = 0; attempt < 20 && player.Inventory.Bag is null; attempt++)
        {
            // Lýko a klacky do rukou (dvě hromádky), pak zkusit spojit.
            while (player.Inventory.Count(ItemKind.Bast) < 4) player.Inventory.TryAdd(Item.Bast);
            while (player.Inventory.Count(ItemKind.Stick) < 2) player.Inventory.TryAdd(Item.Stick);
            a.Combine([0, 1]);
            Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Craft)));
        }

        Assert.True(net.Pump(() => a.Inventory.Bag == ItemKind.SmallBag
            && a.Inventory.Capacity == GameData.Crafting.Hands + GameData.Crafting.BagSlots[ItemKind.SmallBag]));
        Assert.True(net.Pump(() => a.Recipes.Any(r => r.Id == "smallBag" && r.Inputs.Any(i => i.Kind == ItemKind.Bast))));

        // Batoh jde sundat do rukou a zase nasadit.
        a.TakeOffBag();
        Assert.True(net.Pump(() => a.Inventory.Bag is null && a.Inventory.Stacks.Any(s => s.Kind == ItemKind.SmallBag)));
        a.WearBag(a.Inventory.Stacks.ToList().FindIndex(s => s.Kind == ItemKind.SmallBag));
        Assert.True(net.Pump(() => a.Inventory.Bag == ItemKind.SmallBag));
    }

    [Fact]
    public void CraftingKnownRecipeAndAshFromFire()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        player.Inventory.TryAdd(Item.Stone);
        player.Inventory.TryAdd(Item.Stone);

        a.Craft("knife");
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Knife && s.Uses > 0)));
        Assert.Contains(a.TakeEvents(), e => e.Kind == EventKind.Craft && e.Code == (byte)CraftResult.Made
            && e.Subject == GameData.Crafting.IndexOf(GameData.Crafting.Find("knife")!));

        // Neznámý recept z věcí, ze kterých nic nejde: nic se nestane.
        player.Inventory.TryAdd(Item.Ash);
        a.Combine([a.Inventory.Stacks.ToList().FindIndex(s => s.Kind == ItemKind.Knife), a.Inventory.Stacks.Length]);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Craft && e.Code == (byte)CraftResult.NothingCame)));
    }
}

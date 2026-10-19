using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Deník a ukazování receptů přes síť (návrh 0.8, kap. 3).</summary>
public class JournalServerTests
{
    [Fact]
    public void JournalIsSewnReadAndDrawnInto()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var player = net.Server.FindPlayer("Ondra")!;
        player.Character.Learn("journal");
        player.Inventory.TryAdd(new Item(ItemKind.TannedHide));
        player.Inventory.TryAdd(Item.Bast);
        player.Inventory.TryAdd(new Item(ItemKind.Needle, Uses: 5));

        a.Craft("journal");
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Journal)));
        var stack = a.Inventory.Stacks.ToList().FindIndex(s => s.Kind == ItemKind.Journal);

        a.ReadJournal(stack);
        Assert.True(net.Pump(() => a.Journal is { } j && j.StackIndex == stack && j.Pages.Length == 12));

        var bits = SignBits.Empty();
        SignBits.Set(bits, 5, 5, true);
        a.DrawJournal(stack, 1, bits);
        Assert.True(net.Pump(() => a.Journal is { } j && SignBits.Get(j.Pages[1], 5, 5)));
    }

    [Fact]
    public void RecipeIsShownToAnotherPlayer()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var ondra = net.Server.FindPlayer("Ondra")!;
        var petr = net.Server.FindPlayer("Petr")!;
        ondra.Character.Learn("smallBag");
        petr.X = ondra.X + 1;
        petr.Y = ondra.Y;

        a.TeachRecipe(petr.Id, "smallBag");

        Assert.True(net.Pump(() => b.Recipes.Any(r => r.Id == "smallBag")));
        Assert.True(petr.Character.Knows("smallBag"));
        Assert.Contains(b.TakeEvents(), e => e.Kind == EventKind.Craft && e.Code == (byte)CraftResult.LearnedFromOther);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Craft && e.Code == (byte)CraftResult.Taught)));
    }
}

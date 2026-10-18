using Oohra.Core.Items;
using Oohra.Core.Social;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Tělo mrtvého hráče: věci zůstanou u něj, 24 hodin jen pro jeho kmen, po 4 dnech zmizí. Podpis u značky.</summary>
public class CorpseTests
{
    private const double Hour = 60;
    private const double Day = 1440;
    private const int Ondra = 1;
    private const int Petr = 2;
    private const int Jana = 3;

    [Fact]
    public void ItemsStayWithBody()
    {
        var corpses = new Corpses();
        var backpack = Backpack();
        var signature = new Signature([1, 2], SignBits.Empty());

        var corpse = corpses.Add(10.5, 20.5, Ondra, 1, 100, backpack, signature);

        Assert.Empty(backpack.Stacks);
        Assert.Equal(2, corpse.Items.Count(ItemKind.Stick));
        Assert.Equal(1, corpse.Items.Count(ItemKind.Stone));
        Assert.Equal(new byte[] { 1, 2 }, corpse.Signature!.Name);
        Assert.Equal(100 + 24 * Hour, corpse.TribeOnlyUntil);
        Assert.Equal(1, corpses.Version);
    }

    /// <summary>Mrtvý (svým dalším životem) a jeho kmen berou hned, cizí až po 24 hodinách.</summary>
    [Fact]
    public void TribeFirstThenAnyone()
    {
        var corpses = new Corpses();
        var tribes = new Tribes();
        tribes.Invite(Ondra, 0, 0, Petr, 1, 0, 0);
        tribes.Answer(Petr, Ondra, true, 0);
        var corpse = corpses.Add(10.5, 20.5, Ondra, 0, 0, Backpack(), null);

        Assert.Equal(CorpseResult.TribeOnly, corpses.Take(corpse.Id, 0, new Inventory(10), 10.5, 20.5, Jana, tribes, 2 * Hour));
        Assert.Equal(CorpseResult.Taken, corpses.Take(corpse.Id, 0, new Inventory(10), 10.5, 20.5, Petr, tribes, 2 * Hour));
        Assert.Equal(CorpseResult.Taken, corpses.Take(corpse.Id, 0, new Inventory(10), 10.5, 20.5, Ondra, tribes, 3 * Hour));
        Assert.Equal(CorpseResult.Empty, corpses.Take(corpse.Id, 0, new Inventory(10), 10.5, 20.5, Ondra, tribes, 3 * Hour));

        var second = corpses.Add(10.5, 20.5, Ondra, 0, 0, Backpack(), null);
        Assert.Equal(CorpseResult.Taken, corpses.Take(second.Id, 0, new Inventory(10), 10.5, 20.5, Jana, tribes, 25 * Hour));
    }

    [Fact]
    public void ReachAndFullBackpack()
    {
        var corpses = new Corpses();
        var corpse = corpses.Add(10.5, 20.5, Ondra, 0, 0, Backpack(), null);
        var full = new Inventory(1);
        full.TryAdd(Item.RawMeat(0));

        Assert.Equal(CorpseResult.TooFar, corpses.Take(corpse.Id, 0, new Inventory(10), 15, 20.5, Ondra, new Tribes(), 0));
        Assert.Equal(CorpseResult.InventoryFull, corpses.Take(corpse.Id, 0, full, 10.5, 20.5, Ondra, new Tribes(), 0));
        Assert.Equal(CorpseResult.NotFound, corpses.Take(99, 0, new Inventory(10), 10.5, 20.5, Ondra, new Tribes(), 0));
    }

    [Fact]
    public void BodyDecaysAfterFourDays()
    {
        var corpses = new Corpses();
        corpses.Add(10.5, 20.5, Ondra, 0, Day, Backpack(), null);

        corpses.Decay(4.9 * Day);
        Assert.Single(corpses.All);
        corpses.Decay(5 * Day);
        Assert.Empty(corpses.All);
    }

    [Fact]
    public void SignKeepsAuthorSignature()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var grass = Enumerable.Range(0, island.Tiles.Length).First(i => island.Tiles[i] == TileType.Grass && !island.HasTree(i));
        var (x, y) = (grass % island.Width + 0.5, grass / island.Width + 0.5);
        var bits = SignBits.Empty();
        SignBits.Set(bits, 3, 3, true);
        var mark = SignBits.Empty();
        SignBits.Set(mark, 1, 1, true);

        island.Signs.Draw(island, Ondra, x, y, x, y, bits, 0, new Signature([4, 5, 6], mark));

        var sign = island.Signs.All.Single();
        Assert.Equal(new byte[] { 4, 5, 6 }, sign.Signature!.Name);
        Assert.True(SignBits.Get(sign.Signature.Mark, 1, 1));
    }

    [Theory]
    [InlineData(new byte[] { 1 }, 18, true)]
    [InlineData(new byte[] { 1, 2, 3, 4 }, 18, false)]
    [InlineData(new byte[0], 18, false)]
    [InlineData(new byte[] { 1 }, 10, false)]
    public void SignatureValidation(byte[] name, int markBytes, bool valid)
    {
        Assert.Equal(valid, Signature.IsValid(name, new byte[markBytes]));
    }

    private static Inventory Backpack()
    {
        var backpack = new Inventory(10);
        backpack.TryAdd(Item.Stick);
        backpack.TryAdd(Item.Stick);
        backpack.TryAdd(Item.Stone);
        return backpack;
    }
}

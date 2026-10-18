using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Dekorace ze seznamu decorations.json: cena, místo, kudy se dá projít a co vrátí rozebrání.</summary>
public class DecorationTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    [Fact]
    public void ListLoads()
    {
        var decorations = GameData.Decorations;

        Assert.NotEmpty(decorations);
        Assert.Equal(decorations.Count, decorations.Select(d => d.Id).Distinct().Count());
        Assert.All(decorations, d => Assert.True(d.Name.Length > 0 && d.Radius > 0 && d.Sticks >= 0 && d.Stones >= 0));
        Assert.Equal("Lavice", GameData.Decoration("bench")!.Name);
        Assert.Null(GameData.Decoration("neexistuje"));
    }

    /// <summary>Lavice stojí 3 klacky a nedá se přes ni projít. Rozebráním se vrátí polovina (1 klacek).</summary>
    [Fact]
    public void BenchBlocksAndGivesHalfBack()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var (x, y) = OpenSpot(island);
        var bag = Bag(sticks: 3, stones: 0);

        Assert.Equal(BuildResult.Built, Build(island, bag, x, y, x + 1.5, y, "bench"));
        Assert.Equal(0, bag.Count(ItemKind.Stick));
        var bench = island.Structures.All.Single();
        Assert.Equal("bench", bench.DecorationId);
        Assert.False(Movement.CanStand(island, x + 1.5, y, 0.28));
        Assert.True(Movement.CanStand(island, x + 1.5, y + 1, 0.28));

        Assert.Equal(BuildResult.Removed, island.Structures.Remove(bench.Id, bag, x + 1, y));
        Assert.Equal(1, bag.Count(ItemKind.Stick));
    }

    /// <summary>Kruh z kamenů leží na zemi: dá se přes něj projít, rozebráním se vrátí 2 ze 4 kamenů.</summary>
    [Fact]
    public void StoneCircleIsWalkable()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var (x, y) = OpenSpot(island);
        var bag = Bag(sticks: 0, stones: 4);

        Assert.Equal(BuildResult.Built, Build(island, bag, x, y, x + 1.5, y, "stone_circle"));
        Assert.True(Movement.CanStand(island, x + 1.5, y, 0.28));

        island.Structures.Remove(island.Structures.All.Single().Id, bag, x + 1, y);
        Assert.Equal(2, bag.Count(ItemKind.Stone));
    }

    [Fact]
    public void NeedsMaterialsAndKnownKind()
    {
        var (x, y) = OpenSpot(_island);

        Assert.Equal(BuildResult.NeedMaterials, Build(_island, Bag(sticks: 4, stones: 0), x, y, x + 1, y, "totem"));
        Assert.Equal(BuildResult.CannotBuildHere, Build(_island, Bag(sticks: 9, stones: 9), x, y, x + 1, y, "neexistuje"));
        Assert.Equal(BuildResult.TooFar, Build(_island, Bag(sticks: 9, stones: 9), x, y, x + 5, y, "totem"));
    }

    /// <summary>Dvě dekorace se nepřekrývají a ta, přes kterou se nedá projít, nevyroste na hráči.</summary>
    [Fact]
    public void NoOverlapAndNotOnPlayer()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var (x, y) = OpenSpot(island);
        var bag = Bag(sticks: 20, stones: 20);

        Assert.Equal(BuildResult.Built, Build(island, bag, x, y, x + 1.5, y, "stone_circle"));
        Assert.Equal(BuildResult.CannotBuildHere, Build(island, bag, x, y, x + 1.9, y, "totem"));
        Assert.Equal(BuildResult.CannotBuildHere, island.Structures.Build(
            island, new Fires(), bag, x, y, BuildKind.Decoration, x, y - 1.5, 0, 0, [new Position(x, y - 1.5)], decorationId: "bench"));
    }

    private static BuildResult Build(Island island, Inventory bag, double px, double py, double x, double y, string id)
    {
        return island.Structures.Build(island, new Fires(), bag, px, py, BuildKind.Decoration, x, y, 0.3, 0, [], decorationId: id);
    }

    private static Inventory Bag(int sticks, int stones)
    {
        var bag = new Inventory(20);
        for (var i = 0; i < sticks; i++)
        {
            bag.TryAdd(Item.Stick);
        }

        for (var i = 0; i < stones; i++)
        {
            bag.TryAdd(Item.Stone);
        }

        return bag;
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

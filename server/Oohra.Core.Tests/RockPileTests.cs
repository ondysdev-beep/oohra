using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Hromady kamenů na skalách: kde leží, braní po jednom, prázdná zmizí a obnoví se, přes plnou se nedá projít.</summary>
public class RockPileTests
{
    private const double Day = 1440;

    [Fact]
    public void PilesLieOnPartOfRockTiles()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var piles = island.Rocks.All.ToList();
        var rockTiles = island.Tiles.Count(t => t is TileType.Rock or TileType.LayeredRock);
        var rules = GameData.Rocks;

        Assert.NotEmpty(piles);
        Assert.InRange(piles.Count, rockTiles * 0.25, rockTiles * 0.65);
        Assert.All(piles, p =>
        {
            Assert.Contains(island.Tiles[p.TileIndex], new[] { TileType.Rock, TileType.LayeredRock });
            Assert.False(island.HasTree(p.TileIndex));
            Assert.InRange(p.MaxStones, rules.MinStones, rules.MaxStones);
            Assert.Equal(p.MaxStones, p.Stones);
        });

        // Stejný seed dá stejné hromady.
        var again = WorldGenerator.Build(1, 128, 128).Rocks.All.Select(p => (p.TileIndex, p.MaxStones));
        Assert.Equal(piles.Select(p => (p.TileIndex, p.MaxStones)), again);
    }

    /// <summary>Kámen se bere po jednom. Prázdná hromada zmizí (dá se přes ni projít) a za 3 dny se obnoví.</summary>
    [Fact]
    public void TakeUntilEmptyThenRegrow()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var pile = island.Rocks.All.First();
        var (tx, ty) = (pile.TileIndex % island.Width, pile.TileIndex / island.Width);
        var bag = new Inventory(20);
        Assert.True(island.Rocks.Blocks(island, pile.X, pile.Y, 0.28));

        for (var i = 0; i < pile.MaxStones; i++)
        {
            Assert.Equal(MaterialResult.GotStone, Materials.TakeStone(island, tx, ty, bag, pile.X + 0.8, pile.Y, Day));
        }

        Assert.Equal(pile.MaxStones, bag.Count(ItemKind.Stone));
        Assert.Equal(MaterialResult.NothingLeft, Materials.TakeStone(island, tx, ty, bag, pile.X + 0.8, pile.Y, Day));
        Assert.False(island.Rocks.Blocks(island, pile.X, pile.Y, 0.28));

        island.Rocks.Regrow(Day + 2.9 * Day);
        Assert.Equal(0, pile.Stones);
        var version = island.Rocks.Version;
        island.Rocks.Regrow(Day + 3 * Day);
        Assert.Equal(pile.MaxStones, pile.Stones);
        Assert.True(island.Rocks.Version > version);
    }

    [Fact]
    public void RockWithoutPileGivesNothing()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var bare = Enumerable.Range(0, island.Tiles.Length).First(i => island.Tiles[i] == TileType.Rock && island.Rocks.At(i) is null);
        var (tx, ty) = (bare % island.Width, bare / island.Width);

        Assert.Equal(MaterialResult.NothingLeft, Materials.TakeStone(island, tx, ty, new Inventory(5), tx + 0.5, ty + 0.5, 0));
    }

    /// <summary>Přes plnou hromadu se nedá projít ani na ni stavět.</summary>
    [Fact]
    public void FullPileBlocksWalkingAndBuilding()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var pile = island.Rocks.All.First();

        Assert.False(Movement.CanStand(island, pile.X, pile.Y, 0.28));
        Assert.False(Placement.ClearOfTrunks(island, pile.X, pile.Y, pile.X, pile.Y, 0.3));
    }
}

using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

public class IslandTests
{
    [Fact]
    public void SameSeedGivesSameIsland()
    {
        var first = WorldGenerator.Build(777, 128, 128);
        var second = WorldGenerator.Build(777, 128, 128);

        Assert.Equal(first.Tiles, second.Tiles);
        Assert.Equal(first.Plants, second.Plants);
        Assert.Equal(first.Trees, second.Trees);
    }

    [Fact]
    public void DefaultSizeComesFromSettings()
    {
        var island = WorldGenerator.Build(1);

        Assert.Equal(GameData.Settings.WorldWidth, island.Width);
        Assert.Equal(GameData.Settings.WorldHeight, island.Height);
        Assert.Equal(384, island.Width);
    }

    [Fact]
    public void LargeIslandHasLandCampsAndPlants()
    {
        var island = WorldGenerator.Build(12345, 128, 128);

        Assert.Equal(2, island.Camps.Count);
        Assert.NotEmpty(island.Plants);
        Assert.NotEmpty(island.Trees);
        foreach (var camp in island.Camps)
        {
            var tile = island.TileAt(camp.X, camp.Y);
            Assert.NotEqual(TileType.Sea, tile);
            Assert.NotEqual(TileType.Pond, tile);
        }
    }

    [Fact]
    public void BorderOfIslandIsSea()
    {
        var island = WorldGenerator.Build(1, 128, 128);

        for (var x = 0; x < island.Width; x++)
        {
            Assert.Equal(TileType.Sea, island.TileAt(x, 0));
            Assert.Equal(TileType.Sea, island.TileAt(x, island.Height - 1));
        }
    }
}

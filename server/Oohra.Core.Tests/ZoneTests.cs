using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Zóny u vody: močál (nitro bažiny) a mělčina u moře.</summary>
public class ZoneTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 384, 384);

    private static (double X, double Y) First(TileType tile)
    {
        var i = Array.IndexOf(_island.Tiles, tile);
        Assert.True(i >= 0, "Na ostrově chybí " + tile);
        return (i % _island.Width + 0.5, i / _island.Width + 0.5);
    }

    [Fact]
    public void MireIsInsideMarshSlowerThanBogAndNotForBuilding()
    {
        var (x, y) = First(TileType.Mire);
        var (bx, by) = First(TileType.Bog);

        Assert.Equal(RegionKind.Marsh, _island.RegionAt((int)x, (int)y));
        Assert.True(Movement.TerrainSpeed(_island, x, y) < Movement.TerrainSpeed(_island, bx, by));
        Assert.True(Movement.CanStand(_island, x, y, 0.28), "Močálem se dá (pomalu) projít.");
        Assert.False(Placement.OnDryLand(_island, x, y), "V močálu se nestaví ani nerozdělává oheň.");
    }

    [Fact]
    public void ShallowsCanBeWadedButWaterIsSaltyAndCold()
    {
        var (x, y) = First(TileType.Shallows);
        Assert.Equal(RegionKind.Sea, _island.RegionAt((int)x, (int)y));
        Assert.True(Movement.CanStand(_island, x, y, 0.28));
        Assert.True(Movement.TerrainSpeed(_island, x, y) < 1);
        Assert.False(Placement.OnDryLand(_island, x, y));

        var c = new Character(1, new Mulberry32(1));
        c.Hydration = 50;
        Assert.Equal(DrinkResult.Salty, Drinking.Drink(c, _island, (int)x, (int)y, x, y, TestPlants.Clock()));

        // Brodění studí.
        var warm = c.Warmth;
        Hazards.Update(c, _island, new Fires(), x, y, TestPlants.Clock(), 10, new Mulberry32(1));
        Assert.Equal(warm - 10 * GameData.Hazards.Wading.WarmthPerMinute, c.Warmth, 6);
    }
}

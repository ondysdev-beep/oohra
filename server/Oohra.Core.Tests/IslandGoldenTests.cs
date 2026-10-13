using System.Text.Json;
using Oohra.Core.Plants;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Ostrov z C# se musí shodovat s ostrovem z prototypu (golden data, 84 × 84).</summary>
public class IslandGoldenTests
{
    private const int GoldenSize = 84;
    private const double Tolerance = 1e-9;

    public static TheoryData<int> Seeds => [1, 12345, 987654321];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TilesMatchExactly(int seed)
    {
        var island = Build(seed);
        var expected = Golden(seed).GetProperty("tiles").GetString();

        var actual = string.Concat(island.Tiles.Select(t => (char)('0' + (int)t)));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ShoreMatches(int seed)
    {
        var island = Build(seed);

        Assert.Equal(Golden(seed).GetProperty("shoreCount").GetInt32(), island.Shore.Count(s => s));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SpeciesMatch(int seed)
    {
        var island = Build(seed);
        var expected = Golden(seed).GetProperty("species").EnumerateArray().ToList();

        Assert.Equal(expected.Count, island.Species.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var species = island.Species[i];
            var secret = island.SpeciesSecrets[i];
            Assert.Equal(e.GetProperty("id").GetInt32(), species.Id);
            Assert.Equal(e.GetProperty("arch").GetInt32(), secret.ArchetypeIndex);
            Assert.Equal(e.GetProperty("hue").GetDouble(), species.Hue, Tolerance);
            Assert.Equal(e.GetProperty("sat").GetDouble(), species.Saturation, Tolerance);
            Assert.Equal(e.GetProperty("lit").GetDouble(), species.Lightness, Tolerance);
            Assert.Equal(e.GetProperty("fh").GetInt32(), species.FruitHue);
            Assert.Equal(e.GetProperty("shape").GetInt32(), species.Shape);
            Assert.Equal(e.GetProperty("start").GetInt32(), species.StartMonth);

            var jitter = e.GetProperty("jit");
            AssertDoubles(jitter.GetProperty("leaf"), secret.JitterFor(PlantPart.Leaf));
            AssertDoubles(jitter.GetProperty("root"), secret.JitterFor(PlantPart.Root));
            AssertDoubles(jitter.GetProperty("fruit"), secret.JitterFor(PlantPart.Fruit));
        }
    }

    // V golden datech se pole jmenuje "camps", ale obsahuje kmeny (id, barva, tábor). Kontrolujeme polohu táborů.
    [Theory]
    [MemberData(nameof(Seeds))]
    public void CampsMatch(int seed)
    {
        var island = Build(seed);
        var expected = Golden(seed).GetProperty("camps").EnumerateArray()
            .Select(t => t.GetProperty("camp"))
            .Select(c => new GridPoint(c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32()))
            .ToList();

        Assert.Equal(expected, island.Camps);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RuinSpotsMatch(int seed)
    {
        var island = Build(seed);
        var expected = Golden(seed).GetProperty("ruinSpots").EnumerateArray()
            .Select(p => new Position(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble()))
            .ToList();

        Assert.Equal(expected, island.RuinSpots);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TreesMatch(int seed)
    {
        var island = Build(seed);
        var golden = Golden(seed);
        var expected = golden.GetProperty("treesFirst").EnumerateArray().ToList();

        Assert.Equal(golden.GetProperty("treeCount").GetInt32(), island.Trees.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var index = expected[i][0].GetInt32();
            var tree = expected[i][1];
            Assert.Equal(index, island.Trees[i].TileIndex);
            Assert.Equal(tree.GetProperty("x").GetDouble(), island.Trees[i].X);
            Assert.Equal(tree.GetProperty("y").GetDouble(), island.Trees[i].Y);
            Assert.Equal(tree.GetProperty("v").GetDouble(), island.Trees[i].Variant, Tolerance);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void PlantsMatch(int seed)
    {
        var island = Build(seed);
        var golden = Golden(seed);
        var expected = golden.GetProperty("plantsFirst").EnumerateArray().ToList();

        Assert.Equal(golden.GetProperty("plantCount").GetInt32(), island.Plants.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var plant = island.Plants[i];
            Assert.Equal(e.GetProperty("id").GetInt32(), plant.Id);
            Assert.Equal(e.GetProperty("sp").GetInt32(), plant.SpeciesId);
            Assert.Equal(e.GetProperty("x").GetDouble(), plant.X, Tolerance);
            Assert.Equal(e.GetProperty("y").GetDouble(), plant.Y, Tolerance);
            Assert.Equal(e.GetProperty("tile").GetInt32(), (int)plant.Tile);
            Assert.Equal(e.GetProperty("off").GetInt32(), plant.FruitOffset);
        }
    }

    private static Island Build(int seed)
    {
        return IslandGenerator.Build(seed, GoldenSize, GoldenSize);
    }

    private static JsonElement Golden(int seed)
    {
        return GoldenData.ForSeed(seed);
    }

    private static void AssertDoubles(JsonElement expected, double[] actual)
    {
        var values = GoldenData.Doubles(expected);
        Assert.Equal(values.Length, actual.Length);
        for (var i = 0; i < values.Length; i++)
        {
            Assert.Equal(values[i], actual[i], Tolerance);
        }
    }
}

using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Secret;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>
/// Složení rostlin podle pravidel z prototypu (comp v part2.js).
/// Druh má odchylky přesně 1, takže čísla jde ověřit ručně z archetypes.json a plant_rules.json.
/// </summary>
public class CompositionTests
{
    private const double Tolerance = 1e-12;

    // Archetyp 1: výživný kořen [0,9; 0; 0,8; ...], upečení násobí toxin 0,04.
    [Fact]
    public void RawPoisonRootIsToxicAndBakingRemovesToxin()
    {
        var raw = Compute(1, PlantPart.Root);
        var baked = Compute(1, PlantPart.Root, cooking: Cooking.Baked);

        Assert.Equal(0.9, raw.Nutrition, Tolerance);
        Assert.Equal(0.8, raw.Toxin, Tolerance);
        Assert.Equal(0.9 * 1.1, baked.Nutrition, Tolerance);
        Assert.Equal(0.8 * 0.04, baked.Toxin, Tolerance);
    }

    // Archetyp 3: list je na jaře výživný, v zimě má dvacetkrát víc toxinu.
    [Fact]
    public void SeasonalLeafGetsToxicInWinter()
    {
        var spring = Compute(3, PlantPart.Leaf, season: Season.Spring);
        var winter = Compute(3, PlantPart.Leaf, season: Season.Winter);

        Assert.Equal(0.6 * 1.2, spring.Nutrition, Tolerance);
        Assert.Equal(0.05, spring.Toxin, Tolerance);
        Assert.Equal(0.6 * 0.3, winter.Nutrition, Tolerance);
        Assert.Equal(0.05 * 20, winter.Toxin, Tolerance);
    }

    [Fact]
    public void SeasonDoesNotChangeOtherParts()
    {
        var spring = Compute(3, PlantPart.Root, season: Season.Spring);
        var winter = Compute(3, PlantPart.Root, season: Season.Winter);

        Assert.Equal(spring, winter);
    }

    // Archetyp 0: zralý plod má výživu 0,75. Nezralý má méně výživy a dráždí, přezrálý trochu.
    [Fact]
    public void FruitRipenessChangesNutritionAndIrritant()
    {
        var ripe = Compute(0, PlantPart.Fruit, Ripeness.Ripe);
        var unripe = Compute(0, PlantPart.Fruit, Ripeness.Unripe);
        var overripe = Compute(0, PlantPart.Fruit, Ripeness.Overripe);

        Assert.Equal(0.75, ripe.Nutrition, Tolerance);
        Assert.Equal(0.0, ripe.Irritant, Tolerance);
        Assert.Equal(0.75 * 0.45, unripe.Nutrition, Tolerance);
        Assert.Equal(0.4, unripe.Irritant, Tolerance);
        Assert.Equal(0.75 * 0.7, overripe.Nutrition, Tolerance);
        Assert.Equal(0.2, overripe.Irritant, Tolerance);
    }

    // Archetyp 6 má vlastní dráždivost nezralého plodu: 0.
    [Fact]
    public void ArchetypeCanOverrideUnripeIrritant()
    {
        var unripe = Compute(6, PlantPart.Fruit, Ripeness.Unripe);

        Assert.Equal(0.0, unripe.Irritant, Tolerance);
        Assert.Equal(0.85, unripe.Neurotoxin, Tolerance);
    }

    [Fact]
    public void RipenessDoesNotChangeLeaf()
    {
        var a = Compute(0, PlantPart.Leaf, Ripeness.Unripe);
        var b = Compute(0, PlantPart.Leaf, Ripeness.Overripe);

        Assert.Equal(a, b);
    }

    // Bažina zesiluje toxin (×1,35), skála ubírá výživu (×0,7), tráva nic nemění.
    [Fact]
    public void SoilChangesComposition()
    {
        var grass = Compute(1, PlantPart.Root, soil: TileType.Grass);
        var bog = Compute(1, PlantPart.Root, soil: TileType.Bog);
        var rock = Compute(1, PlantPart.Root, soil: TileType.Rock);

        Assert.Equal(0.8, grass.Toxin, Tolerance);
        Assert.Equal(0.8 * 1.35, bog.Toxin, Tolerance);
        Assert.Equal(0.9 * 0.7, rock.Nutrition, Tolerance);
    }

    // Archetyp 2: pálivý list 0,9. Upečení ×0,3, vaření navíc ×0,45.
    [Fact]
    public void BoilingReducesIrritantMoreThanBaking()
    {
        var baked = Compute(2, PlantPart.Leaf, cooking: Cooking.Baked);
        var boiled = Compute(2, PlantPart.Leaf, cooking: Cooking.Boiled);

        Assert.Equal(0.9 * 0.3, baked.Irritant, Tolerance);
        Assert.Equal(0.9 * 0.3 * 0.45, boiled.Irritant, Tolerance);
    }

    [Fact]
    public void BreedingLowersToxinAndRaisesNutrition()
    {
        var bred = Compute(1, PlantPart.Root, bredLevel: 2);

        Assert.Equal(0.8 * (1 - 0.12 * 2), bred.Toxin, Tolerance);
        Assert.Equal(0.9 * (1 + 0.06 * 2), bred.Nutrition, Tolerance);
    }

    [Fact]
    public void MissingFruitHasZeroComposition()
    {
        var fruit = Compute(1, PlantPart.Fruit);

        Assert.Equal(default, fruit);
    }

    [Fact]
    public void SpeciesJitterMultipliesComposition()
    {
        var secret = new SpeciesSecret
        {
            Id = 0,
            ArchetypeIndex = 1,
            LeafJitter = Ones(),
            RootJitter = [0.9, 1, 1.2, 1, 1, 1],
            FruitJitter = Ones(),
        };

        var root = CompositionCalculator.Compute(Item(PlantPart.Root), secret);

        Assert.Equal(0.9 * 0.9, root.Nutrition, Tolerance);
        Assert.Equal(0.8 * 1.2, root.Toxin, Tolerance);
    }

    [Fact]
    public void ComputeDoesNotChangeArchetypeData()
    {
        Compute(1, PlantPart.Root, soil: TileType.Bog, cooking: Cooking.Baked);

        Assert.Equal([0.9, 0, 0.8, 0, 0, 0], GameData.Archetypes[1].Parts.Root);
    }

    private static Composition Compute(
        int archetype,
        PlantPart part,
        Ripeness ripeness = Ripeness.Ripe,
        Season season = Season.Summer,
        TileType soil = TileType.Grass,
        Cooking cooking = Cooking.Raw,
        int bredLevel = 0)
    {
        var secret = new SpeciesSecret
        {
            Id = 0,
            ArchetypeIndex = archetype,
            LeafJitter = Ones(),
            RootJitter = Ones(),
            FruitJitter = Ones(),
        };

        return CompositionCalculator.Compute(Item(part, ripeness, season, soil, cooking, bredLevel), secret);
    }

    private static PlantItem Item(
        PlantPart part,
        Ripeness ripeness = Ripeness.Ripe,
        Season season = Season.Summer,
        TileType soil = TileType.Grass,
        Cooking cooking = Cooking.Raw,
        int bredLevel = 0)
    {
        return new PlantItem(0, part, ripeness, season, soil, cooking, bredLevel);
    }

    private static double[] Ones()
    {
        return [1, 1, 1, 1, 1, 1];
    }
}

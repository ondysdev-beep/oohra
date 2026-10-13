using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Secret;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

public class SpeciesTests
{
    [Fact]
    public void ArchetypeDataIsComplete()
    {
        var archetypes = GameData.Archetypes;

        Assert.Equal(8, archetypes.Count);
        foreach (var archetype in archetypes)
        {
            Assert.NotEmpty(archetype.Home);
            Assert.Equal(6, archetype.Parts.Leaf.Length);
            Assert.Equal(6, archetype.Parts.Root.Length);
            Assert.Equal(6, archetype.Cook.Length);
            if (archetype.Parts.Fruit is not null)
            {
                Assert.Equal(6, archetype.Parts.Fruit.Length);
            }

            if (archetype.Seasonal is not null)
            {
                Assert.Equal(4, archetype.Seasonal.Multipliers.Length);
                Assert.All(archetype.Seasonal.Multipliers, m => Assert.Equal(6, m.Length));
            }
        }
    }

    // Které archetypy plodí a které mají zvláštnosti, je v prototypu pevně dané.
    [Fact]
    public void ArchetypesMatchPrototypeFlags()
    {
        var archetypes = GameData.Archetypes;

        Assert.Equal([true, false, true, false, true, true, true, true], archetypes.Select(a => a.HasFruit));
        Assert.Equal([true, false, false, true, true, true, false, false], archetypes.Select(a => a.Rabbit));
        Assert.Equal(0.0, archetypes[6].UnripeIrritant);
        Assert.Equal(PlantPart.Leaf, archetypes[3].Seasonal!.Part);
        Assert.Equal(PlantPart.Root, archetypes[7].Seasonal!.Part);
    }

    [Fact]
    public void EachArchetypeHasExactlyOneSpecies()
    {
        var result = SpeciesGenerator.Build(12345, GameData.Archetypes.Count, 12);

        Assert.Equal(Enumerable.Range(0, 8), result.Secrets.Select(s => s.ArchetypeIndex).Order());
    }

    [Fact]
    public void PlantRulesAreLoaded()
    {
        var rules = GameData.PlantRules;

        Assert.Equal(1.35, rules.Soil[TileType.Bog][2]);
        Assert.False(rules.Soil.ContainsKey(TileType.Grass));
    }

    [Fact]
    public void MutationIsDeterministic()
    {
        var first = SpeciesGenerator.Build(1, 8, 12);
        var second = SpeciesGenerator.Build(1, 8, 12);

        SpeciesMutation.Apply(first.Species, first.Secrets, 1, 1, 3, 12);
        SpeciesMutation.Apply(second.Species, second.Secrets, 1, 1, 3, 12);

        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(first.Species[i].Hue, second.Species[i].Hue);
            Assert.Equal(first.Species[i].StartMonth, second.Species[i].StartMonth);
            Assert.Equal(first.Secrets[i].RootJitter, second.Secrets[i].RootJitter);
        }
    }

    // V prototypu se epochy dělají najednou při načtení světa i po jedné při nové epoše. Výsledek musí být stejný.
    [Fact]
    public void MutatingEpochsOneByOneEqualsAllAtOnce()
    {
        var together = SpeciesGenerator.Build(42, 8, 12);
        var oneByOne = SpeciesGenerator.Build(42, 8, 12);

        SpeciesMutation.Apply(together.Species, together.Secrets, 42, 1, 2, 12);
        SpeciesMutation.Apply(oneByOne.Species, oneByOne.Secrets, 42, 1, 1, 12);
        SpeciesMutation.Apply(oneByOne.Species, oneByOne.Secrets, 42, 2, 2, 12);

        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(together.Species[i].Hue, oneByOne.Species[i].Hue);
            Assert.Equal(together.Secrets[i].LeafJitter, oneByOne.Secrets[i].LeafJitter);
        }
    }

    [Fact]
    public void MutationChangesPlantsWithinLimits()
    {
        var original = SpeciesGenerator.Build(7, 8, 12);
        var mutated = SpeciesGenerator.Build(7, 8, 12);

        SpeciesMutation.Apply(mutated.Species, mutated.Secrets, 7, 1, 20, 12);

        Assert.NotEqual(original.Secrets[0].LeafJitter, mutated.Secrets[0].LeafJitter);
        foreach (var secret in mutated.Secrets)
        {
            var all = secret.LeafJitter.Concat(secret.RootJitter).Concat(secret.FruitJitter);
            Assert.All(all, j => Assert.InRange(j, 0.45, 1.9));
        }

        Assert.All(mutated.Species, s => Assert.InRange(s.StartMonth, 0, 11));
    }
}

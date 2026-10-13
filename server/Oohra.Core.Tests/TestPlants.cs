using Oohra.Core.Characters;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Secret;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Pomůcky pro testy těla: druh s odchylkami 1, postava a hodiny.</summary>
public static class TestPlants
{
    // Archetypy (viz archetypes.json): 0 bezpečný plod, 1 jedovatý kořen, 2 pálivý list a léčivý kořen,
    // 4 povzbuzující list, 6 plod s nervovým jedem.
    public const int SafeFruit = 0;
    public const int PoisonRoot = 1;
    public const int BurningLeaf = 2;
    public const int StimulantLeaf = 4;
    public const int NeurotoxinFruit = 6;

    public static SpeciesSecret Species(int archetype)
    {
        return new SpeciesSecret
        {
            Id = 0,
            ArchetypeIndex = archetype,
            LeafJitter = [1, 1, 1, 1, 1, 1],
            RootJitter = [1, 1, 1, 1, 1, 1],
            FruitJitter = [1, 1, 1, 1, 1, 1],
        };
    }

    public static PlantItem Item(PlantPart part, Ripeness ripeness = Ripeness.Ripe)
    {
        return new PlantItem(0, part, ripeness, Season.Spring, TileType.Grass);
    }

    public static Character Person(int generation = 0, int seed = 1)
    {
        return new Character(generation, new Mulberry32(seed));
    }

    /// <summary>Jarní ráno, den 90 minut, měsíc 9 hodin.</summary>
    public static GameClock Clock()
    {
        return new GameClock(7 * 60, 5400, 32400, 12);
    }
}

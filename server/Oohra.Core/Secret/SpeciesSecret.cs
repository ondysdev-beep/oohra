using Oohra.Core.Plants;

namespace Oohra.Core.Secret;

/// <summary>
/// Skryté vlastnosti druhu rostliny: archetyp a náhodné odchylky složení.
/// Jen na serveru. Id je stejné jako u PlantSpecies.
/// </summary>
public sealed class SpeciesSecret
{
    public required int Id { get; init; }

    /// <summary>Index do GameData.Archetypes.</summary>
    public required int ArchetypeIndex { get; init; }

    /// <summary>Odchylky složení listu (6 násobků zhruba 0,85 až 1,2).</summary>
    public required double[] LeafJitter { get; init; }

    public required double[] RootJitter { get; init; }

    public required double[] FruitJitter { get; init; }

    public double[] JitterFor(PlantPart part)
    {
        return part switch
        {
            PlantPart.Leaf => LeafJitter,
            PlantPart.Root => RootJitter,
            PlantPart.Fruit => FruitJitter,
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };
    }
}

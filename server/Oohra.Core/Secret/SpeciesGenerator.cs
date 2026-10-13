using Oohra.Core.Plants;
using Oohra.Core.Rng;

namespace Oohra.Core.Secret;

/// <summary>Druhy rostlin ze seedu světa, přepis buildSpecies z prototypu (part1.js).</summary>
public static class SpeciesGenerator
{
    // Barvy a tvary z prototypu. Pořadí a hodnoty musí zůstat, jinak se změní svět pro daný seed.
    private static readonly int[] _leafHues = [95, 120, 145, 170, 75, 300, 18, 55];
    private static readonly int[] _fruitHues = [0, 25, 48, 280, 330, 200, 160, 350];
    private static readonly int[] _shapes = [0, 1, 2, 3, 0, 1, 2, 3];

    public sealed record Result(IReadOnlyList<PlantSpecies> Species, IReadOnlyList<SpeciesSecret> Secrets);

    /// <summary>Každý archetyp dostane jeden druh v náhodném pořadí.</summary>
    public static Result Build(int seed, int archetypeCount, int monthsPerYear)
    {
        var rng = new Mulberry32(seed ^ 0x51ED);
        var order = rng.Shuffle(Enumerable.Range(0, archetypeCount).ToArray());
        var leafHues = rng.Shuffle(_leafHues);
        var fruitHues = rng.Shuffle(_fruitHues);
        var shapes = rng.Shuffle(_shapes);

        var species = new List<PlantSpecies>();
        var secrets = new List<SpeciesSecret>();
        for (var i = 0; i < order.Length; i++)
        {
            // Pořadí volání rng je stejné jako v prototypu: odchylky, odstín, sytost, světlost, měsíc.
            var leafJitter = Jitter(rng);
            var rootJitter = Jitter(rng);
            var fruitJitter = Jitter(rng);
            var hue = leafHues[i] + (rng.NextDouble() * 14 - 7);
            var saturation = 34 + rng.NextDouble() * 20;
            var lightness = 36 + rng.NextDouble() * 14;
            var startMonth = rng.NextInt(monthsPerYear);

            species.Add(new PlantSpecies
            {
                Id = i,
                Hue = hue,
                Saturation = saturation,
                Lightness = lightness,
                FruitHue = fruitHues[i],
                Shape = shapes[i],
                StartMonth = startMonth,
            });
            secrets.Add(new SpeciesSecret
            {
                Id = i,
                ArchetypeIndex = order[i],
                LeafJitter = leafJitter,
                RootJitter = rootJitter,
                FruitJitter = fruitJitter,
            });
        }

        return new Result(species, secrets);
    }

    private static double[] Jitter(Mulberry32 rng)
    {
        var values = new double[6];
        for (var k = 0; k < values.Length; k++)
        {
            values[k] = 0.85 + rng.NextDouble() * 0.35;
        }

        return values;
    }
}

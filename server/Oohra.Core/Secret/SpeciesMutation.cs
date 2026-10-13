using Oohra.Core.Plants;
using Oohra.Core.Rng;

namespace Oohra.Core.Secret;

/// <summary>
/// Nová epocha: rostliny se trochu posunou a staré znalosti mohou přestat platit
/// (přepis mutateSpecies z prototype/src/part1.js). Čísla jsou z prototypu kvůli stejnému výsledku pro seed.
/// </summary>
public static class SpeciesMutation
{
    private const double MinJitter = 0.45;
    private const double MaxJitter = 1.9;

    /// <summary>Provede epochy fromEpoch až toEpoch (včetně). Mění druhy na místě.</summary>
    public static void Apply(
        IReadOnlyList<PlantSpecies> species,
        IReadOnlyList<SpeciesSecret> secrets,
        int seed,
        int fromEpoch,
        int toEpoch,
        int monthsPerYear)
    {
        for (var epoch = fromEpoch; epoch <= toEpoch; epoch++)
        {
            var rng = new Mulberry32(seed ^ unchecked(epoch * 7919));
            for (var i = 0; i < species.Count; i++)
            {
                MutateJitter(rng, secrets[i].LeafJitter);
                MutateJitter(rng, secrets[i].RootJitter);
                MutateJitter(rng, secrets[i].FruitJitter);
                species[i].Hue += rng.NextDouble() * 24 - 12;
                species[i].StartMonth = (species[i].StartMonth + rng.NextInt(3) - 1 + monthsPerYear) % monthsPerYear;
            }
        }
    }

    /// <summary>Každá odchylka se buď zmenší (×0,78 až 0,98), nebo zvětší (×1 až 1,3).</summary>
    private static void MutateJitter(Mulberry32 rng, double[] jitter)
    {
        for (var k = 0; k < jitter.Length; k++)
        {
            var factor = rng.NextDouble() < 0.5 ? 0.78 + rng.NextDouble() * 0.2 : 1 + rng.NextDouble() * 0.3;
            jitter[k] = Math.Clamp(jitter[k] * factor, MinJitter, MaxJitter);
        }
    }
}

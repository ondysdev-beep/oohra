using Oohra.Core.Rng;

namespace Oohra.Core.Tests;

public class RngTests
{
    // V golden datech je prvních 10 čísel z mulberry32(seed).
    [Theory]
    [InlineData(1)]
    [InlineData(12345)]
    [InlineData(987654321)]
    public void Mulberry32MatchesPrototype(int seed)
    {
        var expected = GoldenData.Doubles(GoldenData.ForSeed(seed).GetProperty("prng"));
        var rng = new Mulberry32(seed);

        foreach (var value in expected)
        {
            Assert.Equal(value, rng.NextDouble());
        }
    }

    // V golden datech je hash2(7·i, 13·i + 1, seed) pro i = 0 až 7.
    [Theory]
    [InlineData(1)]
    [InlineData(12345)]
    [InlineData(987654321)]
    public void Hash2MatchesPrototype(int seed)
    {
        var expected = GoldenData.Doubles(GoldenData.ForSeed(seed).GetProperty("hash2"));

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], Hash.Hash2(7 * i, 13 * i + 1, seed));
        }
    }

    [Fact]
    public void Mulberry32GivesSameSequenceForSameSeed()
    {
        var first = new Mulberry32(42);
        var second = new Mulberry32(42);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(first.NextDouble(), second.NextDouble());
        }
    }

    [Fact]
    public void Mulberry32StaysInRange()
    {
        var rng = new Mulberry32(7);

        for (var i = 0; i < 10000; i++)
        {
            var value = rng.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0, $"Číslo {value} je mimo rozsah 0 až 1.");
        }
    }
}

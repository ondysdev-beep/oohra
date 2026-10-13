using System.Text.Json;

namespace Oohra.Core.Tests;

/// <summary>Načte golden data z prototypu (golden/golden_world.json).</summary>
public static class GoldenData
{
    private static readonly Lazy<JsonDocument> _document = new(Load);

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "golden", "golden_world.json");

    /// <summary>Data jednoho seedu (pole "seeds" v souboru).</summary>
    public static JsonElement ForSeed(int seed)
    {
        foreach (var entry in _document.Value.RootElement.GetProperty("seeds").EnumerateArray())
        {
            if (entry.GetProperty("seed").GetInt32() == seed)
            {
                return entry;
            }
        }

        throw new ArgumentException($"Seed {seed} v golden datech není.");
    }

    public static double[] Doubles(JsonElement array)
    {
        return array.EnumerateArray().Select(e => e.GetDouble()).ToArray();
    }

    private static JsonDocument Load()
    {
        return JsonDocument.Parse(File.ReadAllText(FilePath));
    }
}

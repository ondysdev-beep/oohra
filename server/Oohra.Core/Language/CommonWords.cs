using Oohra.Core.Data;
using Oohra.Core.Rng;

namespace Oohra.Core.Language;

/// <summary>
/// Starý hlas: slova z jazyka Pradávných, která vychází ze seedu světa (prototyp makeVocab a common v part1.js).
/// Každý pojem dostane slovo ze dvou různých slabik, žádná dvě slova nejsou stejná.
/// </summary>
public static class CommonWords
{
    public static IReadOnlyDictionary<Concept, byte[]> For(int worldSeed)
    {
        var rules = GameData.Language.CommonWords;
        var r = new Mulberry32(worldSeed ^ rules.SeedSalt);
        var used = new HashSet<string>();
        var words = new Dictionary<Concept, byte[]>();
        foreach (var concept in rules.Concepts)
        {
            byte[] word;
            do
            {
                word = [(byte)r.NextInt(10), (byte)r.NextInt(10)];
            }
            while (used.Contains(Words.Key(word)) || word[0] == word[1]);

            used.Add(Words.Key(word));
            words[concept] = word;
        }

        return words;
    }
}

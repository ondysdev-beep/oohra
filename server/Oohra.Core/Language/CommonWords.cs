using Oohra.Core.Data;
using Oohra.Core.Rng;

namespace Oohra.Core.Language;

/// <summary>
/// Starý hlas: slova z jazyka Pradávných, která vychází ze seedu světa (prototyp makeVocab a common v part1.js).
/// Každý pojem dostane slovo ze dvou různých slabik, žádná dvě slova nejsou stejná.
/// Ze stejné řady náhod vzniknou i jazyky dvou cizích kmenů (prototyp vA a vB).
/// </summary>
public static class CommonWords
{
    public static IReadOnlyDictionary<Concept, byte[]> For(int worldSeed)
    {
        return MakeVocabulary(NewRandom(worldSeed), new Dictionary<Concept, byte[]>());
    }

    /// <summary>
    /// Jazyky dvou cizích kmenů (prototyp: vA a vB). Voda, nebezpečí, ano a ne jsou ze Starého hlasu a znají je
    /// oba kmeny. Druhý kmen má prohozená slova pro jídlo a jed: co jeden říká jídlu, druhý říká jedu.
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<Concept, byte[]>> ForStrangers(int worldSeed)
    {
        var r = NewRandom(worldSeed);
        var all = MakeVocabulary(r, new Dictionary<Concept, byte[]>());
        var common = new Dictionary<Concept, byte[]>
        {
            [Concept.Water] = all[Concept.Water],
            [Concept.Danger] = all[Concept.Danger],
            [Concept.Yes] = all[Concept.Yes],
            [Concept.No] = all[Concept.No],
        };
        var first = MakeVocabulary(r, common);
        var second = MakeVocabulary(r, new Dictionary<Concept, byte[]>(common)
        {
            [Concept.Food] = first[Concept.Poison],
            [Concept.Poison] = first[Concept.Food],
        });
        return [first, second];
    }

    private static Mulberry32 NewRandom(int worldSeed)
    {
        return new Mulberry32(worldSeed ^ GameData.Language.CommonWords.SeedSalt);
    }

    /// <summary>Prototyp makeVocab: pevná slova zůstanou, ostatní pojmy dostanou nová slova, různá od pevných.</summary>
    private static Dictionary<Concept, byte[]> MakeVocabulary(Mulberry32 r, Dictionary<Concept, byte[]> fixedWords)
    {
        var used = new HashSet<string>(fixedWords.Values.Select(Words.Key));
        var words = new Dictionary<Concept, byte[]>();
        foreach (var concept in GameData.Language.CommonWords.Concepts)
        {
            if (fixedWords.TryGetValue(concept, out var fixedWord))
            {
                words[concept] = (byte[])fixedWord.Clone();
                continue;
            }

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

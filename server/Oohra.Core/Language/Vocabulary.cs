using Oohra.Core.Data;

namespace Oohra.Core.Language;

/// <summary>Jedno slovo v zápisníku: kolikrát ho hráč slyšel, v jakých situacích a co si myslí, že znamená.</summary>
public sealed class VocabularyEntry
{
    public VocabularyEntry(byte[] word)
    {
        Word = word;
    }

    public byte[] Word { get; }

    public int Heard { get; set; }

    /// <summary>Kolikrát slovo zaznělo v jaké situaci (index = SpeechContext).</summary>
    public int[] Contexts { get; } = new int[Words.ContextCount];

    /// <summary>Význam, který slovu přiřadil hráč (null = neví). Hra ho nikdy nepotvrzuje.</summary>
    public Concept? Meaning { get; set; }

    /// <summary>Slovo ze Starého hlasu (Pradávný ho zná od narození).</summary>
    public bool OldVoice { get; set; }
}

/// <summary>
/// Osobní zápisník slov (návrh 9.4, prototyp S.vocab). Patří postavě, smrtí se ztratí.
/// </summary>
public sealed class Vocabulary
{
    private readonly Dictionary<string, VocabularyEntry> _entries = [];

    /// <summary>Slova seřazená podle toho, kolikrát zazněla (nejčastější první).</summary>
    public IReadOnlyList<VocabularyEntry> Entries =>
        _entries.Values.OrderByDescending(e => e.Heard).ThenBy(e => Words.Key(e.Word), StringComparer.Ordinal).ToList();

    /// <summary>Naposledy slyšené slovo (pro tlačítko Zopakovat), nebo null.</summary>
    public byte[]? LastHeard { get; private set; }

    /// <summary>Roste s každou změnou (server podle toho pošle zápisník znovu).</summary>
    public int Version { get; private set; }

    public VocabularyEntry? Find(IReadOnlyList<byte> word)
    {
        return _entries.GetValueOrDefault(Words.Key(word));
    }

    /// <summary>
    /// Hráč slovo zaslechl. Souvislosti (bity SpeechContext) se zapíšou, jen když mluvčího viděl zblízka.
    /// </summary>
    public void Hear(byte[] word, int contexts, bool seesSpeaker)
    {
        var entry = GetOrAdd(word);
        entry.Heard++;
        if (seesSpeaker)
        {
            for (var i = 0; i < Words.ContextCount; i++)
            {
                if ((contexts & (1 << i)) != 0)
                {
                    entry.Contexts[i]++;
                }
            }

            LastHeard = word;
        }

        Version++;
    }

    /// <summary>Hráč si slovu zapíše význam (nebo null = smaže ho). Jde to jen u slova, které zná.</summary>
    public bool SetMeaning(IReadOnlyList<byte> word, Concept? meaning)
    {
        var entry = Find(word);
        if (entry is null)
        {
            return false;
        }

        entry.Meaning = meaning;
        Version++;
        return true;
    }

    /// <summary>Starý hlas (prototyp grantOldVoice): Pradávný zná společná slova s významem od narození.</summary>
    public void GrantOldVoice(IReadOnlyDictionary<Concept, byte[]> commonWords)
    {
        foreach (var concept in GameData.Language.CommonWords.OldVoice)
        {
            var entry = GetOrAdd(commonWords[concept]);
            entry.Meaning = concept;
            entry.OldVoice = true;
        }

        Version++;
    }

    /// <summary>
    /// Odhad významu podle nejčastější souvislosti (prototyp vocabInfo: „asi: jídlo“), nebo null, když slovo
    /// v žádné souvislosti nezaznělo. Při shodě počtů vyhrává dřívější souvislost.
    /// </summary>
    public static Concept? Guess(VocabularyEntry entry)
    {
        var best = -1;
        for (var i = 0; i < Words.ContextCount; i++)
        {
            if (entry.Contexts[i] > 0 && (best < 0 || entry.Contexts[i] > entry.Contexts[best]))
            {
                best = i;
            }
        }

        return best < 0 ? null : Words.GuessOf((SpeechContext)best);
    }

    private VocabularyEntry GetOrAdd(byte[] word)
    {
        var key = Words.Key(word);
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new VocabularyEntry((byte[])word.Clone());
            _entries[key] = entry;
        }

        return entry;
    }
}

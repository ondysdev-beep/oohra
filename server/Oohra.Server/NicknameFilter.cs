using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace Oohra.Server;

/// <summary>
/// Hlídá přezdívky: mohou být jakékoli, jen ne sprosté nebo urážlivé. Seznam slov je v blocked_words.json
/// (správce ho může rozšířit). Porovnává se po úpravě, takže nepomůže ani diakritika, velká písmena, mezery,
/// čísla místo písmen (k0k0t) ani opakovaná písmena (kuuurva).
/// </summary>
public sealed class NicknameFilter
{
    private readonly List<string> _blocked;
    private readonly List<string> _allowed;

    public NicknameFilter(IEnumerable<string> blocked, IEnumerable<string>? allowed = null)
    {
        _blocked = blocked.Select(Normalize).Where(w => w.Length > 0).ToList();
        _allowed = (allowed ?? []).Select(Normalize).Where(w => w.Length > 0).ToList();
    }

    /// <summary>Načte seznam ze souboru. Když soubor chybí, nic se nezakazuje.</summary>
    public static NicknameFilter Load(string path)
    {
        if (!File.Exists(path))
        {
            return new NicknameFilter([]);
        }

        var lists = JsonConvert.DeserializeObject<WordLists>(File.ReadAllText(path)) ?? new WordLists();
        return new NicknameFilter(lists.Blocked, lists.Allowed);
    }

    /// <summary>Je v přezdívce sprosté slovo?</summary>
    public bool IsBlocked(string nickname)
    {
        var text = Normalize(nickname);
        foreach (var allowed in _allowed)
        {
            text = text.Replace(allowed, " ");
        }

        return _blocked.Any(text.Contains);
    }

    /// <summary>Malá písmena bez diakritiky, čísla a znaky přepsané na písmena, jen písmena, opakovaná slitá.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var letter = ch switch
            {
                '0' => 'o',
                '1' or '!' or '|' => 'i',
                '3' => 'e',
                '4' or '@' => 'a',
                '5' or '$' => 's',
                '7' => 't',
                '8' => 'b',
                _ => ch,
            };
            if (letter is >= 'a' and <= 'z' && (builder.Length == 0 || builder[^1] != letter))
            {
                builder.Append(letter);
            }
        }

        return builder.ToString();
    }

    private sealed class WordLists
    {
        public List<string> Blocked { get; set; } = [];

        public List<string> Allowed { get; set; } = [];
    }
}

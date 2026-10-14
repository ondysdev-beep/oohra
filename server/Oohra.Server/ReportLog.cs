using Newtonsoft.Json;

namespace Oohra.Server;

/// <summary>
/// Jedno nahlášení (tlačítko Nahlásit). Co se nevztahuje k cíli (třeba značka u nahlášení hráče), zůstane prázdné
/// a do souboru se nezapíše.
/// </summary>
public sealed record ReportEntry
{
    /// <summary>Skutečný čas nahlášení (UTC).</summary>
    public DateTime Time { get; init; }

    /// <summary>Herní čas (minuty od začátku světa).</summary>
    public double GameMinutes { get; init; }

    public string Reporter { get; init; } = string.Empty;

    public int ReporterId { get; init; }

    public double ReporterX { get; init; }

    public double ReporterY { get; init; }

    /// <summary>Player, Sign nebo General.</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>Behavior, Cheating, OffensiveSign, Bug nebo Other.</summary>
    public string Reason { get; init; } = string.Empty;

    public string? Player { get; init; }

    public int? PlayerId { get; init; }

    public double? PlayerX { get; init; }

    public double? PlayerY { get; init; }

    public bool? PlayerOnline { get; init; }

    public int? SignId { get; init; }

    public double? SignX { get; init; }

    public double? SignY { get; init; }

    /// <summary>Kdo značku nakreslil (přezdívka), u starých rytin prázdné.</summary>
    public string? SignAuthor { get; init; }

    /// <summary>Kresba značky: 12 řádků po 12 znacích, # = bod.</summary>
    public string[]? SignRows { get; init; }
}

/// <summary>
/// Nahlášení od hráčů (návrh 14.3: tlačítko Nahlásit, správce rozhoduje). Každé nahlášení je jeden řádek JSON
/// v souboru (reportsFile v server.json). Správce soubor čte a vyloučené hráče dopíše do bannedNicknames.
/// </summary>
public sealed class ReportLog
{
    private static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

    public ReportLog(string path)
    {
        Path = path;
    }

    public string Path { get; }

    /// <summary>Kolik nahlášení server od startu zapsal.</summary>
    public int Count { get; private set; }

    public void Write(ReportEntry entry)
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(Path, JsonConvert.SerializeObject(entry, Settings) + Environment.NewLine);
        Count++;
    }
}

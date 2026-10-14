using Newtonsoft.Json;
using Oohra.Core.Characters;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server;

/// <summary>Statistiky jednoho hráče (podle přezdívky) pro ladění hry.</summary>
public sealed class PlayerStats
{
    /// <summary>Kdy přišel poprvé a naposledy (UTC).</summary>
    public DateTime FirstSeen { get; set; }

    public DateTime LastSeen { get; set; }

    /// <summary>Kolikrát se přihlásil.</summary>
    public int Sessions { get; set; }

    /// <summary>Kolik skutečných minut strávil ve hře.</summary>
    public double MinutesOnline { get; set; }

    public int Deaths { get; set; }

    /// <summary>Nejdelší život (herní dny).</summary>
    public double LongestLifeDays { get; set; }

    /// <summary>Na co umíral (DeathCause → počet).</summary>
    public SortedDictionary<string, int> DeathCauses { get; set; } = [];

    /// <summary>Co hráč chtěl udělat (typ zprávy → počet), třeba Eat, Say, Place.</summary>
    public SortedDictionary<string, int> Intents { get; set; } = [];

    /// <summary>Jak to dopadlo (druh a výsledek hlášky → počet), třeba Build.Built, Hunt.Killed, Body.Vomited.</summary>
    public SortedDictionary<string, int> Results { get; set; } = [];
}

/// <summary>
/// Statistiky hraní pro ladění (návrh 16.4, testeři o nich vědí): kolik kdo hraje, na co umírá, co dělá
/// a jak to dopadá. Žádné zprávy ani polohy. Server je drží v paměti a průběžně ukládá do souboru (statsFile
/// v server.json), po restartu je načte a pokračuje.
/// </summary>
public sealed class PlayStats
{
    private static readonly JsonSerializerSettings Settings = new() { Formatting = Formatting.Indented };

    private readonly SortedDictionary<string, PlayerStats> _players = new(StringComparer.OrdinalIgnoreCase);

    public PlayStats(string path)
    {
        Path = path;
        if (Enabled && File.Exists(path))
        {
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, PlayerStats>>(File.ReadAllText(path));
            foreach (var (nickname, stats) in loaded ?? [])
            {
                _players[nickname] = stats;
            }
        }
    }

    public string Path { get; }

    /// <summary>Prázdná cesta = statistiky se jen počítají, neukládají (testy).</summary>
    public bool Enabled => !string.IsNullOrEmpty(Path);

    public PlayerStats? Of(string nickname)
    {
        return _players.GetValueOrDefault(nickname);
    }

    /// <summary>Hráč se přihlásil.</summary>
    public void Joined(string nickname, DateTime now)
    {
        var stats = GetOrAdd(nickname, now);
        stats.Sessions++;
        stats.LastSeen = now;
    }

    /// <summary>Hráč je ve hře další kus času (volá se z ticku).</summary>
    public void Online(string nickname, double realSeconds, DateTime now)
    {
        var stats = GetOrAdd(nickname, now);
        stats.MinutesOnline += realSeconds / 60;
        stats.LastSeen = now;
    }

    public void Intent(string nickname, MessageType type)
    {
        Add(GetOrAdd(nickname, DateTime.UtcNow).Intents, type.ToString());
    }

    public void Result(string nickname, GameEvent gameEvent)
    {
        Add(GetOrAdd(nickname, DateTime.UtcNow).Results, gameEvent.Kind + "." + CodeName(gameEvent.Kind, gameEvent.Code));
    }

    /// <summary>Postava zemřela: příčina a délka života (herní minuty).</summary>
    public void Died(string nickname, DeathCause? cause, double lifeMinutes)
    {
        var stats = GetOrAdd(nickname, DateTime.UtcNow);
        stats.Deaths++;
        Add(stats.DeathCauses, cause?.ToString() ?? "Unknown");
        stats.LongestLifeDays = Math.Max(stats.LongestLifeDays, Math.Round(lifeMinutes / 1440, 2));
    }

    public void Save()
    {
        if (!Enabled)
        {
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Nejdřív do dočasného souboru, pak přepsat: když server spadne uprostřed zápisu, starý soubor zůstane celý.
        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(_players, Settings));
        File.Move(temporary, Path, overwrite: true);
    }

    /// <summary>Čitelný název výsledku hlášky podle jejího druhu (Built, Killed...), jinak číslo.</summary>
    public static string CodeName(EventKind kind, byte code)
    {
        return kind switch
        {
            EventKind.Body => ((BodyEvent)code).ToString(),
            EventKind.Harvest => ((HarvestResult)code).ToString(),
            EventKind.Drink => ((DrinkResult)code).ToString(),
            EventKind.Material => ((MaterialResult)code).ToString(),
            EventKind.Fire => ((FireResult)code).ToString(),
            EventKind.Build => ((BuildResult)code).ToString(),
            EventKind.Hunt => ((HuntResult)code).ToString(),
            EventKind.Sign => ((SignResult)code).ToString(),
            EventKind.Tribe => ((TribeResult)code).ToString(),
            EventKind.Corpse => ((CorpseResult)code).ToString(),
            EventKind.Report => ((ReportResult)code).ToString(),
            _ => code.ToString(),
        };
    }

    private PlayerStats GetOrAdd(string nickname, DateTime now)
    {
        if (!_players.TryGetValue(nickname, out var stats))
        {
            stats = new PlayerStats { FirstSeen = now, LastSeen = now };
            _players[nickname] = stats;
        }

        return stats;
    }

    private static void Add(SortedDictionary<string, int> counts, string key)
    {
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }
}

using Newtonsoft.Json;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.World;

namespace Oohra.Server;

/// <summary>Uložený hráč: postava, batoh, poloha a vše, co patří hráči (podpis, otisk klíče).</summary>
public sealed class PlayerSave
{
    public int Id { get; init; }

    public string Nickname { get; init; } = string.Empty;

    public string? KeyHash { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Facing { get; init; }

    public int Lives { get; init; }

    public Signature? Signature { get; init; }

    public bool BodyLeft { get; init; }

    public double BornAt { get; init; }

    public CharacterSave Character { get; init; } = new();

    public int InventoryCapacity { get; init; }

    public List<ItemStack> Inventory { get; init; } = [];

    /// <summary>Batoh na zádech (null = jen ruce).</summary>
    public Item? Bag { get; init; }
}

/// <summary>
/// Celý uložený svět (soubor world.json vedle server.json). Seed a velikost musí sedět s nastavením serveru,
/// ostrov se z nich vytvoří znovu a změny se na něj vrátí.
/// </summary>
public sealed class ServerSave
{
    /// <summary>Verze formátu souboru (kdyby se v budoucnu měnil).</summary>
    public int Format { get; init; } = 1;

    public int Seed { get; init; }

    public int WorldSize { get; init; }

    /// <summary>Kdy svět vznikl (skutečný čas, UTC). Podle toho se počítají generace světa.</summary>
    public DateTime CreatedUtc { get; init; }

    public DateTime SavedUtc { get; init; }

    public double ClockMinutes { get; init; }

    public double CalendarMonths { get; init; }

    public int NextPlayerId { get; init; }

    public int RngState { get; init; }

    public List<PlayerSave> Players { get; init; } = [];

    public IslandSave Island { get; init; } = new();

    public TribesSave Tribes { get; init; } = new();

    /// <summary>Deníky (stránky podle čísla deníku).</summary>
    public List<JournalSave> Journals { get; init; } = [];

    public int NextJournalId { get; init; } = 1;

    /// <summary>Hráči vyloučení příkazem vyloucit (k bannedNicknames ze server.json).</summary>
    public List<string> Banned { get; init; } = [];
}

/// <summary>
/// Zápis a čtení uloženého světa. Píše se nejdřív do dočasného souboru a pak se přepíše, takže když server
/// spadne uprostřed zápisu, zůstane celý starý soubor. Jednou za čas se udělá záloha do složky zalohy.
/// </summary>
public sealed class WorldStore
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        // Časy „nikdy“ jsou v kódu -nekonečno, JSON je neumí jinak než jako text.
        FloatFormatHandling = FloatFormatHandling.String,
    };

    private DateTime _lastBackup = DateTime.MinValue;

    public WorldStore(string path, double backupEveryMinutes, int backupsKept)
    {
        Path = path;
        BackupEveryMinutes = backupEveryMinutes;
        BackupsKept = backupsKept;
    }

    public string Path { get; }

    /// <summary>Prázdná cesta = svět se neukládá (testy).</summary>
    public bool Enabled => !string.IsNullOrEmpty(Path);

    public double BackupEveryMinutes { get; }

    public int BackupsKept { get; }

    /// <summary>Složka se zálohami vedle souboru se světem.</summary>
    public string BackupDirectory => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)) ?? ".", "zalohy");

    public ServerSave? Load()
    {
        if (!Enabled || !File.Exists(Path))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<ServerSave>(File.ReadAllText(Path), Settings);
    }

    public void Save(ServerSave save, DateTime now)
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

        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(save, Settings));
        File.Move(temporary, Path, overwrite: true);
        if ((now - _lastBackup).TotalMinutes >= BackupEveryMinutes)
        {
            Backup(now);
        }
    }

    /// <summary>Kopie světa do složky zalohy (svet-RRRRMMDD-HHMM.json). Nejstarší zálohy nad BackupsKept se smažou.</summary>
    private void Backup(DateTime now)
    {
        _lastBackup = now;
        Directory.CreateDirectory(BackupDirectory);
        File.Copy(Path, System.IO.Path.Combine(BackupDirectory, $"svet-{now:yyyyMMdd-HHmm}.json"), overwrite: true);
        var old = Directory.GetFiles(BackupDirectory, "svet-*.json").OrderByDescending(f => f, StringComparer.Ordinal).Skip(BackupsKept);
        foreach (var file in old)
        {
            File.Delete(file);
        }
    }
}

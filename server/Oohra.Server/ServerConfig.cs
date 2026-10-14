using Newtonsoft.Json;
using Oohra.Protocol;

namespace Oohra.Server;

/// <summary>Nastavení serveru ze souboru server.json (vedle programu).</summary>
public sealed class ServerConfig
{
    public int Port { get; set; } = ProtocolInfo.DefaultPort;

    public int Seed { get; set; } = 1;

    /// <summary>Velikost ostrova. 0 = podle settings.json v Oohra.Core.</summary>
    public int WorldSize { get; set; }

    public List<string> InviteCodes { get; set; } = ["OOHRA-TEST"];

    public int MaxPlayers { get; set; } = 20;

    /// <summary>Zrychlení herního času (jen pro zkoušení, ve hře 1).</summary>
    public double TimeSpeed { get; set; } = 1;

    /// <summary>Snímek s polohami hráčů každých N ticků (při 30 tickách za sekundu 2 = 15× za sekundu).</summary>
    public int SnapshotEveryTicks { get; set; } = 2;

    /// <summary>Stav těla hráči každých N ticků.</summary>
    public int SelfStateEveryTicks { get; set; } = 6;

    /// <summary>Kontrola plodů a vykopaných rostlin každých N ticků.</summary>
    public int PlantStatesEveryTicks { get; set; } = 30;

    /// <summary>Jak často vypsat výkon serveru do konzole (v sekundách, jen když někdo hraje). 0 = nevypisovat.</summary>
    public double StatsEverySeconds { get; set; } = 10;

    /// <summary>V kolik hodin svět začíná (jen pro zkoušení, třeba 21 = hned noc). Ve hře 7.</summary>
    public double StartHour { get; set; } = 7;

    /// <summary>Kolik klacků dostane nová postava do batohu (jen pro zkoušení ohně, ve hře 0).</summary>
    public int StartSticks { get; set; }

    /// <summary>Kolik kamenů dostane nová postava do batohu (jen pro zkoušení ohně, ve hře 0).</summary>
    public int StartStones { get; set; }

    /// <summary>Zvířata na ostrově. Testy serveru je vypínají, ať je náhodný divočák nerozhodí.</summary>
    public bool Animals { get; set; } = true;

    /// <summary>Do jaké vzdálenosti (metry) hráč dostává polohy zvířat.</summary>
    public double AnimalViewDistance { get; set; } = 40;

    /// <summary>Kam se zapisují nahlášení (tlačítko Nahlásit), jeden záznam JSON na řádek. Čte je správce.</summary>
    public string ReportsFile { get; set; } = "reports.jsonl";

    /// <summary>Nejkratší doba mezi dvěma nahlášeními jednoho hráče (sekundy).</summary>
    public double ReportCooldownSeconds { get; set; } = 30;

    /// <summary>Vyloučení hráči (přezdívky). Správce je sem dopíše po nahlášení, server je nepustí do hry.</summary>
    public List<string> BannedNicknames { get; set; } = [];

    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ServerConfig();
        }

        return JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText(path)) ?? new ServerConfig();
    }
}

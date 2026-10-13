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

    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ServerConfig();
        }

        return JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText(path)) ?? new ServerConfig();
    }
}

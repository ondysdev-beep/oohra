using System.Text;
using Oohra.Protocol;

namespace Oohra.Server;

/// <summary>
/// Příkazy správce, které se píšou do okna serveru (Program je čte z konzole). Vrací text odpovědi.
/// Vyloučení (vyloucit) platí hned a uloží se se světem, takže kvůli němu není potřeba restart.
/// </summary>
public sealed partial class GameServer
{
    // Vyloučení příkazem za běhu (k bannedNicknames ze server.json). Ukládají se se světem.
    private readonly HashSet<string> _banned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Server se má vypnout (příkaz konec). Program pak skončí smyčku a uloží svět.</summary>
    public bool StopRequested { get; private set; }

    public const string CommandHelp =
        "Příkazy: hraci | ulozit | oznamit <text> | vyhodit <přezdívka> | vyloucit <přezdívka> | povolit <přezdívka> "
        + "| odemknout <přezdívka> | konec";

    /// <summary>Provede příkaz správce a vrátí odpověď.</summary>
    public string RunCommand(string line)
    {
        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return parts[0].ToLowerInvariant() switch
        {
            "pomoc" or "help" or "?" => CommandHelp,
            "hraci" or "players" => PlayerList(),
            "ulozit" or "save" => SaveCommand(),
            "oznamit" or "say" => Announce(argument),
            "vyhodit" or "kick" => Kick(argument),
            "vyloucit" or "ban" => Ban(argument),
            "povolit" or "unban" => Unban(argument),
            "odemknout" or "unlock" => Unlock(argument),
            "konec" or "quit" or "exit" => Stop(),
            _ => "Neznámý příkaz. " + CommandHelp,
        };
    }

    /// <summary>Je přezdívka vyloučená (server.json nebo příkaz vyloucit)?</summary>
    private bool IsBanned(string nickname)
    {
        return _banned.Contains(nickname) || _config.BannedNicknames.Contains(nickname, StringComparer.OrdinalIgnoreCase);
    }

    private string PlayerList()
    {
        var text = new StringBuilder($"Ve hře {_byPeer.Count}, postav celkem {_byNickname.Count}.");
        foreach (var player in InWorld.OrderBy(p => p.Nickname, StringComparer.OrdinalIgnoreCase))
        {
            var state = player.Peer is null ? "odpojený, postava ještě ve světě" : player.Character.IsDead ? "mrtvý" : "ve hře";
            text.Append($"\n  {player.Nickname} (č. {player.Id}, generace {player.Character.Traits.Generation}, {state}, X {player.X:0} Y {player.Y:0})");
        }

        return text.ToString();
    }

    private string SaveCommand()
    {
        if (!_worldStore.Enabled)
        {
            return "Svět se neukládá (worldFile v server.json je prázdné).";
        }

        SaveWorld();
        return $"Svět uložen do {_worldStore.Path}.";
    }

    private string Announce(string text)
    {
        if (text.Length == 0)
        {
            return "Napiš i text oznámení: oznamit Za 5 minut restartuji server.";
        }

        Broadcast(w => Messages.WriteAnnouncement(w, text));
        return $"Oznámeno hráčům ({_byPeer.Count}).";
    }

    private string Kick(string nickname)
    {
        if (FindPlayer(nickname) is not { Peer: { } peer } player)
        {
            return $"Hráč {nickname} není ve hře.";
        }

        peer.Disconnect();
        return $"{player.Nickname} vyhozen ze hry.";
    }

    private string Ban(string nickname)
    {
        if (nickname.Length == 0)
        {
            return "Napiš přezdívku: vyloucit Petr";
        }

        _banned.Add(nickname);
        var kicked = FindPlayer(nickname) is { Peer: not null } ? " " + Kick(nickname) : string.Empty;
        return $"{nickname} je vyloučený (už se nepřihlásí).{kicked}";
    }

    private string Unban(string nickname)
    {
        var inConfig = _config.BannedNicknames.Contains(nickname, StringComparer.OrdinalIgnoreCase);
        var removed = _banned.Remove(nickname);
        return inConfig
            ? $"{nickname} je vyloučený v server.json (bannedNicknames), odeber ho tam a restartuj server."
            : removed ? $"{nickname} se zase může přihlásit." : $"{nickname} nebyl vyloučený.";
    }

    /// <summary>Hráč přišel o svůj klíč (jiný počítač, smazaná hra): postava bude patřit tomu, kdo se přihlásí první.</summary>
    private string Unlock(string nickname)
    {
        if (FindPlayer(nickname) is not { } player)
        {
            return $"Postava {nickname} neexistuje.";
        }

        player.KeyHash = null;
        return $"Postava {player.Nickname} je odemčená. Dostane ji ten, kdo se s touto přezdívkou přihlásí první.";
    }

    private string Stop()
    {
        StopRequested = true;
        return "Server se vypíná a ukládá svět.";
    }
}

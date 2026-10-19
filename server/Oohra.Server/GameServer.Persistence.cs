using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.Rng;

namespace Oohra.Server;

/// <summary>Ukládání světa: co se ve světě změnilo, přežije restart serveru (soubor worldFile, WorldStore).</summary>
public sealed partial class GameServer
{
    /// <summary>Kdy svět vznikl (skutečný čas, UTC). Po restartu se načte z uloženého světa.</summary>
    public DateTime WorldCreatedUtc { get; internal set; } = DateTime.UtcNow;

    /// <summary>Generace světa podle skutečného času od jeho vzniku (návrh 8.2, generations.json).</summary>
    public int WorldGeneration => GenerationTraits.WorldGeneration((DateTime.UtcNow - WorldCreatedUtc).TotalDays);

    /// <summary>Uloží svět hned (příkaz save, vypnutí serveru). Bez souboru (testy) nedělá nic.</summary>
    public void SaveWorld()
    {
        _worldStore.Save(CaptureWorld(), DateTime.UtcNow);
    }

    /// <summary>Celý stav světa, který se mění během hry.</summary>
    internal ServerSave CaptureWorld()
    {
        return new ServerSave
        {
            Seed = _config.Seed,
            WorldSize = Island.Width,
            CreatedUtc = WorldCreatedUtc,
            SavedUtc = DateTime.UtcNow,
            ClockMinutes = Clock.TotalMinutes,
            CalendarMonths = Clock.CalendarMonths,
            NextPlayerId = _nextPlayerId,
            RngState = _rng.State,
            Players = _byNickname.Values.OrderBy(p => p.Id).Select(SavePlayer).ToList(),
            Island = new IslandSave
            {
                Structures = Island.Structures.Export(),
                Fires = Fires.Export(),
                NextFireId = Fires.NextId,
                Plants = Plants.Export(),
                Rocks = Island.Rocks.Export(),
                Forestry = Island.Forestry.Export(),
                Wasps = Island.Wasps.Export(),
                Racks = Racks.Export(),
                Signs = Island.Signs.Export(),
                Corpses = Island.Corpses.Export(),
                NextCorpseId = Island.Corpses.NextId,
                Ground = Island.Ground.Export(),
                NextGroundId = Island.Ground.NextId,
                BirthGuards = Island.BirthGuards.All.ToList(),
            },
            Tribes = Tribes.Export(),
            Journals = Journals.Export(),
            NextJournalId = Journals.NextId,
            Banned = _banned.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    /// <summary>
    /// Vrátí do čerstvě vytvořeného ostrova uložené změny a hráče. Uložený svět musí mít stejný seed a velikost
    /// jako nastavení, jinak by se změny přenesly na jiný ostrov.
    /// </summary>
    private void RestoreWorld(ServerSave save)
    {
        if (save.Seed != _config.Seed || save.WorldSize != Island.Width)
        {
            throw new InvalidOperationException(
                $"Uložený svět ({_worldStore.Path}) je ze seedu {save.Seed} a velikosti {save.WorldSize}, server má seed {_config.Seed} "
                + $"a velikost {Island.Width}. Vrať nastavení v server.json, nebo uložený svět přesuň jinam (pak vznikne nový svět).");
        }

        WorldCreatedUtc = save.CreatedUtc;
        Clock.Restore(save.ClockMinutes, save.CalendarMonths);
        _nextPlayerId = save.NextPlayerId;
        _rng = new Mulberry32(save.RngState);
        Island.Structures.Restore(save.Island.Structures);
        Fires.Restore(save.Island.Fires, save.Island.NextFireId);
        Plants.Restore(save.Island.Plants);
        Island.Rocks.Restore(save.Island.Rocks);
        Island.Wasps.Restore(save.Island.Wasps);
        Racks.Restore(save.Island.Racks);
        Island.Forestry.Restore(Island, save.Island.Forestry);
        Island.Signs.Restore(save.Island.Signs);
        Island.Corpses.Restore(save.Island.Corpses, save.Island.NextCorpseId);
        Island.Ground.Restore(save.Island.Ground, save.Island.NextGroundId);
        Island.BirthGuards.Restore(save.Island.BirthGuards);
        Tribes.Restore(save.Tribes);
        Journals.Restore(save.Journals, save.NextJournalId);
        _banned.UnionWith(save.Banned);
        foreach (var player in save.Players)
        {
            _byNickname[player.Nickname] = LoadPlayer(player);
        }
    }

    private static PlayerSave SavePlayer(PlayerSession p)
    {
        return new PlayerSave
        {
            Id = p.Id,
            Nickname = p.Nickname,
            KeyHash = p.KeyHash,
            X = p.X,
            Y = p.Y,
            Facing = p.Facing,
            Level = p.Level,
            Lives = p.Lives,
            Signature = p.Signature,
            BodyLeft = p.BodyLeft,
            BornAt = p.BornAt,
            Character = p.Character.ToSave(),
            InventoryCapacity = p.Inventory.Capacity,
            Inventory = p.Inventory.Stacks.ToList(),
            Bag = p.Inventory.Bag,
        };
    }

    private static PlayerSession LoadPlayer(PlayerSave save)
    {
        // Místa se počítají z rukou a batohu (crafting.json), uložený počet míst je jen pro starší světy.
        var character = Character.FromSave(save.Character);
        var inventory = Inventory.For(character.Traits);
        inventory.Restore(save.Inventory);
        inventory.RestoreBag(save.Bag);
        return new PlayerSession(save.Id, save.Nickname, character, inventory, save.X, save.Y)
        {
            KeyHash = save.KeyHash,
            Facing = save.Facing,
            Level = save.Level,
            Lives = save.Lives,
            Signature = save.Signature,
            BodyLeft = save.BodyLeft,
            BornAt = save.BornAt,
        };
    }

    /// <summary>Pravidelné ukládání z ticku (každých worldSaveSeconds).</summary>
    private void SaveWorldWhenDue()
    {
        if (_seconds < _nextWorldSaveAt)
        {
            return;
        }

        _nextWorldSaveAt = _seconds + _config.WorldSaveSeconds;
        SaveWorld();
    }
}

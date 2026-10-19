using System.Runtime.CompilerServices;
using LiteNetLib;
using LiteNetLib.Utils;
using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Social;
using Oohra.Core.Time;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server;

/// <summary>Zprávy klientům: stav těla, batoh, snímky, zvířata a pohledy na svět.</summary>
public sealed partial class GameServer
{
    private void SendVocabulary(PlayerSession player)
    {
        var vocabulary = player.Character.Vocabulary;
        player.SentVocabularyVersion = vocabulary.Version;
        var entries = vocabulary.Entries
            .Select(e => new WordEntryView(e.Word, e.Heard, e.Contexts, e.Meaning, e.OldVoice))
            .ToArray();
        SendTo(player.Peer!, w => Messages.WriteVocabulary(w, new VocabularyView(entries, vocabulary.LastHeard ?? [])));
    }

    private static double Distance(PlayerSession a, PlayerSession b)
    {
        return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    // ---------- zprávy klientům ----------

    private void SendBodyEvents(PlayerSession player)
    {
        foreach (var message in player.Character.TakeMessages())
        {
            SendEvent(player, new GameEvent(EventKind.Body, (byte)message.Event, default, message.Notes?.ToArray() ?? [], (byte)(message.Skill ?? 0), (byte)message.Level));
        }
    }

    private void SendEvent(PlayerSession player, GameEvent gameEvent)
    {
        PlayStats.Result(player.Nickname, gameEvent);
        if (player.Peer is not null)
        {
            SendTo(player.Peer, w => Messages.WriteEvent(w, gameEvent));
        }
    }

    private void SendSelfState(PlayerSession player)
    {
        if (player.Peer is null)
        {
            return;
        }

        var c = player.Character;
        var state = new SelfState(
            (float)c.Satiety,
            (float)c.Hydration,
            (float)c.Energy,
            (float)c.Warmth,
            (float)c.Health,
            c.Symptoms().ToArray(),
            c.Traits.Generation,
            c.IsDead,
            c.DeathCause,
            Fires.WarmsAt(Island, player.X, player.Y),
            Shelter.IsSheltered(Island, player.X, player.Y),
            c.Sleeping,
            SkillKinds.Select(s => (byte)c.Skills.Level(s)).ToArray(),
            PendingSkills(c),
            WorldGeneration);
        SendTo(player.Peer, w => Messages.WriteSelfState(w, state));
    }

    private void SendInventory(PlayerSession player)
    {
        if (player.Peer is null)
        {
            return;
        }

        var stacks = player.Inventory.Stacks.Select(StackViewOf).ToArray();
        var view = new InventoryView(player.Inventory.Capacity, stacks, player.Inventory.Bag?.Kind);
        player.SentSpoilage = SpoilageOf(player);
        SendTo(player.Peer, w => Messages.WriteInventory(w, view));
    }

    /// <summary>Které hromádky jsou zkažené (jako text, třeba „01“). Když se změní, batoh se pošle znovu.</summary>
    private string SpoilageOf(PlayerSession player)
    {
        return string.Concat(player.Inventory.Stacks.Select(s => Spoilage.IsSpoiled(s.Item, Clock.TotalMinutes) ? '1' : '0'));
    }

    private void BroadcastSnapshot()
    {
        var players = InWorld
            .Select(p => new PlayerSnapshot(p.Id, (float)p.X, (float)p.Y, (float)p.Facing, p.Character.IsDead, p.Character.Sleeping, p.Character.TorchLit, p.Inventory.Count(ItemKind.LogPiece) > 0, p.Inventory.Bag?.Kind))
            .ToArray();
        var snapshot = new Snapshot(Clock.TotalMinutes, Clock.CalendarMonths, players, CurrentWeather.Kind, (float)CurrentWeather.Intensity, (float)CurrentWeather.Wind);
        Broadcast(w => Messages.WriteSnapshot(w, snapshot), DeliveryMethod.Sequenced);
    }

    /// <summary>Pošle všem rostliny, které od minula změnily vzhled (plody, vykopání, dorostení).</summary>
    private void BroadcastPlantChanges()
    {
        var changes = new List<PlantStateChange>();
        foreach (var plant in Island.Plants)
        {
            var visual = WorldViews.PlantVisual(Island, Plants, plant, Clock);
            if (visual != _sentVisuals[plant.Id])
            {
                _sentVisuals[plant.Id] = visual;
                changes.Add(new PlantStateChange(plant.Id, visual));
            }
        }

        if (changes.Count > 0)
        {
            Broadcast(w => Messages.WritePlantStates(w, changes));
        }
    }

    private List<PlantStateChange> AllPlantVisuals()
    {
        return Island.Plants
            .Select(p => new PlantStateChange(p.Id, WorldViews.PlantVisual(Island, Plants, p, Clock)))
            .ToList();
    }

    private static readonly SkillKind[] SkillKinds = [SkillKind.Gather, SkillKind.Hunt, SkillKind.Craft, SkillKind.Build];

    /// <summary>Bity dovedností, jejichž nácvik čeká na spánek (bit = číslo dovednosti).</summary>
    private static byte PendingSkills(Character c)
    {
        var bits = 0;
        foreach (var skill in SkillKinds)
        {
            if (c.Skills.Pending(skill) > 0)
            {
                bits |= 1 << (int)skill;
            }
        }

        return (byte)bits;
    }

    /// <summary>Hráči ve hře, jak je vidí zvířata.</summary>
    private List<AnimalTarget> AnimalTargets()
    {
        // Postava ve snu bez slov (nováček) zvířata nezajímá.
        return InWorld.Where(p => _seconds >= p.DreamUntil).Select(p => new AnimalTarget(p.Id, p.X, p.Y, p.Character)).ToList();
    }

    /// <summary>
    /// Každému hráči zvířata v jeho okolí (animalViewDistance), ať se neposílá celý ostrov. Posílá se rychle
    /// (bez opakování), takže zpráva musí být v jednom paketu: když je zvířat víc, dostane jen ta nejbližší.
    /// </summary>
    private void SendAnimals()
    {
        if (!_config.Animals)
        {
            return;
        }

        var range = _config.AnimalViewDistance;
        foreach (var player in _byPeer.Values)
        {
            if (player.Peer is null)
            {
                continue;
            }

            var fits = (player.Peer.GetMaxSinglePacketSize(DeliveryMethod.Sequenced) - Messages.AnimalsHeaderBytes) / Messages.AnimalViewBytes;
            var near = Animals.All
                .Where(a => Math.Abs(a.X - player.X) < range && Math.Abs(a.Y - player.Y) < range)
                .OrderBy(a => (a.X - player.X) * (a.X - player.X) + (a.Y - player.Y) * (a.Y - player.Y))
                .Take(fits)
                .Select(a => new AnimalView(a.Id, a.Kind, (float)a.X, (float)a.Y, (float)a.Dir, a.State))
                .ToArray();
            SendTo(player.Peer, w => Messages.WriteAnimals(w, near), DeliveryMethod.Sequenced);
        }
    }

    private StackView StackViewOf(ItemStack stack)
    {
        var plant = stack.Item.Plant;
        var spoiled = Spoilage.IsSpoiled(stack.Item, Clock.TotalMinutes);
        return plant is null
            ? new StackView(stack.Item.Kind, 0, default, default, stack.Item.Cooking, stack.Count, spoiled, stack.Item.Uses, stack.Item.Tree)
            : new StackView(stack.Item.Kind, plant.SpeciesId, plant.Part, plant.Ripeness, plant.Cooking, stack.Count, spoiled);
    }

    /// <summary>Pošle hráči recepty, které jeho postava umí, když se od minula změnily (naučil se, zemřel).</summary>
    private void SendRecipes(PlayerSession player)
    {
        if (player.Peer is null)
        {
            return;
        }

        var known = string.Join(",", player.Character.Recipes.OrderBy(r => r));
        if (known == player.SentRecipes)
        {
            return;
        }

        player.SentRecipes = known;
        var rules = GameData.Crafting;
        var views = rules.Recipes
            .Where(r => player.Character.Knows(r.Id))
            .Select(r => new RecipeView(rules.IndexOf(r), r.Id, r.Name, r.Inputs.Select(i => (i.Key, i.Value)).ToArray(), r.Tool, r.NearWater, r.NearFire, r.Repairs is not null))
            .ToArray();
        SendTo(player.Peer, w => Messages.WriteRecipes(w, views));
    }

    private TreeStateView[] TreeStateViews()
    {
        return Island.Forestry.TreeStates().Select(t => new TreeStateView(t.TileIndex, t.State, (float)t.Angle, t.Kind, (float)t.Height, (float)t.Growth)).ToArray();
    }

    private TimberView[] TimberViews()
    {
        return Island.Forestry.Timbers.Select(t => new TimberView(t.Id, t.Kind, t.Tree, (float)t.X, (float)t.Y, (float)t.Angle, t.Length)).ToArray();
    }

    private FireView[] FireViews()
    {
        return Fires.All.Select(f => new FireView(f.Id, (float)f.X, (float)f.Y, f.Burning)).ToArray();
    }

    private StructureView[] StructureViews()
    {
        return Island.Structures.All.Select(s => new StructureView(s.Id, s.Kind, (float)s.X, (float)s.Y, (float)s.Angle, (float)s.Length, s.Caught.HasValue ? (int)s.Caught.Value : -1, s.OwnerId, s.DecorationId)).ToArray();
    }

    private RockPileView[] RockPileViews()
    {
        return Island.Rocks.All.Select(p => new RockPileView(p.TileIndex, p.Stones, p.MaxStones)).ToArray();
    }

    /// <summary>
    /// Stopy v okolí hráče: pošlou se, když nějaká přibyla nebo zmizela, nebo když hráč popošel, nejvýš jednou
    /// za sendEverySeconds. Jen nejbližší (maxSent).
    /// </summary>
    private void SendTracks(PlayerSession player)
    {
        var rules = GameData.Seasons.Tracks;
        var key = (Island.Tracks.Version, (int)Math.Floor(player.X / 4), (int)Math.Floor(player.Y / 4));
        if (key == player.SentTracks || _seconds < player.NextTracksAt)
        {
            return;
        }

        var near = Island.Tracks.Near(player.X, player.Y, rules.ViewDistance)
            .OrderBy(t => (t.X - player.X) * (t.X - player.X) + (t.Y - player.Y) * (t.Y - player.Y))
            .Take(rules.MaxSent)
            .Select(t => new TrackView((float)t.X, (float)t.Y, AngleByte(t.Dir), t.Maker, (byte)Math.Round(255 * Math.Max(0, 1 - t.Worn / rules.LifeMinutes))))
            .ToList();

        // Prázdný seznam se pošle jen jednou (ať klient smaže staré stopy), pak už ne.
        if (near.Count == 0 && player.SentTracks.Version >= 0 && player.SentTracksEmpty)
        {
            player.SentTracks = key;
            return;
        }

        player.SentTracks = key;
        player.SentTracksEmpty = near.Count == 0;
        player.NextTracksAt = _seconds + rules.SendEverySeconds;
        SendTo(player.Peer!, w => Messages.WriteTracks(w, near));
    }

    private static byte AngleByte(double radians)
    {
        var turn = radians / (2 * Math.PI) % 1;
        return (byte)((int)Math.Round((turn < 0 ? turn + 1 : turn) * 256) % 256);
    }

    /// <summary>Vchody, které hráčova postava zná. Pošle se, když postava objeví nový nebo když je to nová postava.</summary>
    private void SendCaveEntrances(PlayerSession player)
    {
        var c = player.Character;
        var key = ((object?)c, Island.CaveEntrances.Count(e => e.Hidden && c.KnowsCave(e.Id)));
        if (key == player.SentCaves)
        {
            return;
        }

        player.SentCaves = key;
        var views = Caves.Visible(Island, c)
            .Select(e => new CaveEntranceView(e.Id, (float)e.X, (float)e.Y, (float)e.Facing, (float)e.InsideX, (float)e.InsideY, e.Hidden))
            .ToList();
        SendTo(player.Peer!, w => Messages.WriteCaveEntrances(w, views));
    }

    private RackView[] RackViews()
    {
        return Racks.All.Select(r => new RackView(r.Key.Holder, r.Key.Id, r.Pieces
            .Select(p => new RackPieceView(p.Item.Kind, p.Item.Cooking, (byte)Math.Round(255 * MeatRacks.Fraction(r.Key.Holder, p))))
            .ToArray())).ToArray();
    }

    private WaspNestView[] WaspNestViews()
    {
        return Island.Wasps.Active(Clock.TotalMinutes).Select(n => new WaspNestView(n.TileIndex, (float)n.X, (float)n.Y, Island.Wasps.IsAngry(n))).ToArray();
    }

    private static DecorationTypeView[] DecorationTypeViews()
    {
        return GameData.Decorations.Select(d => new DecorationTypeView(d.Id, d.Name, d.Sticks, d.Stones, (float)d.Radius, d.Blocks)).ToArray();
    }

    private SignView[] SignViews()
    {
        return Island.Signs.All.Select(s => new SignView(
            s.Id, (float)s.X, (float)s.Y, s.Bits, s.AuthorId, s.CreatedAt, Signs.WeathersAt(s) ?? -1, s.Carved, s.Ancient,
            s.Record?.SpeciesId ?? -1, s.Record?.Name ?? [], s.Record?.Tags.ToArray() ?? [], s.Signature?.Name ?? [], s.Signature?.Mark ?? [], s.ByAncient)).ToArray();
    }

    private static PlayerInfo InfoOf(PlayerSession player)
    {
        return new PlayerInfo(player.Id, player.Nickname, player.Character.Traits.Generation);
    }

    /// <summary>
    /// Pošle zprávu. Rychlá zpráva (Sequenced, Unreliable) se musí vejít do jednoho paketu, jinak LiteNetLib
    /// vyhodí chybu: větší se proto pošle spolehlivě (ReliableOrdered), ta se umí rozdělit.
    /// </summary>
    private void SendTo(NetPeer peer, Action<NetDataWriter> write, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        _writer.Reset();
        write(_writer);
        if (method is DeliveryMethod.Sequenced or DeliveryMethod.Unreliable && _writer.Length > peer.GetMaxSinglePacketSize(method))
        {
            method = DeliveryMethod.ReliableOrdered;
        }

        peer.Send(_writer, method);
    }

    private void Broadcast(Action<NetDataWriter> write, DeliveryMethod method = DeliveryMethod.ReliableOrdered, NetPeer? except = null)
    {
        foreach (var player in _byPeer.Values)
        {
            if (player.Peer is not null && player.Peer != except)
            {
                SendTo(player.Peer, write, method);
            }
        }
    }

    private void Broadcast(Action<NetDataWriter> write, NetPeer except)
    {
        Broadcast(write, DeliveryMethod.ReliableOrdered, except);
    }
}

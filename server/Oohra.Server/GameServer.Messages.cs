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
            GenerationTraits.WorldGeneration(Clock.CalendarMonths));
        SendTo(player.Peer, w => Messages.WriteSelfState(w, state));
    }

    private void SendInventory(PlayerSession player)
    {
        if (player.Peer is null)
        {
            return;
        }

        var stacks = player.Inventory.Stacks.Select(StackViewOf).ToArray();
        var view = new InventoryView(player.Inventory.Capacity, stacks);
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
            .Select(p => new PlayerSnapshot(p.Id, (float)p.X, (float)p.Y, (float)p.Facing, p.Character.IsDead, p.Character.Sleeping))
            .ToArray();
        var snapshot = new Snapshot(Clock.TotalMinutes, Clock.CalendarMonths, players);
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
        return InWorld.Select(p => new AnimalTarget(p.Id, p.X, p.Y, p.Character)).ToList();
    }

    /// <summary>Každému hráči zvířata v jeho okolí (animalViewDistance), ať se neposílá celý ostrov.</summary>
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

            var near = Animals.All
                .Where(a => Math.Abs(a.X - player.X) < range && Math.Abs(a.Y - player.Y) < range)
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
            ? new StackView(stack.Item.Kind, 0, default, default, stack.Item.Cooking, stack.Count, spoiled)
            : new StackView(stack.Item.Kind, plant.SpeciesId, plant.Part, plant.Ripeness, plant.Cooking, stack.Count, spoiled);
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

    private void SendTo(NetPeer peer, Action<NetDataWriter> write, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
    {
        _writer.Reset();
        write(_writer);
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

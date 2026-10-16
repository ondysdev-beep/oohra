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

/// <summary>Cizí kmeny (NPC): krok cizinců, jejich slova a gesta, dary.</summary>
public sealed partial class GameServer
{
    // ---------- cizí kmeny ----------

    /// <summary>Krok cizinců a rozeslání toho, co řekli a ukázali.</summary>
    private void UpdateStrangers(double realSeconds)
    {
        if (!_config.Strangers)
        {
            return;
        }

        var players = InWorld.Where(p => !p.Character.IsDead).Select(p => new Position(p.X, p.Y)).ToList();
        Strangers.Update(Island, Plants, Animals.All, Clock, realSeconds, players);
        foreach (var act in Strangers.TakeActs())
        {
            BroadcastStrangerAct(act);
        }
    }

    /// <summary>
    /// Cizinec řekl slovo a/nebo udělal gesto. Slovo slyší hráči do hearDistance a zapíšou si ho do zápisníku
    /// i se situací (u jídla, u vody...), když cizince vidí zblízka. Význam hra neprozradí.
    /// </summary>
    private void BroadcastStrangerAct(StrangerAct act)
    {
        var stranger = Strangers.Find(act.StrangerId);
        if (stranger is null)
        {
            return;
        }

        var language = GameData.Language;
        var word = act.Word is { } concept ? Strangers.WordOf(stranger.Tribe, concept) : null;
        var contexts = act.Word is { } said ? 1 << (int)Strangers.ContextOf(said) : 0;
        foreach (var listener in _byPeer.Values)
        {
            var distance = Math.Sqrt((listener.X - stranger.X) * (listener.X - stranger.X) + (listener.Y - stranger.Y) * (listener.Y - stranger.Y));
            if (word is not null && distance <= language.HearDistance)
            {
                var speech = new SpeechView(stranger.Id, word, Strangers.ToneOf(act.Word!.Value), Stranger: true);
                SendTo(listener.Peer!, w => Messages.WriteSpeech(w, speech));
                if (!listener.Character.IsDead && !listener.Character.Sleeping)
                {
                    listener.Character.Vocabulary.Hear(word, contexts, seesSpeaker: distance <= language.ContextDistance);
                }
            }

            if (act.Gesture is { } gesture && distance <= language.GestureDistance)
            {
                var view = new GestureView(stranger.Id, gesture, (float)act.PointX, (float)act.PointY, Stranger: true);
                SendTo(listener.Peer!, w => Messages.WriteGestureSeen(w, view));
            }
        }
    }

    /// <summary>Hráč dal cizinci věc: hláška a nový batoh.</summary>
    private void StrangerAction(PlayerSession player, Func<StrangerResult> action)
    {
        if (player.Character.IsDead || !_config.Strangers)
        {
            return;
        }

        StrangerAnswer(player, action());
    }

    /// <summary>Výsledek rozhovoru s cizincem (dar, zahozená věc...). Null = nic, co by stálo za hlášku.</summary>
    private void StrangerAnswer(PlayerSession player, StrangerResult? result)
    {
        if (result is { } r)
        {
            SendEvent(player, new GameEvent(EventKind.Stranger, (byte)r, default, []));
            SendInventory(player);
        }
    }

    /// <summary>Každému hráči cizinci v jeho okolí (stejně daleko jako zvířata).</summary>
    private void SendStrangers()
    {
        if (!_config.Strangers)
        {
            return;
        }

        var range = _config.AnimalViewDistance;
        foreach (var player in _byPeer.Values)
        {
            var near = Strangers.All
                .Where(s => Math.Abs(s.X - player.X) <= range && Math.Abs(s.Y - player.Y) <= range)
                .Select(s => new StrangerView(s.Id, s.Tribe, (float)s.X, (float)s.Y, (float)s.Facing, s.Shown))
                .ToList();
            SendTo(player.Peer!, w => Messages.WriteStrangers(w, near), DeliveryMethod.Sequenced);
        }
    }

    /// <summary>Rostlina nejblíž bodu (do 1 m), nebo null.</summary>
    private PlantInstance? PlantAt(double x, double y)
    {
        PlantInstance? best = null;
        var bestDistance = 1.0;
        foreach (var plant in Island.Plants)
        {
            var d = Math.Sqrt((plant.X - x) * (plant.X - x) + (plant.Y - y) * (plant.Y - y));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = plant;
            }
        }

        return best;
    }
}

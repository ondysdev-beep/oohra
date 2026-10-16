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

/// <summary>Jazyk a gesta: slova, gesta a ukázané věci.</summary>
public sealed partial class GameServer
{
    // ---------- jazyk a gesta ----------

    /// <summary>
    /// Hráč řekl slovo. Uslyší ho hráči do hearDistance (i on sám, kvůli bublině). Kdo mluvčího vidí zblízka,
    /// zapíše si do zápisníku i situaci (u vody, u ohně...). Význam slova hra nikdy neprozradí.
    /// </summary>
    private void Say(PlayerSession speaker, (byte[] Word, Tone Tone) say)
    {
        var rules = GameData.Language;
        if (speaker.Character.IsDead || !Words.IsValid(say.Word) || !Enum.IsDefined(say.Tone)
            || _seconds - speaker.LastSaidAt < rules.SayCooldownSeconds)
        {
            return;
        }

        speaker.LastSaidAt = _seconds;
        var gesture = _seconds - speaker.LastGestureAt <= rules.Context.GestureSeconds ? speaker.LastGesture : null;
        var contexts = SpeechContexts.Of(Island, Fires, Animals, speaker.Character, speaker.X, speaker.Y, Clock.TotalMinutes, gesture);
        var speech = new SpeechView(speaker.Id, say.Word, say.Tone);
        foreach (var listener in _byPeer.Values)
        {
            var distance = Distance(speaker, listener);
            if (distance > rules.HearDistance)
            {
                continue;
            }

            SendTo(listener.Peer!, w => Messages.WriteSpeech(w, speech));
            if (listener != speaker && !listener.Character.IsDead && !listener.Character.Sleeping)
            {
                listener.Character.Vocabulary.Hear(say.Word, contexts, seesSpeaker: distance <= rules.ContextDistance);
            }
        }

        if (_config.Strangers)
        {
            // Cizinec poblíž odpoví (rozumí jen slovům svého kmene a Starému hlasu).
            StrangerAnswer(speaker, Strangers.Hear(Island, speaker.Id, speaker.X, speaker.Y, say.Word, speaker.Inventory, Clock));
        }
    }

    /// <summary>Gesto vidí hráči do gestureDistance. Kývnutí a vrtění hlavou dají souvislost slovům řečeným hned potom.</summary>
    private void MakeGesture(PlayerSession player, (Gesture Gesture, float X, float Y) gesture)
    {
        // Ukázat věc má vlastní zprávu (nese věc), krčení rameny dělají jen cizinci.
        if (player.Character.IsDead || !Enum.IsDefined(gesture.Gesture) || gesture.Gesture is Gesture.ShowItem or Gesture.Puzzled)
        {
            return;
        }

        player.LastGesture = gesture.Gesture;
        player.LastGestureAt = _seconds;
        var view = new GestureView(player.Id, gesture.Gesture, gesture.X, gesture.Y);
        foreach (var viewer in _byPeer.Values)
        {
            if (Distance(player, viewer) <= GameData.Language.GestureDistance)
            {
                SendTo(viewer.Peer!, w => Messages.WriteGestureSeen(w, view));
            }
        }

        if (!_config.Strangers)
        {
            return;
        }

        if (gesture.Gesture == Gesture.Point)
        {
            // Ukázat cizinci rostlinu: cizinec řekne, jestli se jí (prototyp askByPoint).
            var plant = PlantAt(gesture.X, gesture.Y);
            if (plant is { } p)
            {
                Strangers.AskAbout(Island, p, player.X, player.Y, Clock);
            }

            return;
        }

        StrangerAnswer(player, Strangers.SeeGesture(Island, player.Id, player.X, player.Y, gesture.Gesture, player.Inventory, Clock));
    }

    /// <summary>
    /// Hráč ukázal věc z batohu (návrh 9.3, učení v kontextu). Hráči do gestureDistance ji uvidí v bublině.
    /// Slovo řečené hned potom dostane souvislost „u jídla“, když je to jídlo. Cizinec poblíž řekne, co si o tom myslí.
    /// </summary>
    private void ShowItem(PlayerSession player, int stackIndex)
    {
        if (player.Character.IsDead || stackIndex < 0 || stackIndex >= player.Inventory.Stacks.Count)
        {
            return;
        }

        var stack = player.Inventory.Stacks[stackIndex];
        var food = stack.Item.Kind == ItemKind.Plant || stack.Item.IsFlesh;
        player.LastGesture = food ? Gesture.ShowItem : Gesture.Point;
        player.LastGestureAt = _seconds;
        var view = new GestureView(player.Id, Gesture.ShowItem, (float)player.X, (float)player.Y, Item: StackViewOf(stack) with { Count = 1 });
        foreach (var viewer in _byPeer.Values)
        {
            if (Distance(player, viewer) <= GameData.Language.GestureDistance)
            {
                SendTo(viewer.Peer!, w => Messages.WriteGestureSeen(w, view));
            }
        }

        if (_config.Strangers)
        {
            Strangers.SeeItem(Island, stack.Item, player.X, player.Y);
        }
    }
}

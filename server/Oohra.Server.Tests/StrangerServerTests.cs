using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Social;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Cizí kmeny přes síť: hráč je vidí, slyší jejich slova, odpovídají mu a dávají dary.</summary>
public class StrangerServerTests
{
    [Fact]
    public void PlayerSeesStrangersNearCamp()
    {
        using var net = new TestNetwork(configure: c => c.Strangers = true);
        var a = net.Join("Ondra");
        var stranger = PutNextToStranger(net, "Ondra");

        Assert.True(net.Pump(() => a.Strangers.Any(s => s.Id == stranger.Id)));
        Assert.Contains(a.Strangers, s => s.Tribe == stranger.Tribe);
    }

    /// <summary>
    /// Na slovo „voda“ cizinec odpoví slovem „voda“ svého kmene. Hráč ho uslyší jako cizince a zapíše si ho
    /// do zápisníku i se situací „u vody“.
    /// </summary>
    [Fact]
    public void StrangerAnswersAndWordGoesToVocabulary()
    {
        using var net = new TestNetwork(configure: c => c.Strangers = true);
        var a = net.Join("Ondra");
        var stranger = PutNextToStranger(net, "Ondra");
        var water = net.Server.Strangers.WordOf(stranger.Tribe, Concept.Water);

        a.Say(water, Tone.Calm);

        var heard = new List<SpeechView>();
        Assert.True(net.Pump(() =>
        {
            heard.AddRange(a.TakeSpeech());
            return heard.Any(s => s.Stranger && s.PlayerId == stranger.Id);
        }));
        var reply = heard.First(s => s.Stranger);
        Assert.Equal(water, reply.Word);
        Assert.True(net.Pump(() => a.Vocabulary.Entries.Any(e => e.Word.SequenceEqual(water) && e.Contexts[(int)SpeechContext.AtWater] > 0)));
    }

    /// <summary>Na „jídlo“ dá kmen upečenou rostlinu, kámen cizinec nechce.</summary>
    [Fact]
    public void FoodWordGivesGiftAndStoneIsRefused()
    {
        using var net = new TestNetwork(configure: c =>
        {
            c.Strangers = true;
            c.StartStones = 1;
        });
        var a = net.Join("Ondra");
        var stranger = PutNextToStranger(net, "Ondra");
        Assert.True(net.Pump(() => a.Inventory.Stacks.Length == 1));

        a.Say(net.Server.Strangers.WordOf(stranger.Tribe, Concept.Food), Tone.Calm);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Stranger && e.Code == (byte)StrangerResult.Gift)));
        Assert.True(net.Pump(() => a.Inventory.Stacks.Any(s => s.Kind == ItemKind.Plant)));

        var stone = Array.FindIndex(a.Inventory.Stacks, s => s.Kind == ItemKind.Stone);
        PutNextToStranger(net, "Ondra", stranger.Id);
        a.GiveToStranger(stranger.Id, stone);
        Assert.True(net.Pump(() => a.TakeEvents().Any(e => e.Kind == EventKind.Stranger && e.Code == (byte)StrangerResult.Puzzled)));
    }

    /// <summary>
    /// Ukázaná věc (návrh 9.3): druhý hráč ji uvidí v bublině i s tím, co to je, a slovo řečené hned potom
    /// si zapíše „u jídla“, když je to jídlo.
    /// </summary>
    [Fact]
    public void ShowItemTeachesWord()
    {
        using var net = new TestNetwork(configure: c => c.StartSticks = 1);
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var ondra = net.Server.FindPlayer("Ondra")!;
        var petr = net.Server.FindPlayer("Petr")!;
        petr.X = ondra.X + 1;
        petr.Y = ondra.Y;
        ondra.Inventory.TryAdd(Item.RawMeat(0));
        var meat = ondra.Inventory.Stacks.ToList().FindIndex(s => s.Item.IsFlesh);

        a.ShowItem(meat);
        var seen = new List<GestureView>();
        Assert.True(net.Pump(() =>
        {
            seen.AddRange(b.TakeGestures());
            return seen.Any(g => g.Gesture == Gesture.ShowItem);
        }));
        Assert.Equal(ItemKind.Meat, seen.First(g => g.Gesture == Gesture.ShowItem).Item!.Value.Kind);

        var word = new byte[] { 4, 4 };
        a.Say(word);
        Assert.True(net.Pump(() => b.Vocabulary.Entries.Any(e => e.Word.SequenceEqual(word) && e.Contexts[(int)SpeechContext.Eating] > 0)));

        // Ukázat klacek (není jídlo) slovu souvislost nedá a gesto ShowItem bez věci server nepustí.
        a.ShowItem(ondra.Inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == ItemKind.Stick));
        a.MakeGesture(Gesture.ShowItem);
        net.PumpFor(200);
        Assert.All(b.TakeGestures(), g => Assert.True(g.Gesture != Gesture.ShowItem || g.Item.HasValue));
    }

    /// <summary>Postaví hráče těsně vedle cizince (cizinci se hýbou, proto se volá i před každou akcí).</summary>
    private static Stranger PutNextToStranger(TestNetwork net, string nickname, int strangerId = 0)
    {
        var stranger = net.Server.Strangers.Find(strangerId)!;
        var player = net.Server.FindPlayer(nickname)!;
        player.X = stranger.X + 0.5;
        player.Y = stranger.Y;
        return stranger;
    }
}

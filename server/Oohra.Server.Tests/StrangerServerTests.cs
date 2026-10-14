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

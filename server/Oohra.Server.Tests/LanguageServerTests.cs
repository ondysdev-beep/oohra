using Oohra.Core.Characters;
using Oohra.Core.Language;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Server.Tests;

/// <summary>Jazyk ze slabik a gesta přes síť.</summary>
public class LanguageServerTests
{
    [Fact]
    public void AncientKnowsOldVoice()
    {
        using var net = new TestNetwork();
        var client = net.Join("Ondra");

        Assert.True(net.Pump(() => client.Vocabulary.Entries.Length == 4));
        Assert.All(client.Vocabulary.Entries, e => Assert.True(e.OldVoice && e.Meaning is not null));
        Assert.Contains(client.Vocabulary.Entries, e => e.Meaning == Concept.Water);
    }

    /// <summary>Slovo slyší jen hráči poblíž. Kdo slyšel, má ho v zápisníku, i se situací mluvčího.</summary>
    [Fact]
    public void WordIsHeardOnlyNearby()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var c = net.Join("Jana");
        var (x, y) = WaterEdge(net.Server.Island);
        Put(net, "Ondra", x, y);
        Put(net, "Petr", x - 3, y);
        Put(net, "Jana", x - 30, y);

        a.Say([1, 7], Tone.Alarm);

        Assert.True(net.Pump(() => b.TakeSpeech().Any(s => s.PlayerId == a.PlayerId && s.Word.SequenceEqual(new byte[] { 1, 7 }) && s.Tone == Tone.Alarm)));
        Assert.True(net.Pump(() => b.Vocabulary.Entries.Any(e => e.Word.SequenceEqual(new byte[] { 1, 7 }))));
        var entry = b.Vocabulary.Entries.Single(e => e.Word.SequenceEqual(new byte[] { 1, 7 }));
        Assert.Equal(1, entry.Heard);
        Assert.Equal(1, entry.Contexts[(int)SpeechContext.AtWater]);
        Assert.Null(entry.Meaning);
        Assert.Equal([1, 7], b.Vocabulary.LastHeard);

        // Mluvčí vidí svou bublinu, ale do zápisníku si vlastní slovo nepíše. Vzdálená Jana nic neslyší.
        Assert.Contains(a.TakeSpeech(), s => s.PlayerId == a.PlayerId);
        Assert.DoesNotContain(a.Vocabulary.Entries, e => e.Word.SequenceEqual(new byte[] { 1, 7 }));
        Assert.Empty(c.TakeSpeech());
        Assert.DoesNotContain(c.Vocabulary.Entries, e => e.Word.SequenceEqual(new byte[] { 1, 7 }));
    }

    [Fact]
    public void NoddingGivesContextToNextWord()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var spot = net.Server.FindPlayer("Ondra")!;
        Put(net, "Petr", spot.X, spot.Y);

        a.MakeGesture(Gesture.Yes);
        Assert.True(net.Pump(() => b.TakeGestures().Any(g => g.PlayerId == a.PlayerId && g.Gesture == Gesture.Yes)));
        a.Say([3]);

        Assert.True(net.Pump(() => b.Vocabulary.Entries.Any(e => e.Word.SequenceEqual(new byte[] { 3 }))));
        var entry = b.Vocabulary.Entries.Single(e => e.Word.SequenceEqual(new byte[] { 3 }));
        Assert.Equal(1, entry.Contexts[(int)SpeechContext.Nodding]);
    }

    [Fact]
    public void PointingSendsPlace()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var spot = net.Server.FindPlayer("Ondra")!;
        Put(net, "Petr", spot.X + 1, spot.Y);

        a.MakeGesture(Gesture.Point, 10.5f, 20.5f);

        Assert.True(net.Pump(() => b.TakeGestures().Any(g => g.Gesture == Gesture.Point && g.X == 10.5f && g.Y == 20.5f)));
    }

    [Fact]
    public void TooFastWordsAreIgnored()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var spot = net.Server.FindPlayer("Ondra")!;
        Put(net, "Petr", spot.X, spot.Y);

        a.Say([4]);
        a.Say([4]);
        net.PumpFor(300);

        Assert.Equal(1, b.Vocabulary.Entries.Single(e => e.Word.SequenceEqual(new byte[] { 4 })).Heard);
    }

    [Fact]
    public void MeaningIsWrittenToNotebook()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        var b = net.Join("Petr");
        var spot = net.Server.FindPlayer("Ondra")!;
        Put(net, "Petr", spot.X, spot.Y);
        a.Say([5, 6]);
        Assert.True(net.Pump(() => b.Vocabulary.Entries.Any(e => e.Word.SequenceEqual(new byte[] { 5, 6 }))));

        b.SetMeaning([5, 6], Concept.Fire);

        Assert.True(net.Pump(() => b.Vocabulary.Entries.Single(e => e.Word.SequenceEqual(new byte[] { 5, 6 })).Meaning == Concept.Fire));
    }

    /// <summary>Smrtí se zápisník ztratí. Nový (generace 1) nezná ani Starý hlas.</summary>
    [Fact]
    public void DeathLosesNotebook()
    {
        using var net = new TestNetwork();
        var a = net.Join("Ondra");
        Assert.True(net.Pump(() => a.Vocabulary.Entries.Length == 4));
        var player = net.Server.FindPlayer("Ondra")!;
        player.Character.Hydration = 0;
        player.Character.Health = 0.0001;
        Assert.True(net.Pump(() => a.Self!.Dead));

        a.Reborn();

        Assert.True(net.Pump(() => a.Self is { Dead: false } && a.Vocabulary.Entries.Length == 0));
    }

    private static void Put(TestNetwork net, string nickname, double x, double y)
    {
        var player = net.Server.FindPlayer(nickname)!;
        player.X = x;
        player.Y = y;
    }

    private static (double X, double Y) WaterEdge(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (x > 35 && island.InBounds(x + 1, y) && island.Tiles[i] is TileType.Grass or TileType.Sand
                && island.TileAt(x + 1, y) is TileType.Pond or TileType.Sea)
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není voda.");
    }
}

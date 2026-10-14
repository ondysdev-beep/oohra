using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Jazyk ze slabik: Starý hlas, zápisník slov a souvislosti řeči (prototyp makeVocab, npcSay, vocabInfo).</summary>
public class LanguageTests
{
    private static readonly Island _island = IslandGenerator.Build(1, 128, 128);

    /// <summary>Stejný seed dá stejná slova jako prototyp (makeVocab s mulberry32(seed ^ 0x1A9)).</summary>
    [Theory]
    [InlineData(1, "3-9 9-6 2-0 0-5 4-2 6-9 6-8")]
    [InlineData(12345, "3-6 7-3 5-2 2-0 0-5 1-4 6-2")]
    public void CommonWordsMatchPrototype(int seed, string expected)
    {
        var words = CommonWords.For(seed);
        var order = new[] { Concept.Food, Concept.Water, Concept.Danger, Concept.Fire, Concept.Yes, Concept.No, Concept.Poison };

        Assert.Equal(expected, string.Join(" ", order.Select(c => Words.Key(words[c]))));
    }

    [Fact]
    public void AncientsKnowOldVoice()
    {
        var vocabulary = new Vocabulary();
        vocabulary.GrantOldVoice(CommonWords.For(1));

        Assert.Equal(4, vocabulary.Entries.Count);
        var water = vocabulary.Find([9, 6])!;
        Assert.Equal(Concept.Water, water.Meaning);
        Assert.True(water.OldVoice);
        Assert.Equal(0, water.Heard);
    }

    [Fact]
    public void HearingWritesContextOnlyWhenSpeakerIsSeen()
    {
        var vocabulary = new Vocabulary();
        var atWater = SpeechContexts.Bit(SpeechContext.AtWater);

        vocabulary.Hear([1, 2], atWater, seesSpeaker: false);
        var entry = vocabulary.Find([1, 2])!;
        Assert.Equal(1, entry.Heard);
        Assert.All(entry.Contexts, count => Assert.Equal(0, count));
        Assert.Null(vocabulary.LastHeard);
        Assert.Null(Vocabulary.Guess(entry));

        vocabulary.Hear([1, 2], atWater, seesSpeaker: true);
        Assert.Equal(2, entry.Heard);
        Assert.Equal(1, entry.Contexts[(int)SpeechContext.AtWater]);
        Assert.Equal([1, 2], vocabulary.LastHeard);
    }

    [Fact]
    public void GuessFollowsMostFrequentContext()
    {
        var vocabulary = new Vocabulary();
        vocabulary.Hear([4], SpeechContexts.Bit(SpeechContext.AtFire), true);
        vocabulary.Hear([4], SpeechContexts.Bit(SpeechContext.Eating) | SpeechContexts.Bit(SpeechContext.AtFire), true);
        vocabulary.Hear([4], SpeechContexts.Bit(SpeechContext.Eating), true);
        vocabulary.Hear([4], SpeechContexts.Bit(SpeechContext.Eating), true);

        Assert.Equal(Concept.Food, Vocabulary.Guess(vocabulary.Find([4])!));
    }

    [Fact]
    public void MeaningOnlyForKnownWords()
    {
        var vocabulary = new Vocabulary();
        Assert.False(vocabulary.SetMeaning([1, 2, 3], Concept.Fire));

        vocabulary.Hear([1, 2, 3], 0, true);
        Assert.True(vocabulary.SetMeaning([1, 2, 3], Concept.Fire));
        Assert.Equal(Concept.Fire, vocabulary.Find([1, 2, 3])!.Meaning);
        Assert.True(vocabulary.SetMeaning([1, 2, 3], null));
        Assert.Null(vocabulary.Find([1, 2, 3])!.Meaning);
    }

    [Fact]
    public void FrequentWordsComeFirst()
    {
        var vocabulary = new Vocabulary();
        vocabulary.Hear([1], 0, true);
        vocabulary.Hear([2], 0, true);
        vocabulary.Hear([2], 0, true);

        Assert.Equal([2], vocabulary.Entries[0].Word);
    }

    [Theory]
    [InlineData(new byte[0], false)]
    [InlineData(new byte[] { 0 }, true)]
    [InlineData(new byte[] { 9, 9, 9 }, true)]
    [InlineData(new byte[] { 1, 2, 3, 4 }, false)]
    [InlineData(new byte[] { 10 }, false)]
    public void WordHasOneToThreeSyllables(byte[] word, bool valid)
    {
        Assert.Equal(valid, Words.IsValid(word));
    }

    [Fact]
    public void ContextOfSpeaker()
    {
        var clock = TestPlants.Clock();
        var speaker = new Character(1, new Mulberry32(1));
        var (x, y) = OpenSpot();
        var animals = new Animals(1);
        var fires = new Fires();

        Assert.Equal(0, SpeechContexts.Of(_island, fires, animals, speaker, x, y, clock.TotalMinutes, null));

        Eating.EatMeat(speaker, Cooking.Baked, clock);
        animals.Place(AnimalKind.Rabbit, x + 4, y);
        var inventory = new Inventory(10);
        for (var i = 0; i < 3; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        inventory.TryAdd(Item.Stone);
        Assert.Equal(FireResult.Lit, fires.Light(_island, inventory, x, y, x + 1, y));

        var bits = SpeechContexts.Of(_island, fires, animals, speaker, x, y, clock.TotalMinutes, Gesture.Yes);
        Assert.Equal(
            SpeechContexts.Bit(SpeechContext.Eating) | SpeechContexts.Bit(SpeechContext.AnimalNear)
            | SpeechContexts.Bit(SpeechContext.AtFire) | SpeechContexts.Bit(SpeechContext.Nodding),
            bits);

        // Za deset minut už mluvčí „u jídla“ není.
        Assert.Equal(0, SpeechContexts.Of(_island, fires, animals, speaker, x, y, clock.TotalMinutes + 10, null) & SpeechContexts.Bit(SpeechContext.Eating));
    }

    /// <summary>Gesto těsně před slovem dá slovu souvislost (návrh 9.3): jíst nebo ukázané jídlo = u jídla, pít = u vody, pozor = zvíře.</summary>
    [Theory]
    [InlineData(Gesture.Eat, SpeechContext.Eating)]
    [InlineData(Gesture.ShowItem, SpeechContext.Eating)]
    [InlineData(Gesture.Drink, SpeechContext.AtWater)]
    [InlineData(Gesture.Beware, SpeechContext.AnimalNear)]
    [InlineData(Gesture.No, SpeechContext.ShakingHead)]
    public void GestureGivesContext(Gesture gesture, SpeechContext expected)
    {
        var (x, y) = OpenSpot();
        var bits = SpeechContexts.Of(_island, new Fires(), new Animals(1), new Character(1, new Mulberry32(1)), x, y, 0, gesture);

        Assert.Equal(SpeechContexts.Bit(expected), bits);
    }

    [Fact]
    public void ContextAtWater()
    {
        var (x, y) = WaterEdge();
        var bits = SpeechContexts.Of(_island, new Fires(), new Animals(1), new Character(1, new Mulberry32(1)), x, y, 0, null);

        Assert.Equal(SpeechContexts.Bit(SpeechContext.AtWater), bits);
    }

    private static (double X, double Y) WaterEdge()
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width, i / _island.Width);
            if (_island.InBounds(x + 1, y) && _island.Tiles[i] is TileType.Grass or TileType.Sand
                && _island.TileAt(x + 1, y) is TileType.Pond or TileType.Sea)
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není voda.");
    }

    /// <summary>Tráva, kolem které je 6 m bez vody a překážek.</summary>
    private static (double X, double Y) OpenSpot()
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width, i / _island.Width);
            if (_island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-6, 13).All(dy => Enumerable.Range(-6, 13).All(dx =>
                    _island.InBounds(x + dx, y + dy) && !Movement.IsBlocked(_island, x + dx, y + dy)
                    && !Drinking.IsWater(_island.TileAt(x + dx, y + dy)))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

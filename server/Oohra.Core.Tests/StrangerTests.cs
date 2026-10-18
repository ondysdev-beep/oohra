using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Social;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Cizí kmeny (prototyp initNPCs, makeVocab, handleNPCWord, askByPoint a giveTo).</summary>
public class StrangerTests
{
    /// <summary>Jazyky kmenů stejné jako v prototypu (výstup makeVocab pro vA a vB, pořadí pojmů: jídlo, voda, nebezpečí, oheň, ano, ne, jed).</summary>
    [Theory]
    [InlineData(1, "3-7 9-6 2-0 0-2 4-2 6-9 5-9", "5-9 9-6 2-0 8-9 4-2 6-9 3-7")]
    [InlineData(12345, "7-2 7-3 5-2 9-7 0-5 1-4 8-3", "8-3 7-3 5-2 2-0 0-5 1-4 7-2")]
    [InlineData(987654321, "6-5 8-2 1-0 2-3 9-7 4-9 6-9", "6-9 8-2 1-0 4-0 9-7 4-9 6-5")]
    public void VocabulariesMatchPrototype(int seed, string first, string second)
    {
        var vocabularies = CommonWords.ForStrangers(seed);

        Assert.Equal(first, Text(vocabularies[0]));
        Assert.Equal(second, Text(vocabularies[1]));
    }

    /// <summary>Starý hlas (voda, nebezpečí, ano, ne) mají oba kmeny stejný jako Pradávní.</summary>
    [Fact]
    public void TribesShareOldVoice()
    {
        var common = CommonWords.For(1);
        var vocabularies = CommonWords.ForStrangers(1);

        foreach (var concept in GameData.Language.CommonWords.OldVoice)
        {
            Assert.Equal(common[concept], vocabularies[0][concept]);
            Assert.Equal(common[concept], vocabularies[1][concept]);
        }
    }

    [Fact]
    public void ThreeStrangersInEachCamp()
    {
        var island = WorldGenerator.Build(12345, 128, 128);
        var strangers = new Strangers(12345, island);

        Assert.Equal(6, strangers.All.Count);
        // Stojí v kruhu kolem ohně (2,4 m). Když tam je kmen stromu, posune se na nejbližší volné místo.
        foreach (var n in strangers.All)
        {
            var camp = island.Camps[n.Tribe];
            Assert.InRange(Math.Sqrt(Math.Pow(n.X - camp.X - 0.5, 2) + Math.Pow(n.Y - camp.Y - 0.5, 2)), 0, GameData.Strangers.RingRadius + 3);
            Assert.True(Movement.IsWalkable(island, n.X, n.Y, GameData.Movement.Radius));
        }
    }

    /// <summary>Cizinci chodí kolem tábora a nevzdálí se (toulání do wanderRadius, jídlo a voda blízko).</summary>
    [Fact]
    public void StrangersStayNearCamp()
    {
        var island = WorldGenerator.Build(12345, 128, 128);
        var strangers = new Strangers(12345, island);
        var clock = GameClock.FromSettings();
        var plants = new PlantStates();
        var start = strangers.All.Select(n => (n.X, n.Y)).ToList();

        for (var i = 0; i < 30 * 60; i++)
        {
            strangers.Update(island, plants, [], clock, 1.0 / 30, []);
        }

        Assert.Contains(strangers.All, n => (n.X, n.Y) != start[n.Id]);
        foreach (var n in strangers.All)
        {
            Assert.True(Distance(n.X, n.Y, n.HomeX, n.HomeY) < GameData.Strangers.PondSearchRadius + 1, $"Cizinec {n.Id} odešel moc daleko.");
            Assert.True(Movement.CanStand(island, n.X, n.Y, 0.2));
        }
    }

    /// <summary>Na slovo „voda“ (Starý hlas) cizinec po chvíli řekne „voda“ a ukáže k vodě.</summary>
    [Fact]
    public void AnswersWaterWordAfterDelay()
    {
        var (island, strangers, n) = Setup();
        var clock = GameClock.FromSettings();

        var result = strangers.Hear(island, 1, n.X + 1, n.Y, strangers.WordOf(n.Tribe, Concept.Water), new Inventory(8), clock);

        Assert.Null(result);
        Assert.Empty(strangers.TakeActs());
        strangers.Update(island, new PlantStates(), [], clock, 1, []);
        var act = Assert.Single(strangers.TakeActs(), a => a.StrangerId == n.Id && a.Word is not null);
        Assert.Equal(Concept.Water, act.Word);
    }

    /// <summary>Na „jídlo“ dá kmen jednou za den upečenou rostlinu. Podruhé řekne „ne“.</summary>
    [Fact]
    public void FoodWordGivesGiftOncePerDay()
    {
        var (island, strangers, n) = Setup();
        var clock = GameClock.FromSettings();
        var inventory = new Inventory(8);
        var food = strangers.WordOf(n.Tribe, Concept.Food);

        Assert.Equal(StrangerResult.Gift, strangers.Hear(island, 1, n.X, n.Y, food, inventory, clock));
        var gift = Assert.Single(inventory.Stacks);
        Assert.Equal(GameData.Strangers.GiftCount, gift.Count);
        Assert.Equal(Cooking.Baked, gift.Item.Plant!.Cooking);
        Assert.NotNull(strangers.SafePart(island, gift.Item.Plant.SpeciesId, clock.Season));

        Assert.Null(strangers.Hear(island, 1, n.X, n.Y, food, inventory, clock));
        strangers.Update(island, new PlantStates(), [], clock, 1, []);
        Assert.Contains(strangers.TakeActs(), a => a.Word == Concept.No);
    }

    /// <summary>Druhý kmen má prohozené jídlo a jed: slovo, za které první kmen dá jídlo, u druhého znamená jed.</summary>
    [Fact]
    public void SecondTribeMixesFoodAndPoison()
    {
        var (_, strangers, _) = Setup();
        var firstFood = strangers.WordOf(0, Concept.Food);

        Assert.Equal(Concept.Poison, strangers.ConceptOf(1, firstFood));
        Assert.Equal(Concept.Food, strangers.ConceptOf(0, firstFood));
    }

    /// <summary>Slovo, které kmen nezná, a gesto, kterému nerozumí: cizinec krčí rameny.</summary>
    [Fact]
    public void UnknownWordPuzzles()
    {
        var (island, strangers, n) = Setup();
        var clock = GameClock.FromSettings();
        var unknown = Enumerable.Range(0, 10).Select(i => new byte[] { (byte)i, (byte)i }).First(w => strangers.ConceptOf(n.Tribe, w) is null);

        strangers.Hear(island, 1, n.X, n.Y, unknown, new Inventory(8), clock);
        strangers.Update(island, new PlantStates(), [], clock, 1, []);

        Assert.Contains(strangers.TakeActs(), a => a.Gesture == Gesture.Puzzled && a.Word is null);
    }

    /// <summary>Ukázaná rostlina: bezpečnou cizinec pojmenuje „jídlo“ a kývne, jedovatou „jed“ a zavrtí hlavou.</summary>
    [Fact]
    public void AskAboutPlant()
    {
        var (island, strangers, n) = Setup();
        var clock = GameClock.FromSettings();
        var safe = island.Plants.First(p => strangers.SafePart(island, p.SpeciesId, clock.Season) is not null);
        var bad = island.Plants.First(p => strangers.SafePart(island, p.SpeciesId, clock.Season) is null);

        Assert.True(strangers.AskAbout(island, safe, n.X, n.Y, clock));
        Assert.False(strangers.AskAbout(island, bad, n.X, n.Y, clock));
        strangers.Update(island, new PlantStates(), [], clock, 1, []);
        Assert.Contains(strangers.TakeActs(), a => a.Word == Concept.Food && a.Gesture == Gesture.Yes);

        strangers.Update(island, new PlantStates(), [], clock, GameData.Strangers.AskCooldownSeconds, []);
        Assert.True(strangers.AskAbout(island, bad, n.X, n.Y, clock));
        strangers.Update(island, new PlantStates(), [], clock, 1, []);
        Assert.Contains(strangers.TakeActs(), a => a.Word == Concept.Poison && a.Gesture == Gesture.No);
    }

    /// <summary>Ukázaná věc z batohu: maso je jídlo (kývne), kámen nezná (krčí rameny). Mezi odpověďmi je chvíle pauza.</summary>
    [Fact]
    public void SeesShownItem()
    {
        var (island, strangers, n) = Setup();
        var clock = GameClock.FromSettings();

        Assert.True(strangers.SeeItem(island, Item.RawMeat(0), n.X, n.Y));
        Assert.False(strangers.SeeItem(island, Item.Stone, n.X, n.Y));
        strangers.Update(island, new PlantStates(), [], clock, 1, []);
        Assert.Contains(strangers.TakeActs(), a => a.Word == Concept.Food && a.Gesture == Gesture.Yes);

        strangers.Update(island, new PlantStates(), [], clock, GameData.Strangers.AskCooldownSeconds, []);
        Assert.True(strangers.SeeItem(island, Item.Stone, n.X, n.Y));
        strangers.Update(island, new PlantStates(), [], clock, 1, []);
        Assert.Contains(strangers.TakeActs(), a => a.Word is null && a.Gesture == Gesture.Puzzled);
    }

    /// <summary>Darovaná škodlivá rostlina skončí na zemi, kámen cizinec nechce, dobré jídlo přijme a dá něco zpátky.</summary>
    [Fact]
    public void GiveItems()
    {
        var (island, strangers, n) = Setup();
        var clock = GameClock.FromSettings();
        var season = clock.Season;
        var harmful = Enumerable.Range(0, island.SpeciesSecrets.Count)
            .SelectMany(s => new[] { PlantPart.Leaf, PlantPart.Root }.Select(p => new PlantItem(s, p, Ripeness.Ripe, season, TileType.Grass)))
            .First(item => Strangers.Harmful(Oohra.Core.Secret.CompositionCalculator.Compute(item, island.SpeciesSecrets[item.SpeciesId])));
        var inventory = new Inventory(8);
        inventory.TryAdd(Item.Stone);
        inventory.TryAdd(harmful);

        Assert.Equal(StrangerResult.Puzzled, strangers.Give(island, 1, n.Id, n.X, n.Y, inventory, 0, clock));
        Assert.Equal(StrangerResult.Discarded, strangers.Give(island, 1, n.Id, n.X, n.Y, inventory, 1, clock));
        Assert.Single(inventory.Stacks);
        Assert.Equal(StrangerResult.TooFar, strangers.Give(island, 1, n.Id, n.X + 5, n.Y, inventory, 0, clock));

        inventory.TryAdd(Item.RawMeat(0));
        Assert.Equal(StrangerResult.Gift, strangers.Give(island, 1, n.Id, n.X, n.Y, inventory, 1, clock));
        inventory.TryAdd(Item.RawMeat(0));
        Assert.Equal(StrangerResult.Accepted, strangers.Give(island, 1, n.Id, n.X, n.Y, inventory, inventory.Stacks.Count - 1, clock));
    }

    /// <summary>Útočící vlk poblíž: cizinec utíká k táboru a varuje „nebezpečí“, když je u něj hráč.</summary>
    [Fact]
    public void FleesFromWolf()
    {
        var (island, strangers, n) = Setup();
        var clock = GameClock.FromSettings();
        var wolf = new Animal(1, AnimalKind.Wolf, n.X + 3, n.Y, 10, 1) { State = AnimalState.Charge };

        var acts = new List<StrangerAct>();
        var fled = false;
        for (var i = 0; i < 30 * 20 && !acts.Any(a => a.StrangerId == n.Id && a.Word == Concept.Danger); i++)
        {
            // Vlk drží krok s cizincem, aby nebezpečí trvalo, dokud cizinec nepromluví (může mít ještě pauzu mezi slovy).
            wolf.X = n.X + 3;
            wolf.Y = n.Y;
            strangers.Update(island, new PlantStates(), [wolf], clock, 1.0 / 30, [new Position(n.X + 1, n.Y)]);
            fled |= n.State == StrangerState.Flee;
            acts.AddRange(strangers.TakeActs());
        }

        Assert.True(fled);
        Assert.Contains(acts, a => a.StrangerId == n.Id && a.Word == Concept.Danger && a.Gesture == Gesture.Beware);
    }

    private static (Island Island, Strangers Strangers, Stranger First) Setup()
    {
        var island = WorldGenerator.Build(12345, 128, 128);
        var strangers = new Strangers(12345, island);
        return (island, strangers, strangers.All[0]);
    }

    private static string Text(IReadOnlyDictionary<Concept, byte[]> vocabulary)
    {
        return string.Join(" ", GameData.Language.CommonWords.Concepts.Select(c => Words.Key(vocabulary[c])));
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }
}

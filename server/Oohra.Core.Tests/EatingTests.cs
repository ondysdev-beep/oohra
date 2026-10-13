using Oohra.Core.Characters;
using Oohra.Core.Plants;
using static Oohra.Core.Tests.TestPlants;

namespace Oohra.Core.Tests;

/// <summary>Jedení, zpožděná otrava a příznaky (prototyp consume a runEvents, part4.js).</summary>
public class EatingTests
{
    [Fact]
    public void SafeFruitFillsStomachRightAway()
    {
        var c = Person();
        var clock = Clock();

        Eating.Eat(c, Item(PlantPart.Fruit), Species(SafeFruit), clock);

        Assert.Equal(Math.Min(100, 70 + 0.75 * 38), c.Satiety, 9);
        Assert.Empty(c.Scheduled);
    }

    // Brána Fáze A: sníš rostlinu a po čase vidíš příznaky otravy.
    [Fact]
    public void PoisonActsOnlyAfterDelay()
    {
        var c = Person(generation: 1);
        var clock = Clock();

        for (var i = 0; i < 3; i++)
        {
            Eating.Eat(c, Item(PlantPart.Root), Species(PoisonRoot), clock);
        }

        Physiology.Run(c, clock, 59);
        Assert.Equal(0, c.Toxin);
        Assert.Empty(c.Symptoms());

        Physiology.Run(c, clock, 600);
        Assert.Contains(Symptom.HeavyBody, c.Symptoms());
        Assert.Contains(Symptom.Fever, c.Symptoms());
    }

    [Fact]
    public void ToxinDelayIsBetweenOneAndFiveHours()
    {
        var c = Person(generation: 1);
        var clock = Clock();
        var start = clock.TotalMinutes;

        Eating.Eat(c, Item(PlantPart.Root), Species(PoisonRoot), clock);

        var toxin = Assert.Single(c.Scheduled, e => e.Type == EffectType.Toxin);
        Assert.InRange(toxin.At - start, 60, 300);
        Assert.Equal(0.8 * 35 * 1.04, toxin.Strength, 9);
    }

    [Fact]
    public void AncientStomachTakesLessPoison()
    {
        var ancient = Person(generation: 0);
        var later = Person(generation: 1);
        var clock = Clock();

        Eating.Eat(ancient, Item(PlantPart.Root), Species(PoisonRoot), clock);
        Eating.Eat(later, Item(PlantPart.Root), Species(PoisonRoot), clock);

        var a = ancient.Scheduled.Single(e => e.Type == EffectType.Toxin).Strength;
        var b = later.Scheduled.Single(e => e.Type == EffectType.Toxin).Strength;
        Assert.True(a < b);
    }

    // Upečení ubere toxin na 4 % (cook v archetypu 1). Zbytek je neškodný.
    [Fact]
    public void BakingMakesPoisonRootAlmostHarmless()
    {
        var raw = Person();
        var baked = Person();
        var clock = Clock();

        Eating.Eat(raw, Item(PlantPart.Root), Species(PoisonRoot), clock);
        Eating.Eat(baked, Item(PlantPart.Root) with { Cooking = Cooking.Baked }, Species(PoisonRoot), clock);

        var rawToxin = raw.Scheduled.Single(e => e.Type == EffectType.Toxin).Strength;
        var bakedToxin = baked.Scheduled.Single(e => e.Type == EffectType.Toxin).Strength;
        Assert.Equal(rawToxin * 0.04, bakedToxin, 9);
        Assert.True(bakedToxin < 1);
    }

    // Archetyp 2: list s dráždivostí 0,9 vyvolá zvracení vždy (šance 0,9 × 1,4 > 1), nejpozději za 4 hodiny.
    [Fact]
    public void BurningLeafCausesVomiting()
    {
        var c = Person();
        var clock = Clock();

        Eating.Eat(c, Item(PlantPart.Leaf), Species(BurningLeaf), clock);
        c.TakeMessages();
        var hydrationBefore = c.Hydration;
        Physiology.Run(c, clock, 241);

        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.Vomited);
        Assert.True(c.Hydration < hydrationBefore - 10);
    }

    [Fact]
    public void NeurotoxinCausesTremor()
    {
        var c = Person(generation: 1);
        var clock = Clock();

        Eating.Eat(c, Item(PlantPart.Fruit), Species(NeurotoxinFruit), clock);
        Physiology.Run(c, clock, 361);

        var events = c.TakeMessages().Select(m => m.Event).ToList();
        Assert.Contains(BodyEvent.Tremor, events);
        Assert.Contains(BodyEvent.Fainted, events);
    }

    [Fact]
    public void StimulantGivesEnergyThenCrash()
    {
        var c = Person();
        var clock = Clock();
        c.Energy = 50;

        Eating.Eat(c, Item(PlantPart.Leaf), Species(StimulantLeaf), clock);

        Assert.Equal(50 + 22 * 0.8, c.Energy, 9);
        Assert.Contains(Symptom.Agitated, c.Symptoms());
        Physiology.Run(c, clock, 171);
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.Crash);
    }

    // Archetyp 2: kořen má léčivo 0,8. Sníží horečku o 35 × 0,8.
    [Fact]
    public void MedicineLowersFever()
    {
        var c = Person();
        var clock = Clock();
        c.Fever = 50;

        Eating.Eat(c, Item(PlantPart.Root), Species(BurningLeaf), clock);

        Assert.Equal(50 - 35 * 0.8, c.Fever, 9);
    }

    // Ochrana první otravy: zdravá postava přežije i velkou otravu (zdraví neklesne pod 10).
    [Fact]
    public void FirstPoisoningDoesNotKillHealthyCharacter()
    {
        var c = Person(generation: 1);
        var clock = Clock();
        for (var i = 0; i < 6; i++)
        {
            Eating.Eat(c, Item(PlantPart.Root), Species(PoisonRoot), clock);
        }

        RunFed(c, clock, 2 * 1440);

        Assert.False(c.IsDead);
        Assert.True(c.Health >= 10);
    }

    // Kdo už je zraněný nebo oslabený, ochranu nemá. Opakované riskování zabíjí.
    [Fact]
    public void PoisoningKillsWeakenedCharacter()
    {
        var c = Person(generation: 1);
        var clock = Clock();
        c.Health = 60;
        for (var i = 0; i < 6; i++)
        {
            Eating.Eat(c, Item(PlantPart.Root), Species(PoisonRoot), clock);
        }

        RunFed(c, clock, 2 * 1440);

        Assert.True(c.IsDead);
        Assert.Contains(c.DeathCause, new DeathCause?[] { DeathCause.Toxin, DeathCause.Fever });
    }

    [Fact]
    public void AncientHasNoBitternessInstinct()
    {
        var c = Person(generation: 0);
        var clock = Clock();
        for (var i = 0; i < 20; i++)
        {
            Eating.Eat(c, Item(PlantPart.Root), Species(PoisonRoot), clock);
        }

        Assert.DoesNotContain(c.TakeMessages(), m => m.Event == BodyEvent.BitternessStopped);
    }

    [Fact]
    public void LaterGenerationsSometimesStopAtBitterness()
    {
        var c = Person(generation: 6);
        var clock = Clock();
        for (var i = 0; i < 20; i++)
        {
            Eating.Eat(c, Item(PlantPart.Root), Species(PoisonRoot), clock);
        }

        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.BitternessStopped);
    }

    [Fact]
    public void TastingGivesHints()
    {
        var c = Person();
        var clock = Clock();

        Eating.Taste(c, Item(PlantPart.Fruit), Species(SafeFruit), clock);
        Eating.Taste(c, Item(PlantPart.Leaf), Species(BurningLeaf), clock);
        for (var i = 0; i < 10; i++)
        {
            Eating.Taste(c, Item(PlantPart.Root), Species(PoisonRoot), clock);
        }

        var tastes = c.TakeMessages().Where(m => m.Event == BodyEvent.Tasted).ToList();
        Assert.Contains(TasteNote.Sweet, tastes[0].Notes!);
        Assert.Contains(TasteNote.Burns, tastes[1].Notes!);
        Assert.Contains(tastes.Skip(2), m => m.Notes!.Contains(TasteNote.VeryBitter));
    }

    [Fact]
    public void TastingIsMuchWeakerThanEating()
    {
        var c = Person(generation: 1);
        var clock = Clock();

        Eating.Taste(c, Item(PlantPart.Root), Species(PoisonRoot), clock);

        var toxin = c.Scheduled.Single(e => e.Type == EffectType.Toxin);
        Assert.Equal(0.8 * 0.1 * 35 * 1.04, toxin.Strength, 9);
    }

    [Fact]
    public void SameSeedGivesSameOutcome()
    {
        var first = Person(generation: 3, seed: 99);
        var second = Person(generation: 3, seed: 99);
        var clock1 = Clock();
        var clock2 = Clock();

        Eating.Eat(first, Item(PlantPart.Leaf), Species(BurningLeaf), clock1);
        Eating.Eat(second, Item(PlantPart.Leaf), Species(BurningLeaf), clock2);

        Assert.Equal(first.Scheduled, second.Scheduled);
    }

    /// <summary>Nechá běžet čas a každou hodinu postavu nakrmí a napojí, aby umírala jen na otravu.</summary>
    private static void RunFed(Character c, Time.GameClock clock, double minutes)
    {
        for (var done = 0.0; done < minutes && !c.IsDead; done += 60)
        {
            c.Satiety = 100;
            c.Hydration = 100;
            Physiology.Run(c, clock, 60);
        }
    }
}

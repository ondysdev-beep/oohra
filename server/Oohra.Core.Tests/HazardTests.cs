using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Nebezpečí v bažině a nemoci s vlastním lékem (návrh 0.8, kap. 5).</summary>
public class HazardTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    [Fact]
    public void DysenteryGivesDiarrheaAndThirstAndHealsByItself()
    {
        var c = Person();
        var clock = Clock(hour: 12, month: 1);
        Diseases.Start(c, DiseaseKind.Dysentery, 50, clock.TotalMinutes);
        c.Hydration = 80;

        Physiology.Run(c, clock, 60);
        Assert.True(c.DiarrheaMinutes > 0);
        Assert.True(c.Hydration < 80);

        // Postava pije a jí každé 4 hodiny (s průjmem je žízeň trojnásobná, kdo nepije, zemře).
        for (var meal = 0; meal < 4 * 6; meal++)
        {
            (c.Hydration, c.Satiety, c.Energy) = (100, 100, 100);
            Physiology.Run(c, clock, 240, stepMinutes: 10);
        }

        Assert.False(c.IsDead);
        Assert.False(c.HasDisease(DiseaseKind.Dysentery), "Úplavice má sama odeznít.");
    }

    /// <summary>Každá nemoc má svůj lék: kořen archetypu 7 léčí úplavici, na zimnici nepomůže.</summary>
    [Fact]
    public void EachDiseaseHasItsOwnCure()
    {
        var c = Person();
        var clock = Clock(hour: 12, month: 1);
        Diseases.Start(c, DiseaseKind.Dysentery, 40, clock.TotalMinutes);
        Diseases.Start(c, DiseaseKind.MarshFever, 40, clock.TotalMinutes);
        var cure = _island.SpeciesSecrets.First(s => s.ArchetypeIndex == 7);
        var root = new PlantItem(cure.Id, PlantPart.Root, Ripeness.Ripe, Season.Spring, TileType.Grass);

        Eating.Eat(c, root, cure, clock);

        Assert.False(c.HasDisease(DiseaseKind.Dysentery), "Správná rostlina úplavici vyléčí.");
        Assert.True(c.HasDisease(DiseaseKind.MarshFever), "Na jinou nemoc nepomůže.");
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.Recovered);
        Assert.NotEmpty(GameData.Archetypes[2].CuresFor(PlantPart.Leaf));
    }

    [Fact]
    public void MarshFeverComesInWaves()
    {
        var c = Person();
        var clock = Clock(hour: 12, month: 1);
        Diseases.Start(c, DiseaseKind.MarshFever, 70, clock.TotalMinutes);
        var rules = GameData.Hazards.MarshFever;

        Physiology.Run(c, clock, 30);
        Assert.True(c.Fever > 50, $"Horečka {c.Fever}");
        Assert.Contains(Symptom.Chills, c.Symptoms());

        // Mimo vlnu horečka klesá.
        Physiology.Run(c, clock, rules.CycleMinutes * (1 - rules.FeverShare) - 40, stepMinutes: 5);
        Assert.True(c.Fever < 50, $"Horečka mimo vlnu {c.Fever}");
    }

    /// <summary>Komáři štípou v létě za soumraku v bažině. U ohně a natřeného bahnem ne, v zimě vůbec.</summary>
    [Fact]
    public void MosquitoesBiteAtDuskInMarshUnlessProtected()
    {
        var (x, y) = Marsh();
        var dusk = Clock(hour: 19, month: 4);
        var bitten = Bitten(Person(), dusk, x, y, new Fires());
        Assert.Contains(bitten.TakeMessages(), m => m.Event == BodyEvent.MosquitoBite);

        var smeared = Person();
        smeared.SmearedUntil = double.PositiveInfinity;
        Assert.DoesNotContain(Bitten(smeared, dusk, x, y, new Fires()).TakeMessages(), m => m.Event == BodyEvent.MosquitoBite);

        var winter = Clock(hour: 19, month: 10);
        Assert.Equal(0, Hazards.MosquitoStrength(_island, x, y, winter));
        Assert.True(Hazards.MosquitoStrength(_island, x, y, dusk) > Hazards.MosquitoStrength(_island, x, y, Clock(hour: 13, month: 4)));
    }

    [Fact]
    public void MosquitoBitesCanBringMarshFever()
    {
        var (x, y) = Marsh();
        var c = Bitten(Person(), Clock(hour: 19, month: 4), x, y, new Fires(), minutes: 3000);
        Assert.Contains(c.Scheduled, e => e.Type == EffectType.MarshFever);
    }

    [Fact]
    public void LeechesStickInBogAndComeOffWithAsh()
    {
        var bog = Enumerable.Range(0, _island.Tiles.Length).First(i => _island.Tiles[i] == TileType.Bog);
        var (x, y) = (bog % _island.Width + 0.5, bog / _island.Width + 0.5);
        var c = Person();
        var clock = Clock(hour: 12, month: 1);
        var rng = new Mulberry32(5);
        for (var i = 0; i < 200; i++)
        {
            Hazards.Update(c, _island, new Fires(), x, y, clock, 1, rng);
        }

        Assert.InRange(c.Leeches, 1, GameData.Hazards.Leeches.Max);
        Assert.Contains(Symptom.Leeches, c.Symptoms());
        Assert.True(c.Health < 100);

        var inventory = new Inventory(10);
        Assert.False(Hazards.RemoveLeeches(c, inventory, nearFire: false, 0));
        inventory.TryAdd(Item.Ash);
        Assert.True(Hazards.RemoveLeeches(c, inventory, nearFire: false, 0));
        Assert.Equal(0, c.Leeches);
        Assert.Equal(0, inventory.Count(ItemKind.Ash));
    }

    [Fact]
    public void SmearWithMudNearWaterOrWithAsh()
    {
        var (x, y) = Marsh();
        var c = Person();
        var inventory = new Inventory(10);

        Assert.True(Hazards.Smear(c, _island, inventory, x, y, ash: false, 0));
        Assert.Equal(GameData.Hazards.SmearMinutes, c.SmearedUntil);
        Assert.False(Hazards.Smear(c, _island, inventory, x, y, ash: true, 0), "Bez popela to nejde.");

        var saved = Character.FromSave(c.ToSave());
        Assert.Equal(c.SmearedUntil, saved.SmearedUntil);
    }

    private static Character Bitten(Character c, GameClock clock, double x, double y, Fires fires, int minutes = 400)
    {
        var rng = new Mulberry32(3);
        for (var i = 0; i < minutes; i++)
        {
            Hazards.Update(c, _island, fires, x, y, clock, 1, rng);
        }

        return c;
    }

    /// <summary>Místo v bažině.</summary>
    private static (double X, double Y) Marsh()
    {
        var tile = Enumerable.Range(0, _island.Tiles.Length).First(i => _island.Regions[i] == RegionKind.Marsh);
        return (tile % _island.Width + 0.5, tile / _island.Width + 0.5);
    }

    private static Character Person() => new(1, new Mulberry32(1));

    /// <summary>Hodiny v daný čas a měsíc kalendáře (léto je měsíc 3 až 5, zima 9 až 11).</summary>
    private static GameClock Clock(double hour, double month)
    {
        var clock = GameClock.FromSettings(hour * 60);
        clock.Restore(hour * 60, month + 0.5);
        return clock;
    }
}

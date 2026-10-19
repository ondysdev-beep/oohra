using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Nebezpečí, druhá vlna (návrh 0.8, kap. 5): klíšťata, hadi a vosí hnízda, každá nemoc s vlastním lékem.</summary>
public class WildHazardTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    /// <summary>V létě v trávě se klíšťata přisají potichu. Najde je až prohlídka, pak už žádné nejsou.</summary>
    [Fact]
    public void TicksAttachSilentlyAndInspectionFindsThem()
    {
        var c = Person();
        var (x, y) = Tile(TileType.Grass);
        var clock = Clock(hour: 12, month: 4);
        var rng = new Mulberry32(5);
        for (var i = 0; i < 600; i++)
        {
            Hazards.Update(c, _island, new Fires(), x, y, clock, 1, rng);
        }

        Assert.NotEmpty(c.Ticks);
        Assert.DoesNotContain(c.TakeMessages(), m => m.Event is BodyEvent.TicksFound or BodyEvent.NoTicks);

        Assert.True(Hazards.Inspect(c, clock.TotalMinutes) > 0);
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.TicksFound);
        Assert.Empty(c.Ticks);
        Assert.Equal(0, Hazards.Inspect(c, clock.TotalMinutes));
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.NoTicks);
    }

    [Fact]
    public void NoTicksInWinterOrOnSand()
    {
        var winter = Person();
        var sand = Person();
        var rng = new Mulberry32(5);
        var (gx, gy) = Tile(TileType.Grass);
        var (sx, sy) = Tile(TileType.Sand);
        for (var i = 0; i < 2000; i++)
        {
            Hazards.Update(winter, _island, new Fires(), gx, gy, Clock(hour: 12, month: 10), 1, rng);
            Hazards.Update(sand, _island, new Fires(), sx, sy, Clock(hour: 12, month: 4), 1, rng);
        }

        Assert.Empty(winter.Ticks);
        Assert.Empty(sand.Ticks);
    }

    /// <summary>Klíště, které zůstane přisáté dlouho, může přenést horečku (začne se zpožděním).</summary>
    [Fact]
    public void TickLeftTooLongCausesTickFever()
    {
        var c = Person();
        var clock = Clock(hour: 12, month: 4);
        for (var i = 0; i < 5; i++)
        {
            c.AddTick(clock.TotalMinutes - GameData.Hazards.Ticks.TransmitMinutes - 10);
        }

        Hazards.Update(c, _island, new Fires(), 0.5, 0.5, clock, 1, new Mulberry32(2));

        Assert.Contains(c.Scheduled, e => e.Type == EffectType.TickFever);
        Assert.Equal(5, c.Ticks.Count);
    }

    /// <summary>Klíšťová horečka: horečka a bolest kloubů. Léčí ji kořen archetypu 4 (silná nemoc chce dvě dávky), ne lék na úplavici.</summary>
    [Fact]
    public void TickFeverHasItsOwnCure()
    {
        var c = Person();
        var clock = Clock(hour: 12, month: 4);
        Diseases.Start(c, DiseaseKind.TickFever, 60, clock.TotalMinutes);
        Physiology.Run(c, clock, 30);
        Assert.True(c.Fever > 20, $"Horečka {c.Fever}");
        Assert.True(c.Limp > 0);
        Assert.Contains(Symptom.AchingJoints, c.Symptoms());

        Eat(c, archetype: 7, PlantPart.Root, clock);
        Assert.True(c.HasDisease(DiseaseKind.TickFever), "Lék na úplavici na klíšťovou horečku nepomůže.");

        Eat(c, archetype: 4, PlantPart.Root, clock);
        Eat(c, archetype: 4, PlantPart.Root, clock);
        Assert.False(c.HasDisease(DiseaseKind.TickFever), "Správný kořen ji vyléčí.");
    }

    /// <summary>Hadí jed: jed v těle a kulhání, odezní sám do dne, nebo hned po správném kořeni.</summary>
    [Fact]
    public void SnakeVenomPoisonsAndLimpsAndHasItsOwnCure()
    {
        var c = Person();
        var clock = Clock(hour: 12, month: 4);
        Diseases.Start(c, DiseaseKind.SnakeVenom, 50, clock.TotalMinutes);
        Physiology.Run(c, clock, 30);
        Assert.True(c.Toxin > 0);
        Assert.True(c.Limp > 0);
        Assert.Contains(Symptom.Swelling, c.Symptoms());

        Eat(c, archetype: 0, PlantPart.Root, clock);
        Eat(c, archetype: 0, PlantPart.Root, clock);
        Assert.False(c.HasDisease(DiseaseKind.SnakeVenom));

        var other = Person();
        Diseases.Start(other, DiseaseKind.SnakeVenom, 50, clock.TotalMinutes);
        for (var hour = 0; hour < 24; hour++)
        {
            (other.Hydration, other.Satiety) = (100, 100);
            Physiology.Run(other, clock, 60, stepMinutes: 10);
        }

        Assert.False(other.HasDisease(DiseaseKind.SnakeVenom), "Jed má sám odeznít.");
    }

    /// <summary>Had v létě ve dne varuje a kdo přijde blízko, toho kousne (jed se zpožděním).</summary>
    [Fact]
    public void SnakeWarnsAndBitesInSummerDay()
    {
        var (px, py) = Tile(TileType.Grass);
        var animals = new Animals(3);
        var snake = animals.Place(AnimalKind.Snake, px + 0.5, py);
        var player = new AnimalTarget(1, px, py, Person());

        Run(animals, Clock(hour: 12, month: 4), player, 0.3);

        Assert.Equal(AnimalState.Alert, snake.State);
        var messages = player.Character.TakeMessages();
        Assert.Contains(messages, m => m.Event == BodyEvent.SnakeWarning);
        Assert.Contains(messages, m => m.Event == BodyEvent.SnakeBite);
        Assert.Contains(player.Character.Scheduled, e => e.Type == EffectType.SnakeVenom);
    }

    [Fact]
    public void SnakeSleepsInWinterAndAtNight()
    {
        var (px, py) = Tile(TileType.Grass);
        foreach (var clock in new[] { Clock(hour: 12, month: 10), Clock(hour: 1, month: 4) })
        {
            var animals = new Animals(3);
            var snake = animals.Place(AnimalKind.Snake, px + 0.5, py);
            var player = new AnimalTarget(1, px, py, Person());

            Run(animals, clock, player, 2);

            Assert.Equal(AnimalState.Asleep, snake.State);
            Assert.Empty(player.Character.TakeMessages());
        }
    }

    [Fact]
    public void SnakeDiesFromOneHitAndLivesOnStonyGround()
    {
        var (px, py) = Tile(TileType.Grass);
        var animals = new Animals(3);
        var snake = animals.Place(AnimalKind.Snake, px + 0.5, py);
        var hunter = new AnimalTarget(1, px, py, Person());

        Assert.Equal(HuntResult.Killed, animals.Attack(snake.Id, hunter, new Inventory(5), 0));
        Assert.True(Animals.TargetCount(_island, AnimalKind.Snake) > 0);

        animals.Populate(_island, []);
        var snakes = animals.All.Where(a => a.Kind == AnimalKind.Snake && !a.IsDead).ToList();
        Assert.NotEmpty(snakes);
        Assert.All(snakes, s => Assert.Contains(_island.TileAt((int)s.X, (int)s.Y), new[] { TileType.Highland, TileType.LayeredRock, TileType.Grass }));
    }

    /// <summary>Vosy se rozzuří na toho, kdo přijde k hnízdu, a štípou. S hořící pochodní ne.</summary>
    [Fact]
    public void WaspsStingNearNestButNotSomeoneWithTorch()
    {
        var wasps = new WaspNests(_island);
        var nest = wasps.Active(0).First();
        var stung = new AnimalTarget(1, nest.X + 1, nest.Y, Person());
        var smoky = new AnimalTarget(2, nest.X - 1, nest.Y, Person());
        smoky.Character.TorchMinutes = 60;

        for (var t = 0; t < 40; t++)
        {
            wasps.Update(0.1, 0, [stung, smoky]);
        }

        Assert.True(wasps.IsAngry(nest));
        Assert.True(stung.Character.Toxin > 0);
        Assert.Contains(stung.Character.TakeMessages(), m => m.Event == BodyEvent.WaspSting);
        Assert.Equal(0, smoky.Character.Toxin);
    }

    [Fact]
    public void TorchSmokesOutNestForAFewDays()
    {
        var wasps = new WaspNests(_island);
        var nest = wasps.Active(0).First();
        var c = Person();

        Assert.Equal(WaspResult.NeedTorch, wasps.SmokeOut(nest.TileIndex, c, nest.X + 1, nest.Y, 0));
        c.TorchMinutes = 60;
        Assert.Equal(WaspResult.TooFar, wasps.SmokeOut(nest.TileIndex, c, nest.X + 6, nest.Y, 0));
        Assert.Equal(WaspResult.SmokedOut, wasps.SmokeOut(nest.TileIndex, c, nest.X + 1, nest.Y, 0));
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.NestSmokedOut);
        Assert.DoesNotContain(wasps.Active(0), n => n.TileIndex == nest.TileIndex);

        // Uloží se a po načtení je hnízdo pořád pryč, po pár dnech se vrátí.
        var restored = new WaspNests(_island);
        restored.Restore(wasps.Export());
        Assert.DoesNotContain(restored.Active(10), n => n.TileIndex == nest.TileIndex);
        var back = GameData.Hazards.Wasps.RegrowDays * 1440 + 1;
        restored.Update(0.1, back, []);
        Assert.Contains(restored.Active(back), n => n.TileIndex == nest.TileIndex);
    }

    [Fact]
    public void NestsComeFromSeedInForestAndMeadowTrees()
    {
        var a = new WaspNests(_island).Active(0).Select(n => n.TileIndex).ToList();
        var b = new WaspNests(WorldGenerator.Build(1, 128, 128)).Active(0).Select(n => n.TileIndex).ToList();

        Assert.NotEmpty(a);
        Assert.Equal(a, b);
        Assert.All(a, i => Assert.Contains(_island.Regions[i], new[] { RegionKind.Forest, RegionKind.Meadow }));
    }

    private static void Eat(Character c, int archetype, PlantPart part, GameClock clock)
    {
        var secret = _island.SpeciesSecrets.First(s => s.ArchetypeIndex == archetype);
        Eating.Eat(c, new PlantItem(secret.Id, part, Ripeness.Ripe, Season.Spring, TileType.Grass), secret, clock);
    }

    private static void Run(Animals animals, GameClock clock, AnimalTarget player, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 0.05)
        {
            animals.Update(_island, new Fires(), new PlantStates(), clock, 0.05, [player]);
        }
    }

    /// <summary>Střed dlaždice daného druhu, kolem které je ve čtverci 5 × 5 volno.</summary>
    private static (double X, double Y) Tile(TileType type)
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width, i / _island.Width);
            if (_island.Tiles[i] == type
                && Enumerable.Range(-2, 5).All(dy => Enumerable.Range(-2, 5).All(dx => !Movement.IsBlocked(_island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Taková dlaždice na ostrově není.");
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

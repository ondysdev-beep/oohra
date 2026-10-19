using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Zvířata, lov a zranění (prototyp updateAnimals, attack, wound a meatQty).</summary>
public class AnimalTests
{
    // Velký ostrov s volnými loukami (pro pohyb zvířat). Počty z prototypu se kontrolují na ostrově 84 × 84.
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);
    private static readonly Island _prototypeSize = WorldGenerator.Build(12345, 84, 84);

    [Fact]
    public void PopulationMatchesPrototypeAndScalesWithIsland()
    {
        // Divočáků je víc než v prototypu (5): jsou nebezpečnější, na přání vývojáře.
        Assert.Equal(7, Animals.TargetCount(_prototypeSize, AnimalKind.Boar));
        Assert.Equal(12, Animals.TargetCount(_prototypeSize, AnimalKind.Rabbit));
        Assert.Equal(5, Animals.TargetCount(_prototypeSize, AnimalKind.Deer));
        Assert.Equal(2, Animals.TargetCount(_prototypeSize, AnimalKind.Wolf));

        var big = WorldGenerator.Build(1, 128, 128);
        Assert.Equal(28, Animals.TargetCount(big, AnimalKind.Rabbit));
    }

    [Fact]
    public void AnimalsSpawnOnGoodGroundAwayFromPlayers()
    {
        var (px, py) = OpenSpot();
        var players = new[] { Player(1, px, py) };
        var animals = new Animals(7);

        animals.Populate(_island, players);

        // Ostrov 128 × 128: 16 divočáků, 28 králíků, 12 jelenů, 5 vlků, 5 hadů.
        Assert.Equal(66, animals.All.Count);
        foreach (var a in animals.All)
        {
            Assert.True(Movement.CanStand(_island, a.X, a.Y, 0.2));
            Assert.True(Distance(a.X, a.Y, px, py) >= 6);
        }
    }

    [Fact]
    public void RabbitFleesFromPlayer()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var rabbit = animals.Place(AnimalKind.Rabbit, px + 3, py);
        var players = new[] { Player(1, px, py) };

        Run(animals, TestPlants.Clock(), players, 1);

        Assert.True(Distance(rabbit.X, rabbit.Y, px, py) > 4, $"Králík je {Distance(rabbit.X, rabbit.Y, px, py):0.0} m od hráče.");
    }

    [Fact]
    public void RabbitSleepsAtNight()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var rabbit = animals.Place(AnimalKind.Rabbit, px + 4, py);

        Run(animals, Night(), [Player(1, px, py)], 0.5);

        Assert.Equal(AnimalState.Asleep, rabbit.State);
    }

    /// <summary>Divočák se na blízkého hráče zaměří, vyrazí a zraní ho. Pak je vyčerpaný.</summary>
    [Fact]
    public void BoarChargesAndWounds()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var boar = animals.Place(AnimalKind.Boar, px + 2.5, py);
        var player = Player(1, px, py);

        var clock = TestPlants.Clock();
        for (var i = 0; i < 200 && player.Character.Bleed <= 0; i++)
        {
            animals.Update(_island, new Fires(), new PlantStates(), clock, 0.05, [player]);
        }

        Assert.True(player.Character.Health < 100);
        Assert.True(player.Character.Bleed > 0);
        Assert.True(player.Character.Limp > 0);
        Assert.Equal(AnimalState.Exhaust, boar.State);
        var messages = player.Character.TakeMessages();
        Assert.Contains(messages, m => m.Event == BodyEvent.BoarAlert);
        Assert.Contains(messages, m => m.Event == BodyEvent.WoundedByBoar);
    }

    /// <summary>Kdo uhne do strany, než divočák doběhne, toho nezraní (výpad jde rovně).</summary>
    [Fact]
    public void DodgingTheCharge()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var boar = animals.Place(AnimalKind.Boar, px + 2.5, py);
        var clock = TestPlants.Clock();
        var player = Player(1, px, py);
        for (var i = 0; i < 200 && boar.State != AnimalState.Charge; i++)
        {
            animals.Update(_island, new Fires(), new PlantStates(), clock, 0.05, [player]);
        }

        Assert.Equal(AnimalState.Charge, boar.State);
        var dodged = Player(1, px, py + 1.5, player.Character);
        for (var i = 0; i < 40 && boar.State == AnimalState.Charge; i++)
        {
            animals.Update(_island, new Fires(), new PlantStates(), clock, 0.05, [dodged]);
        }

        Assert.Equal(100, player.Character.Health);
        Assert.Equal(AnimalState.Exhaust, boar.State);
    }

    [Fact]
    public void WolfStalksExposedPlayerAtNight()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var wolf = animals.Place(AnimalKind.Wolf, px + 6, py);
        var player = Player(1, px, py);

        Run(animals, Night(), [player], 0.2);

        Assert.Contains(wolf.State, new[] { AnimalState.Stalk, AnimalState.Alert, AnimalState.Charge, AnimalState.Exhaust });
        Assert.Contains(player.Character.TakeMessages(), m => m.Event == BodyEvent.Stalked);
    }

    [Fact]
    public void FireKeepsWolfAway()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var wolf = animals.Place(AnimalKind.Wolf, px + 6, py);
        var fires = new Fires();
        var materials = new Inventory(10);
        for (var i = 0; i < 3; i++)
        {
            materials.TryAdd(Item.Stick);
        }

        materials.TryAdd(Item.Stone);
        Assert.Equal(FireResult.Lit, fires.Light(_island, materials, px, py, px + 1, py));

        var clock = Night();
        for (var i = 0; i < 20; i++)
        {
            animals.Update(_island, fires, new PlantStates(), clock, 0.1, [Player(1, px, py)]);
        }

        Assert.Equal(AnimalState.Wander, wolf.State);
    }

    [Fact]
    public void TorchKeepsWolfAway()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var wolf = animals.Place(AnimalKind.Wolf, px + 6, py);
        var player = Player(1, px, py);
        player.Character.TorchMinutes = 60;

        Run(animals, Night(), [player], 0.2);

        Assert.Equal(AnimalState.Wander, wolf.State);
        Assert.DoesNotContain(player.Character.TakeMessages(), m => m.Event == BodyEvent.Stalked);
    }

    [Fact]
    public void WolfIgnoresPlayersByDay()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var wolf = animals.Place(AnimalKind.Wolf, px + 6, py);

        Run(animals, TestPlants.Clock(), [Player(1, px, py)], 1);

        Assert.Equal(AnimalState.Wander, wolf.State);
    }

    [Fact]
    public void RabbitIsCaughtWithOneHit()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var rabbit = animals.Place(AnimalKind.Rabbit, px + 1, py);
        var hunter = Player(1, px, py);

        Assert.Equal(HuntResult.CaughtSmall, animals.Attack(rabbit.Id, hunter, new Inventory(10), 0));
        Assert.True(rabbit.IsDead);
        Assert.Equal(2, hunter.Character.Skills.Pending(SkillKind.Hunt), 9);
    }

    /// <summary>Pěst ubere 1, kopí 3. Mezi ranami je krátká prodleva. Silné zvíře padne po několika ranách.</summary>
    [Fact]
    public void SpearHitsHarderThanFist()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var boar = animals.Place(AnimalKind.Boar, px + 1, py);
        var hunter = Player(1, px, py);
        var spear = new Inventory(10);
        spear.TryAdd(Item.Spear);

        Assert.Equal(HuntResult.HitFist, animals.Attack(boar.Id, hunter, new Inventory(10), 0));
        Assert.Equal(GameData.Animals.Boar.Hp - 1, boar.Hp);
        Assert.Equal(HuntResult.TooSoon, animals.Attack(boar.Id, hunter, spear, 0));

        animals.Update(_island, new Fires(), new PlantStates(), TestPlants.Clock(), 0.5, []);
        Assert.Equal(HuntResult.HitSpear, animals.Attack(boar.Id, hunter, spear, 0));
        // Pěst ubere 1, kopí 3.
        Assert.Equal(GameData.Animals.Boar.Hp - 4, boar.Hp);

        // Divočák (9 životů) vydrží ještě jednu ránu kopím, druhá ho zabije.
        animals.Update(_island, new Fires(), new PlantStates(), TestPlants.Clock(), 0.5, []);
        Assert.Equal(HuntResult.HitSpear, animals.Attack(boar.Id, Player(1, boar.X - 1, boar.Y, hunter.Character), spear, 0));
        animals.Update(_island, new Fires(), new PlantStates(), TestPlants.Clock(), 0.5, []);
        Assert.Equal(HuntResult.Killed, animals.Attack(boar.Id, Player(1, boar.X - 1, boar.Y, hunter.Character), spear, 0));
        Assert.True(boar.IsDead);
        Assert.Equal(HuntResult.TooFar, animals.Attack(animals.Place(AnimalKind.Wolf, px + 5, py).Id, hunter, spear, 0));
    }

    [Fact]
    public void ButcheringGivesMeatAndRemovesBody()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(1);
        var deer = animals.Place(AnimalKind.Deer, px + 1, py);
        var inventory = new Inventory(10);

        Assert.Equal(HuntResult.NotDead, animals.Butcher(deer.Id, inventory, px, py, 0));
        deer.Hp = 1;
        animals.Attack(deer.Id, Player(1, px, py), inventory, 0);
        Assert.Equal(HuntResult.TooFar, animals.Butcher(deer.Id, inventory, px - 5, py, 0));

        Assert.Equal(HuntResult.Butchered, animals.Butcher(deer.Id, inventory, px, py, 0));

        Assert.Equal(4, inventory.Count(ItemKind.Meat));
        Assert.Null(animals.Find(deer.Id));
    }

    [Fact]
    public void CorpsesDisappearAndAnimalsComeBack()
    {
        var (px, py) = OpenSpot();
        var animals = new Animals(3);
        var clock = TestPlants.Clock();
        var rabbit = animals.Place(AnimalKind.Rabbit, px + 1, py);
        animals.Update(_island, new Fires(), new PlantStates(), clock, 0.1, []);
        animals.Attack(rabbit.Id, Player(1, px, py), new Inventory(10), clock.TotalMinutes);

        clock.Advance(clock.RealSecondsFor(1450));
        animals.Update(_island, new Fires(), new PlantStates(), clock, 0.1, []);

        Assert.Null(animals.Find(rabbit.Id));
        Assert.Contains(animals.All, a => a.Kind == AnimalKind.Rabbit && !a.IsDead);
    }

    /// <summary>Pradávný snese zranění lépe než Nový. Zkušený lovec taky.</summary>
    [Fact]
    public void WoundsDependOnGenerationAndHunting()
    {
        var ancient = new Character(0, new Mulberry32(1));
        var newer = new Character(4, new Mulberry32(1));
        var hunter = new Character(4, new Mulberry32(1));
        hunter.Skills.Practice(SkillKind.Hunt, 50, 1);
        hunter.Skills.Consolidate(out _);

        ancient.Wound(30, AnimalKind.Boar, 0);
        newer.Wound(30, AnimalKind.Boar, 0);
        hunter.Wound(30, AnimalKind.Boar, 0);

        Assert.True(ancient.Health > newer.Health);
        Assert.True(hunter.Health > newer.Health);
        Assert.Contains(newer.Symptoms(), s => s == Symptom.Bleeding);
        Assert.Contains(newer.Symptoms(), s => s == Symptom.Limping);
        Assert.True(newer.StunSeconds > 0);
    }

    [Fact]
    public void BleedingHurtsAndStopsHealingThenEnds()
    {
        var c = new Character(1, new Mulberry32(1)) { Satiety = 100, Hydration = 100 };
        c.Wound(40, AnimalKind.Wolf, 0);
        var afterHit = c.Health;
        var clock = TestPlants.Clock();

        Physiology.Run(c, clock, 60);
        Assert.True(c.Health < afterHit);

        Physiology.Run(c, clock, 200);
        Assert.Equal(0, c.Bleed);
        Assert.True(c.Limp < 40 * 1.8);
    }

    [Fact]
    public void LimpingSlowsWalking()
    {
        var c = new Character(0, new Mulberry32(1));
        var normal = Movement.BodySpeed(c);
        c.Limp = 100;

        Assert.True(Movement.BodySpeed(c) < normal * 0.6);
    }

    [Fact]
    public void BakedMeatFeedsMoreThanRaw()
    {
        var raw = new Character(0, new Mulberry32(1)) { Satiety = 20 };
        var baked = new Character(0, new Mulberry32(1)) { Satiety = 20 };

        Eating.EatMeat(raw, Plants.Cooking.Raw, TestPlants.Clock());
        Eating.EatMeat(baked, Plants.Cooking.Baked, TestPlants.Clock());

        Assert.True(baked.Satiety > raw.Satiety);
        Assert.True(raw.Satiety > 40);
    }

    [Fact]
    public void MeatCanBeBakedAtFire()
    {
        var (px, py) = OpenSpot();
        var fires = new Fires();
        var inventory = new Inventory(10);
        inventory.TryAdd(Item.RawMeat(0));
        for (var i = 0; i < 3; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        inventory.TryAdd(Item.Stone);
        fires.Light(_island, inventory, px, py, px + 1, py);

        Assert.Equal(FireResult.Baked, fires.Bake(_island, inventory, 0, px, py));
        Assert.Equal(Item.RawMeat(0) with { Cooking = Plants.Cooking.Baked }, Assert.Single(inventory.Stacks).Item);
    }

    /// <summary>Pradávný kopí umí od narození (klacek a kámen), jedno stačí, procvičí řemeslo.</summary>
    [Fact]
    public void SpearFromStickAndStone()
    {
        var c = new Character(0, new Mulberry32(1));
        var inventory = new Inventory(10);
        var place = new CraftPlace(false, false, 0);
        Assert.Equal(CraftResult.NeedMaterials, Crafting.Make("spear", inventory, c, place));

        inventory.TryAdd(Item.Stick);
        inventory.TryAdd(Item.Stone);
        Assert.Equal(CraftResult.Made, Crafting.Make("spear", inventory, c, place));
        Assert.Equal(1, inventory.Count(ItemKind.Spear));
        Assert.Equal(CraftResult.AlreadyHave, Crafting.Make("spear", inventory, c, place));
        Assert.True(c.Skills.Pending(SkillKind.Craft) > 0);
    }

    private static void Run(Animals animals, GameClock clock, AnimalTarget[] players, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 0.05)
        {
            animals.Update(_island, new Fires(), new PlantStates(), clock, 0.05, players);
        }
    }

    private static GameClock Night()
    {
        var clock = TestPlants.Clock();
        clock.Advance(clock.RealSecondsFor(15 * 60));
        return clock;
    }

    private static AnimalTarget Player(int id, double x, double y, Character? c = null)
    {
        return new AnimalTarget(id, x, y, c ?? new Character(0, new Mulberry32(id)));
    }

    /// <summary>Střed travnaté dlaždice, kolem které je ve čtverci 9 × 9 volno (bez vody a stromů).</summary>
    private static (double X, double Y) OpenSpot()
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width, i / _island.Width);
            if (_island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-4, 9).All(dy => Enumerable.Range(-4, 9).All(dx => !Movement.IsBlocked(_island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není dost volného místa.");
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }
}

using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Kažení jídla, pasti a rybaření (prototyp spoiled, trapCheck a fish).</summary>
public class TrapFishSpoilTests
{
    private const double Day = 1440;
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    [Fact]
    public void MeatAndFruitSpoilAfterDays()
    {
        var meat = Item.RawMeat(0);
        Assert.False(Spoilage.IsSpoiled(meat, 1.9 * Day));
        Assert.True(Spoilage.IsSpoiled(meat, 2.1 * Day));
        Assert.False(Spoilage.IsSpoiled(meat with { Cooking = Cooking.Baked }, 4.4 * Day));
        Assert.True(Spoilage.IsSpoiled(Item.RawFish(0), 2.1 * Day));

        var fruit = Item.Of(new PlantItem(0, PlantPart.Fruit, Ripeness.Ripe, Season.Summer, TileType.Grass, Day: 0));
        Assert.False(Spoilage.IsSpoiled(fruit, 3.9 * Day));
        Assert.True(Spoilage.IsSpoiled(fruit, 4.1 * Day));

        var leaf = Item.Of(new PlantItem(0, PlantPart.Leaf, Ripeness.Ripe, Season.Summer, TileType.Grass));
        Assert.False(Spoilage.IsSpoiled(leaf, 50 * Day));
    }

    [Fact]
    public void ItemsFromDifferentDaysDoNotStack()
    {
        var inventory = new Inventory(10);
        inventory.TryAdd(Item.RawMeat(1));
        inventory.TryAdd(Item.RawMeat(1));
        inventory.TryAdd(Item.RawMeat(2));

        Assert.Equal(2, inventory.Stacks.Count);
    }

    [Fact]
    public void SpoiledFoodFeedsLessAndMakesSick()
    {
        var fresh = new Character(3, new Mulberry32(4)) { Satiety = 20 };
        var spoiled = new Character(3, new Mulberry32(4)) { Satiety = 20 };

        Eating.EatMeat(fresh, Cooking.Baked, TestPlants.Clock());
        Eating.EatMeat(spoiled, Cooking.Baked, TestPlants.Clock(), spoiled: true);

        Assert.True(fresh.Satiety > spoiled.Satiety);
        Assert.DoesNotContain(fresh.Scheduled, e => e.Type == EffectType.Sickness);

        // Nový onemocní zkaženým jídlem v 85 % případů.
        var sick = Enumerable.Range(1, 20).Count(seed =>
        {
            var c = new Character(3, new Mulberry32(seed));
            Eating.EatMeat(c, Cooking.Baked, TestPlants.Clock(), spoiled: true);
            return c.Scheduled.Any(e => e.Type == EffectType.Sickness);
        });
        Assert.InRange(sick, 12, 20);
    }

    /// <summary>Pradávný (železný žaludek) onemocní ze zkaženého jídla méně často a slaběji.</summary>
    [Fact]
    public void AncientsHandleSpoiledFoodBetter()
    {
        var ancientSick = 0;
        var newSick = 0;
        for (var seed = 1; seed <= 200; seed++)
        {
            var ancient = new Character(0, new Mulberry32(seed));
            var newer = new Character(2, new Mulberry32(seed));
            Eating.EatMeat(ancient, Cooking.Baked, TestPlants.Clock(), spoiled: true);
            Eating.EatMeat(newer, Cooking.Baked, TestPlants.Clock(), spoiled: true);
            ancientSick += ancient.Scheduled.Count(e => e.Type == EffectType.Sickness);
            newSick += newer.Scheduled.Count(e => e.Type == EffectType.Sickness);
        }

        Assert.True(ancientSick < newSick / 1.5, $"Pradávní {ancientSick}, Noví {newSick}");
    }

    [Fact]
    public void TastingSpoiledFoodWarns()
    {
        var c = new Character(1, new Mulberry32(1));
        Eating.EatMeat(c, Cooking.Baked, TestPlants.Clock(), taste: true, spoiled: true);

        var tasted = Assert.Single(c.TakeMessages(), m => m.Event == BodyEvent.Tasted);
        Assert.Contains(TasteNote.Rotten, tasted.Notes!);
    }

    [Fact]
    public void TrapCatchesRabbitAndGivesMeat()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var (px, py) = OpenSpot(island);
        var inventory = Sticks(3);
        Assert.Equal(BuildResult.Built, island.Structures.Build(island, new Fires(), inventory, px, py, BuildKind.Trap, px + 2, py, 0, 0, []));
        var trap = island.Structures.All.Single();
        Assert.Equal(0, inventory.Count(ItemKind.Stick));

        // Králík se pase přímo na pasti (nikdo ho neplaší).
        var animals = new Animals(1);
        var rabbit = animals.Place(AnimalKind.Rabbit, trap.X, trap.Y);
        var clock = TestPlants.Clock();
        for (var i = 0; i < 200 && trap.Caught is null; i++)
        {
            rabbit.X = trap.X;
            rabbit.Y = trap.Y;
            animals.Update(island, new Fires(), new PlantStates(), clock, 0.05, []);
        }

        Assert.Equal(AnimalKind.Rabbit, trap.Caught);
        Assert.Null(animals.Find(rabbit.Id));

        var bag = new Inventory(10);
        Assert.Equal(HuntResult.TooFar, island.Structures.TakeCatch(trap.Id, bag, px - 5, py, clock.TotalMinutes));
        Assert.Equal(HuntResult.Butchered, island.Structures.TakeCatch(trap.Id, bag, trap.X - 1, trap.Y, clock.TotalMinutes));
        Assert.Equal(1, bag.Count(ItemKind.Meat));
        Assert.Equal(HuntResult.TrapEmpty, island.Structures.TakeCatch(trap.Id, bag, trap.X - 1, trap.Y, clock.TotalMinutes));

        // Zvednutí pasti vrátí 2 klacky.
        Assert.Equal(BuildResult.Removed, island.Structures.Remove(trap.Id, bag, trap.X - 1, trap.Y));
        Assert.Equal(2, bag.Count(ItemKind.Stick));
    }

    [Fact]
    public void TrapDoesNotBlockWalking()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var (px, py) = OpenSpot(island);
        island.Structures.Place(BuildKind.Trap, px + 1, py, 0, length: 0.01);

        Assert.True(Movement.CanStand(island, px + 1, py, 0.28));
    }

    [Fact]
    public void FishingNeedsSpearAndWater()
    {
        var (wx, wy) = WaterEdge();
        var animals = new Animals(1);
        var fisher = new AnimalTarget(1, wx, wy, new Character(0, new Mulberry32(1)));
        var (tx, ty) = ((int)Math.Floor(wx) + 1, (int)Math.Floor(wy));

        Assert.Equal(HuntResult.NeedSpear, animals.Fish(_island, tx, ty, fisher, new Inventory(10), 0));

        var gear = new Inventory(10);
        gear.TryAdd(Item.Spear);
        Assert.Equal(HuntResult.NotWater, animals.Fish(_island, tx - 1, ty, fisher, gear, 0));

        var caught = 0;
        for (var i = 0; i < 60; i++)
        {
            var result = animals.Fish(_island, tx, ty, fisher, gear, 0);
            Assert.Contains(result, new[] { HuntResult.FishCaught, HuntResult.FishEscaped, HuntResult.TooSoon });
            if (result == HuntResult.FishCaught)
            {
                caught++;
            }

            animals.Update(_island, new Fires(), new PlantStates(), TestPlants.Clock(), 0.6, []);
        }

        Assert.InRange(caught, 3, 20);
        Assert.Equal(caught, gear.Count(ItemKind.Fish));
        Assert.True(fisher.Character.Skills.Pending(SkillKind.Hunt) > 0);
    }

    /// <summary>I když jsou zvířata vypnutá (žádná se nerodí), čas pro prodlevy běží a rybařit jde opakovaně.</summary>
    [Fact]
    public void FishingWorksWithAnimalsDisabled()
    {
        var (wx, wy) = WaterEdge();
        var animals = new Animals(1) { Enabled = false };
        var fisher = new AnimalTarget(1, wx, wy, new Character(0, new Mulberry32(1)));
        var gear = new Inventory(10);
        gear.TryAdd(Item.Spear);
        var (tx, ty) = ((int)Math.Floor(wx) + 1, (int)Math.Floor(wy));
        var clock = TestPlants.Clock();

        animals.Fish(_island, tx, ty, fisher, gear, 0);
        animals.Update(_island, new Fires(), new PlantStates(), clock, 1.5, []);
        clock.Advance(clock.RealSecondsFor(800));
        animals.Update(_island, new Fires(), new PlantStates(), clock, 0.1, []);

        Assert.NotEqual(HuntResult.TooSoon, animals.Fish(_island, tx, ty, fisher, gear, 0));
        Assert.Empty(animals.All);
    }

    private static Inventory Sticks(int count)
    {
        var inventory = new Inventory(10);
        for (var i = 0; i < count; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        return inventory;
    }

    /// <summary>Střed suché dlaždice, východně od které je jezírko nebo moře.</summary>
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

    private static (double X, double Y) OpenSpot(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-4, 9).All(dy => Enumerable.Range(-4, 9).All(dx => !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

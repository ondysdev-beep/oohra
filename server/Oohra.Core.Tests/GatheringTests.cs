using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;
using static Oohra.Core.Tests.TestPlants;

namespace Oohra.Core.Tests;

/// <summary>Sběr rostlin, batoh a pití (prototyp pluck, addItem a drinkKind).</summary>
public class GatheringTests
{
    private static readonly Island _island = WorldGenerator.Build(12345, 128, 128);

    [Fact]
    public void PickedLeafGoesToInventoryAndRegrowsLater()
    {
        var plant = _island.Plants[0];
        var states = new PlantStates();
        var inventory = new Inventory(10);
        var clock = Clock();

        Assert.Equal(HarvestResult.Picked, Harvest(plant, PlantPart.Leaf, states, inventory, clock));
        Assert.Equal(HarvestResult.NoLeavesLeft, Harvest(plant, PlantPart.Leaf, states, inventory, clock));

        clock.Advance(clock.RealSecondsFor(701));
        Assert.Equal(HarvestResult.Picked, Harvest(plant, PlantPart.Leaf, states, inventory, clock));

        var stack = Assert.Single(inventory.Stacks);
        Assert.Equal(PlantPart.Leaf, stack.Item.Plant!.Part);
        Assert.Equal(plant.SpeciesId, stack.Item.Plant!.SpeciesId);
        Assert.Equal(plant.Tile, stack.Item.Plant!.Soil);
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void DiggingRootRemovesPlantForDays()
    {
        var plant = _island.Plants[1];
        var states = new PlantStates();
        var inventory = new Inventory(10);
        var clock = Clock();

        Assert.Equal(HarvestResult.Picked, Harvest(plant, PlantPart.Root, states, inventory, clock));
        Assert.True(states.IsGone(plant.Id, clock.TotalMinutes));
        Assert.Equal(HarvestResult.PlantGone, Harvest(plant, PlantPart.Leaf, states, inventory, clock));

        clock.Advance(clock.RealSecondsFor(4 * 1440 + 1));
        Assert.False(states.IsGone(plant.Id, clock.TotalMinutes));
    }

    [Fact]
    public void CannotPickFromFarAway()
    {
        var plant = _island.Plants[0];

        var result = Gathering.Harvest(_island, new PlantStates(), plant, PlantPart.Leaf, Clock(), new Inventory(10), plant.X + 2, plant.Y);

        Assert.Equal(HarvestResult.TooFar, result);
    }

    [Fact]
    public void FruitOnlyWhenPlantBearsFruit()
    {
        var states = new PlantStates();
        var clock = Clock();
        foreach (var plant in _island.Plants.Take(40))
        {
            var stage = Gathering.FruitStage(_island, states, plant, clock);
            var result = Harvest(plant, PlantPart.Fruit, states, new Inventory(10), clock);

            Assert.Equal(stage is null ? HarvestResult.NoFruit : HarvestResult.Picked, result);
        }
    }

    [Fact]
    public void PickedFruitIsGoneForADay()
    {
        var states = new PlantStates();
        var clock = Clock();
        var plant = FindFruiting(states, clock);

        Assert.Equal(HarvestResult.Picked, Harvest(plant, PlantPart.Fruit, states, new Inventory(10), clock));
        Assert.Null(Gathering.FruitStage(_island, states, plant, clock));
    }

    [Fact]
    public void FullInventoryRefusesNewItems()
    {
        var inventory = new Inventory(1);
        var states = new PlantStates();
        var clock = Clock();
        var first = _island.Plants[0];
        var other = _island.Plants.First(p => p.SpeciesId != first.SpeciesId);

        Assert.Equal(HarvestResult.Picked, Harvest(first, PlantPart.Leaf, states, inventory, clock));
        Assert.Equal(HarvestResult.InventoryFull, Harvest(other, PlantPart.Leaf, states, inventory, clock));
        Assert.False(states.Find(other.Id)?.LeavesAt > clock.TotalMinutes);
    }

    [Fact]
    public void StacksHoldAtMostNine()
    {
        var inventory = new Inventory(10);
        var item = Item(PlantPart.Leaf);

        for (var i = 0; i < 10; i++)
        {
            Assert.True(inventory.TryAdd(item));
        }

        Assert.Equal(new[] { 9, 1 }, inventory.Stacks.Select(s => s.Count));
    }

    [Fact]
    public void TakeOneAndDrop()
    {
        var inventory = new Inventory(10);
        inventory.TryAdd(Item(PlantPart.Leaf));
        inventory.TryAdd(Item(PlantPart.Leaf));
        inventory.TryAdd(Item(PlantPart.Root));

        Assert.Equal(PlantPart.Leaf, inventory.TakeOne(0)!.Plant!.Part);
        Assert.Equal(1, inventory.Stacks[0].Count);
        inventory.Drop(0);
        Assert.Equal(PlantPart.Root, Assert.Single(inventory.Stacks).Item.Plant!.Part);
        Assert.Null(inventory.TakeOne(5));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 10)]
    [InlineData(2, 11)]
    [InlineData(6, 13)]
    [InlineData(20, 13)]
    public void LaterGenerationsCarryMore(int generation, int slots)
    {
        Assert.Equal(slots, Inventory.SlotsFor(generation));
    }

    [Fact]
    public void PondWaterQuenchesThirst()
    {
        var c = Person();
        c.Hydration = 20;
        var (x, y) = FindTile(TileType.Pond);

        var result = Drinking.Drink(c, _island, x, y, x + 0.5, y + 1.5, Clock());

        Assert.Equal(DrinkResult.Clean, result);
        Assert.Equal(60, c.Hydration);
        Assert.Empty(c.Scheduled);
    }

    [Fact]
    public void SeaWaterMakesThirstWorse()
    {
        var c = Person();
        var (x, y) = FindTile(TileType.Sea);

        Assert.Equal(DrinkResult.Salty, Drinking.Drink(c, _island, x, y, x + 0.5, y + 0.5, Clock()));
        Assert.Equal(75 - 8, c.Hydration);
    }

    [Fact]
    public void MuddyWaterSometimesMakesSick()
    {
        var c = Person();
        var clock = Clock();
        var (x, y) = FindTile(TileType.Bog);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(DrinkResult.Muddy, Drinking.Drink(c, _island, x, y, x + 0.5, y + 0.5, clock));
        }

        Assert.Contains(c.Scheduled, e => e.Type == EffectType.Sickness);
        Physiology.Run(c, clock, 501);
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.FeltSick);
        Assert.True(c.Infection > 0);
    }

    [Fact]
    public void LaterGenerationsSipMuddyWater()
    {
        var c = Person(generation: 2);
        c.Hydration = 20;
        var (x, y) = FindTile(TileType.Bog);

        Assert.Equal(DrinkResult.MuddySip, Drinking.Drink(c, _island, x, y, x + 0.5, y + 0.5, Clock()));
        Assert.Equal(34, c.Hydration);
    }

    [Fact]
    public void CannotDrinkFarFromWaterOrFromLand()
    {
        var c = Person();
        var (x, y) = FindTile(TileType.Pond);
        var camp = _island.Camps[0];

        Assert.Equal(DrinkResult.TooFar, Drinking.Drink(c, _island, x, y, x + 5, y, Clock()));
        Assert.Equal(DrinkResult.NotWater, Drinking.Drink(c, _island, camp.X, camp.Y, camp.X + 0.5, camp.Y + 0.5, Clock()));
    }

    [Fact]
    public void AfterDeathYouAreBornAsNextGeneration()
    {
        Assert.Equal(1, GenerationTraits.NextAfterDeath(0));
        Assert.Equal(3, GenerationTraits.NextAfterDeath(2));
        Assert.Equal(GameData.Generations.Cap, GenerationTraits.NextAfterDeath(GameData.Generations.Cap));
    }

    [Fact]
    public void RebirthIsAwayFromPlaceOfDeath()
    {
        var death = SpawnPoint.PickWild(_island, new Mulberry32(1), firstLife: true);

        var reborn = SpawnPoint.PickWild(_island, new Mulberry32(2), firstLife: false, avoid: death);

        var distance = Math.Sqrt(Math.Pow(reborn.X - death.X, 2) + Math.Pow(reborn.Y - death.Y, 2));
        Assert.True(distance >= 21);
    }

    private static HarvestResult Harvest(PlantInstance plant, PlantPart part, PlantStates states, Inventory inventory, GameClock clock)
    {
        return Gathering.Harvest(_island, states, plant, part, clock, inventory, plant.X, plant.Y);
    }

    private static PlantInstance FindFruiting(PlantStates states, GameClock clock)
    {
        for (var month = 0; month < 12; month++)
        {
            foreach (var plant in _island.Plants)
            {
                if (Gathering.FruitStage(_island, states, plant, clock) is not null)
                {
                    return plant;
                }
            }

            clock.Advance(32400);
        }

        throw new InvalidOperationException("Žádná rostlina neplodí.");
    }

    private static (int X, int Y) FindTile(TileType type)
    {
        var index = Array.IndexOf(_island.Tiles, type);
        return (index % _island.Width, index / _island.Width);
    }
}

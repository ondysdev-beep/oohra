using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Uzení a sušení masa (návrh 0.8, kap. 5): maso vydrží déle, uzení jen nad hořícím ohněm, sušení ve dne za sucha.</summary>
public class PreservingTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    [Fact]
    public void MeatSmokedOverBurningFireLastsLonger()
    {
        var (fires, fire) = BurningFire();
        var racks = new MeatRacks();
        var inventory = Meat(Item.RawMeat(0), 2);
        var clock = Clock(hour: 12, day: 0);

        Assert.Equal(PreserveResult.Hung, racks.Hang(RackHolder.Fire, fire.Id, inventory, 0, clock.TotalMinutes));
        Assert.Empty(inventory.Stacks);
        Assert.Equal(2, racks.On(RackHolder.Fire, fire.Id).Count);

        racks.Update(fires, new Structures(), clock, WeatherKind.Clear, GameData.Preserving.SmokeMinutes);

        Assert.All(racks.On(RackHolder.Fire, fire.Id), p => Assert.Equal(Cooking.Smoked, p.Item.Cooking));
        Assert.Equal(PreserveResult.Taken, racks.Take(RackHolder.Fire, fire.Id, -1, inventory));
        var smoked = inventory.Stacks[0].Item;
        Assert.False(Spoilage.IsSpoiled(smoked, 8 * GameClock.MinutesPerDay), "Uzené maso vydrží přes týden.");
        Assert.True(Spoilage.IsSpoiled(Item.RawMeat(0), 3 * GameClock.MinutesPerDay), "Syrové maso se zkazí za pár dní.");
    }

    [Fact]
    public void SmokingStopsWhenFireGoesOut()
    {
        var (fires, fire) = BurningFire();
        var racks = new MeatRacks();
        racks.Hang(RackHolder.Fire, fire.Id, Meat(Item.RawMeat(0), 1), 0, 0);
        fires.Burn(fire.BurnMinutes + 1);

        racks.Update(fires, new Structures(), Clock(hour: 12, day: 0), WeatherKind.Clear, GameData.Preserving.SmokeMinutes * 2);

        Assert.Equal(Cooking.Raw, racks.On(RackHolder.Fire, fire.Id)[0].Item.Cooking);
    }

    [Fact]
    public void OnlyFreshRawMeatCanBeHungAndRackHasSlots()
    {
        var racks = new MeatRacks();
        Assert.Equal(PreserveResult.NotMeat, racks.Hang(RackHolder.Fire, 1, Meat(Item.RawMeat(0) with { Cooking = Cooking.Baked }, 1), 0, 0));
        Assert.Equal(PreserveResult.NotMeat, racks.Hang(RackHolder.Fire, 1, Meat(Item.Stick, 1), 0, 0));
        Assert.Equal(PreserveResult.Spoiled, racks.Hang(RackHolder.Fire, 1, Meat(Item.RawMeat(0), 1), 0, 5 * GameClock.MinutesPerDay));

        var many = Meat(Item.RawFish(0), GameData.Preserving.FireSlots + 2);
        Assert.Equal(PreserveResult.Hung, racks.Hang(RackHolder.Fire, 1, many, 0, 0));
        Assert.Equal(GameData.Preserving.FireSlots, racks.On(RackHolder.Fire, 1).Count);
        Assert.Equal(2, many.Count(ItemKind.Fish));
        Assert.Equal(PreserveResult.RackFull, racks.Hang(RackHolder.Fire, 1, many, 0, 0));
    }

    /// <summary>Na sušáku se suší jen ve dne a když neprší. Sušené vydrží nejdéle.</summary>
    [Fact]
    public void MeatDriesOnRackOnlyInDryDaylight()
    {
        var structures = new Structures();
        var rack = structures.Place(BuildKind.Decoration, 10, 10, 0, decorationId: GameData.Preserving.RackDecoration);
        var racks = new MeatRacks();
        racks.Hang(RackHolder.Structure, rack.Id, Meat(Item.RawMeat(0), 1), 0, 0);
        var dry = GameData.Preserving.DryMinutes;

        racks.Update(new Fires(), structures, Clock(hour: 1, day: 0), WeatherKind.Clear, dry);
        racks.Update(new Fires(), structures, Clock(hour: 12, day: 0), WeatherKind.Rain, dry);
        Assert.Equal(Cooking.Raw, racks.On(RackHolder.Structure, rack.Id)[0].Item.Cooking);

        racks.Update(new Fires(), structures, Clock(hour: 12, day: 0), WeatherKind.Clear, dry / 2);
        Assert.Equal(0.5, MeatRacks.Fraction(RackHolder.Structure, racks.On(RackHolder.Structure, rack.Id)[0]), 3);
        racks.Update(new Fires(), structures, Clock(hour: 12, day: 0), WeatherKind.Clear, dry / 2);
        var dried = racks.On(RackHolder.Structure, rack.Id)[0].Item;
        Assert.Equal(Cooking.Dried, dried.Cooking);
        Assert.False(Spoilage.IsSpoiled(dried, 12 * GameClock.MinutesPerDay));

        // Sušák už není (rozebraný): maso zmizí s ním.
        racks.Update(new Fires(), new Structures(), Clock(hour: 12, day: 0), WeatherKind.Clear, 1);
        Assert.Empty(racks.On(RackHolder.Structure, rack.Id));
    }

    /// <summary>Kdo vezme maso dřív, dostane ho syrové. Bez čísla kusu se bere nejdřív hotové.</summary>
    [Fact]
    public void TakingEarlyGivesRawAndReadyComesFirst()
    {
        var (fires, fire) = BurningFire();
        var racks = new MeatRacks();
        racks.Hang(RackHolder.Fire, fire.Id, Meat(Item.RawMeat(0), 1), 0, 0);
        racks.Update(fires, new Structures(), Clock(hour: 12, day: 0), WeatherKind.Clear, GameData.Preserving.SmokeMinutes);
        racks.Hang(RackHolder.Fire, fire.Id, Meat(Item.RawFish(0), 1), 0, 0);

        var hands = new Inventory(5);
        Assert.Equal(PreserveResult.Taken, racks.Take(RackHolder.Fire, fire.Id, -1, hands));
        Assert.Equal(Cooking.Smoked, hands.Stacks[0].Item.Cooking);
        Assert.Equal(PreserveResult.Taken, racks.Take(RackHolder.Fire, fire.Id, 0, hands));
        Assert.Contains(hands.Stacks, s => s.Item.Kind == ItemKind.Fish && s.Item.Cooking == Cooking.Raw);
        Assert.Equal(PreserveResult.NothingHere, racks.Take(RackHolder.Fire, fire.Id, -1, hands));
    }

    [Fact]
    public void SmokedMeatFeedsAlmostLikeBaked()
    {
        var baked = Eat(Cooking.Baked);
        var smoked = Eat(Cooking.Smoked);
        var dried = Eat(Cooking.Dried);

        Assert.True(smoked > 0 && smoked < baked, $"uzené {smoked}, pečené {baked}");
        Assert.True(dried > 0 && dried < smoked);
    }

    [Fact]
    public void RacksAreSavedAndRestored()
    {
        var racks = new MeatRacks();
        racks.Hang(RackHolder.Fire, 3, Meat(Item.RawMeat(2), 2), 0, 2 * GameClock.MinutesPerDay);
        var restored = new MeatRacks();

        restored.Restore(racks.Export());

        Assert.Equal(2, restored.On(RackHolder.Fire, 3).Count);
        Assert.Equal(Item.RawMeat(2), restored.On(RackHolder.Fire, 3)[0].Item);
    }

    private static double Eat(Cooking cooking)
    {
        var c = new Character(1, new Mulberry32(1)) { Satiety = 20 };
        Eating.EatMeat(c, cooking, Clock(hour: 12, day: 0));
        return c.Satiety - 20;
    }

    private static Inventory Meat(Item item, int count)
    {
        var inventory = new Inventory(10);
        for (var i = 0; i < count; i++)
        {
            Assert.True(inventory.TryAdd(item));
        }

        return inventory;
    }

    private static (Fires Fires, Fire Fire) BurningFire()
    {
        var spot = Enumerable.Range(0, _island.Tiles.Length).First(i => _island.Tiles[i] == TileType.Grass
            && Movement.CanStand(_island, i % _island.Width + 0.5, i / _island.Width + 0.5, 0.6)
            && Movement.CanStand(_island, i % _island.Width + 1.5, i / _island.Width + 0.5, 0.6));
        var (x, y) = (spot % _island.Width + 0.5, spot / _island.Width + 0.5);
        var fires = new Fires();
        var materials = new Inventory(10);
        for (var i = 0; i < 3; i++)
        {
            materials.TryAdd(Item.Stick);
        }

        materials.TryAdd(Item.Stone);
        Assert.Equal(FireResult.Lit, fires.Light(_island, materials, x, y, x + 1, y));
        return (fires, fires.All[0]);
    }

    private static GameClock Clock(double hour, int day)
    {
        var clock = GameClock.FromSettings(hour * 60);
        clock.Restore(day * GameClock.MinutesPerDay + hour * 60, 4.5);
        return clock;
    }
}

using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Pochodeň (zapálení u ohně, dohořívání, déšť) a chlad v dešti bez úkrytu.</summary>
public class TorchTests
{
    private static readonly Island _island = WorldGenerator.Build(12345, 128, 128);

    private static readonly WeatherState _rain = new(WeatherKind.Rain, 1, 0.5);

    [Fact]
    public void TorchIsLitFromFireWithStick()
    {
        var (x, y) = AtCamp();
        var c = new Character(1, new Mulberry32(1));
        var inventory = new Inventory(10);

        Assert.Equal(FireResult.NoStick, Torch.Light(_island, new Fires(), c, inventory, x, y));
        inventory.TryAdd(Item.Stick);
        Assert.Equal(FireResult.NoFireNear, Torch.Light(_island, new Fires(), c, inventory, x + 30, y));
        Assert.Equal(FireResult.TorchLit, Torch.Light(_island, new Fires(), c, inventory, x, y));

        Assert.True(c.TorchLit);
        Assert.Equal(0, inventory.Count(ItemKind.Stick));
        Assert.Equal(FireResult.TorchAlreadyLit, Torch.Light(_island, new Fires(), c, inventory, x, y));
    }

    [Fact]
    public void TorchBurnsDownWithWarningAndOut()
    {
        var c = Lit();
        var total = GameData.Weather.TorchMinutes;

        Torch.Burn(c, 0, total - GameData.Weather.TorchWarnMinutes + 1, WeatherState.Calm);
        Torch.Burn(c, 1, 1, WeatherState.Calm);
        Assert.True(c.TorchLit);
        Torch.Burn(c, 2, total, WeatherState.Calm);

        Assert.False(c.TorchLit);
        var events = c.TakeMessages().Select(m => m.Event).ToList();
        Assert.Equal(1, events.Count(e => e == BodyEvent.TorchLow));
        Assert.Contains(BodyEvent.TorchOut, events);
    }

    [Fact]
    public void RainBurnsTorchFaster()
    {
        var dry = Lit();
        var wet = Lit();

        Torch.Burn(dry, 0, 60, WeatherState.Calm);
        Torch.Burn(wet, 0, 60, _rain);

        Assert.True(wet.TorchMinutes < dry.TorchMinutes);
    }

    [Fact]
    public void TorchIsSavedAndGoesOutOnDeath()
    {
        var c = Lit();
        var restored = Character.FromSave(c.ToSave());
        Assert.Equal(c.TorchMinutes, restored.TorchMinutes);

        c.Die(0);
        Assert.False(c.TorchLit);
    }

    [Fact]
    public void RainWithoutShelterIsCold()
    {
        var calm = new Character(1, new Mulberry32(1));
        var wet = new Character(1, new Mulberry32(1));
        var sheltered = new Character(1, new Mulberry32(1));

        Physiology.Run(calm, TestPlants.Clock(), 120);
        Physiology.Run(wet, TestPlants.Clock(), 120, weather: _rain);
        Physiology.Run(sheltered, TestPlants.Clock(), 120, sheltered: true, weather: _rain);

        Assert.True(wet.Warmth < calm.Warmth - 3, $"v dešti {wet.Warmth}, jasno {calm.Warmth}");
        Assert.True(sheltered.Warmth >= calm.Warmth);
    }

    private static Character Lit()
    {
        var (x, y) = AtCamp();
        var c = new Character(1, new Mulberry32(1));
        var inventory = new Inventory(10);
        inventory.TryAdd(Item.Stick);
        Assert.Equal(FireResult.TorchLit, Torch.Light(_island, new Fires(), c, inventory, x, y));
        return c;
    }

    /// <summary>U ohniště tábora cizinců (hoří pořád).</summary>
    private static (double X, double Y) AtCamp()
    {
        var camp = _island.Camps[0];
        return (camp.X + 0.5, camp.Y + 0.5);
    }
}

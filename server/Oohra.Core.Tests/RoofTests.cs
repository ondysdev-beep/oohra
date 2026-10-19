using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Sloupky, podlahy a střechy se statikou, první verze (návrh 0.8, kap. 6).</summary>
public class RoofTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    [Fact]
    public void RoofNeedsTwoSupports()
    {
        var (x, y, s) = Open();
        var sticks = Sticks();

        Assert.Equal(BuildResult.NoSupport, Build(s, sticks, BuildKind.Roof, x, y));
        s.Place(BuildKind.Post, x - 0.8, y - 0.8, 0, length: 0.01);
        Assert.Equal(BuildResult.NoSupport, Build(s, sticks, BuildKind.Roof, x, y));
        s.Place(BuildKind.StickWall, x, y + 1, 0);
        Assert.Equal(BuildResult.Built, Build(s, sticks, BuildKind.Roof, x, y));
        Assert.Equal(BuildResult.CannotBuildHere, Build(s, sticks, BuildKind.Roof, x + 0.5, y));
    }

    /// <summary>Sousední střecha, která drží, je taky podpora (podpůrný graf). Dvě střechy daleko od podpory ne.</summary>
    [Fact]
    public void NeighbourRoofCountsAsSupport()
    {
        var (x, y, s) = Open();
        var sticks = Sticks();
        s.Place(BuildKind.Post, x - 0.8, y, 0, length: 0.01);
        s.Place(BuildKind.Post, x + 0.8, y, 0, length: 0.01);
        Assert.Equal(BuildResult.Built, Build(s, sticks, BuildKind.Roof, x, y));

        // Sloupek jen pod druhou střechou (třetí střecha ho už nemá pod sebou).
        s.Place(BuildKind.Post, x + 2.5, y, 0, length: 0.01);
        Assert.Equal(BuildResult.Built, Build(s, sticks, BuildKind.Roof, x + 2, y));
        Assert.Equal(BuildResult.NoSupport, Build(s, sticks, BuildKind.Roof, x + 4, y));
    }

    /// <summary>Bez podpory střecha zapraská a za warnSeconds spadne. Když se podpora vrátí včas, drží dál.</summary>
    [Fact]
    public void RoofWithoutSupportCracksAndFalls()
    {
        var (x, y, s) = Open();
        var a = s.Place(BuildKind.Post, x - 0.8, y, 0, length: 0.01);
        s.Place(BuildKind.Post, x + 0.8, y, 0, length: 0.01);
        var roof = s.Place(BuildKind.Roof, x, y, 0, length: GameData.Building.Roof.Size);
        var rules = GameData.Building.Roof;

        Assert.Empty(s.Update(1).Cracked);
        Assert.True(Roofs.IsRoofed(s, x, y));

        s.Remove(a.Id, new Inventory(5), a.X, a.Y);
        Assert.Contains(roof, s.Update(1).Cracked);
        Assert.NotNull(roof.CrackingSince);
        Assert.False(Roofs.IsRoofed(s, x, y), "Praskající střecha před deštěm nechrání.");

        // Podpora se vrátí: střecha přestane praskat.
        var again = s.Place(BuildKind.Post, x - 0.8, y, 0, length: 0.01);
        s.Update(1);
        Assert.Null(roof.CrackingSince);

        s.Remove(again.Id, new Inventory(5), again.X, again.Y);
        s.Update(1);
        var fallen = s.Update(rules.WarnSeconds + 1).Fallen;
        Assert.Contains(roof, fallen);
        Assert.Null(s.Find(roof.Id));
    }

    /// <summary>Před deštěm chrání střecha: kdo pod ní stojí, v dešti neprochladne (zdi samy nestačí).</summary>
    [Fact]
    public void RoofKeepsRainOff()
    {
        var clock = GameClock.FromSettings(12 * 60);
        var rain = new WeatherState(WeatherKind.Rain, 1, 0.5);
        var wet = new Character(1, new Mulberry32(1));
        var dry = new Character(1, new Mulberry32(1));
        for (var i = 0; i < 300; i++)
        {
            Physiology.Update(wet, clock, 1, sheltered: true, weather: rain, roofed: false);
            Physiology.Update(dry, clock, 1, sheltered: true, weather: rain, roofed: true);
        }

        Assert.True(dry.Warmth > wet.Warmth + 3, $"pod střechou {dry.Warmth}, jen ve zdech {wet.Warmth}");
    }

    [Fact]
    public void PostBlocksButFloorDoesNot()
    {
        var (x, y, s) = Open();
        var sticks = Sticks();
        Assert.Equal(BuildResult.Built, Build(s, sticks, BuildKind.Post, x + 1, y));
        Assert.True(s.Blocks(x + 1, y, 0.28));

        Assert.Equal(BuildResult.Built, Build(s, sticks, BuildKind.Floor, x - 1.5, y));
        Assert.False(s.Blocks(x - 1.5, y, 0.28));
        Assert.Equal(BuildResult.CannotBuildHere, Build(s, sticks, BuildKind.Floor, x - 1, y));
    }

    private static BuildResult Build(Structures s, Inventory sticks, BuildKind kind, double x, double y)
    {
        return s.Build(_island, new Fires(), sticks, x, y + 1.5, kind, x, y, 0, 1, []);
    }

    /// <summary>Volné místo na trávě (9 × 9 m bez vody, stromů a skal) a prázdné stavby (ostrov se použije jen kvůli zemi).</summary>
    private static (double X, double Y, Structures S) Open()
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width, i / _island.Width);
            if (_island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-4, 9).All(dy => Enumerable.Range(-4, 9).All(dx => _island.InBounds(x + dx, y + dy)
                    && _island.TileAt(x + dx, y + dy) is TileType.Grass or TileType.Forest or TileType.Sand && !Movement.IsBlocked(_island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5, new Structures());
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }

    private static Inventory Sticks()
    {
        var inventory = new Inventory(40);
        for (var i = 0; i < 30; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        return inventory;
    }
}

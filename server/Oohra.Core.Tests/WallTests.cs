using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Zdi z klacků a úkryt (prototyp buildWall a sheltered).</summary>
public class WallTests
{
    [Fact]
    public void WallCostsTwoSticksAndBlocksWay()
    {
        var island = NewIsland();
        var (x, y) = SpotWithFreeEast(island);
        var inventory = Sticks(3);

        Assert.Equal(BuildResult.Built, island.Walls.Build(island, new Fires(), inventory, x, y, 0, []));

        Assert.True(island.Walls.Has(island.Index((int)x + 1, (int)y)));
        Assert.Equal(1, inventory.Count(ItemKind.Stick));
        Assert.True(Movement.IsBlocked(island, (int)x + 1, (int)y));

        for (var i = 0; i < 30; i++)
        {
            (x, y) = Movement.Step(island, x, y, 1, 0, 0.05);
        }

        Assert.True(x <= (int)x + 1 - 0.28 + 1e-9, $"Postava prošla zdí na x = {x:0.00}.");
    }

    [Fact]
    public void WallNeedsTwoSticks()
    {
        var island = NewIsland();
        var (x, y) = SpotWithFreeEast(island);

        Assert.Equal(BuildResult.NeedSticks, island.Walls.Build(island, new Fires(), Sticks(1), x, y, 0, []));
        Assert.Empty(island.Walls.Tiles);
    }

    [Fact]
    public void NoWallOnWallFireOrPlayer()
    {
        var island = NewIsland();
        var (x, y) = SpotWithFreeEast(island);
        var fires = new Fires();
        var inventory = Sticks(9);
        inventory.TryAdd(Item.Stone);

        // Stojí tam jiný hráč.
        Assert.Equal(BuildResult.CannotBuildHere, island.Walls.Build(island, fires, inventory, x, y, 0, [new Position(x + 1, y)]));

        // Hoří tam oheň.
        fires.Light(island, inventory, x, y, 0);
        Assert.Equal(BuildResult.CannotBuildHere, island.Walls.Build(island, fires, inventory, x, y, 0, []));

        // Už tam zeď je (na jih od postavy).
        Assert.Equal(BuildResult.Built, island.Walls.Build(island, new Fires(), inventory, x, y, Math.PI / 2, []));
        Assert.Equal(BuildResult.CannotBuildHere, island.Walls.Build(island, new Fires(), inventory, x, y, Math.PI / 2, []));
    }

    [Fact]
    public void RemovingWallGivesStickBack()
    {
        var island = NewIsland();
        var (x, y) = SpotWithFreeEast(island);
        var inventory = Sticks(2);
        island.Walls.Build(island, new Fires(), inventory, x, y, 0, []);
        var (wx, wy) = ((int)x + 1, (int)y);
        var version = island.Walls.Version;

        Assert.Equal(BuildResult.TooFar, island.Walls.Remove(island, wx, wy, inventory, x - 3, y));
        Assert.Equal(BuildResult.Removed, island.Walls.Remove(island, wx, wy, inventory, x, y));

        Assert.False(island.Walls.Has(island.Index(wx, wy)));
        Assert.Equal(1, inventory.Count(ItemKind.Stick));
        Assert.True(island.Walls.Version > version);
        Assert.Equal(BuildResult.NoWall, island.Walls.Remove(island, wx, wy, inventory, x, y));
    }

    [Fact]
    public void FourWallsAroundMakeShelter()
    {
        var island = NewIsland();
        const int x = 30, y = 30;
        Ring(island, x, y, x, y);

        Assert.True(Shelter.IsSheltered(island, x + 0.5, y + 0.5));
        Assert.False(Shelter.IsSheltered(island, x + 3.5, y + 0.5));
    }

    [Fact]
    public void OpenEnclosureIsNoShelter()
    {
        var island = NewIsland();
        Ring(island, 30, 30, 32, 32);
        Assert.True(Shelter.IsSheltered(island, 31.5, 31.5));

        island.Walls.Remove(island, 33, 31, Sticks(0), 32.5, 31.5);
        Assert.False(Shelter.IsSheltered(island, 31.5, 31.5));
    }

    /// <summary>Velká ohrada (7 × 7 = 49 dlaždic, víc než 36) není úkryt.</summary>
    [Fact]
    public void LargeEnclosureIsNoShelter()
    {
        var island = NewIsland();
        Ring(island, 30, 30, 36, 36);

        Assert.False(Shelter.IsSheltered(island, 33.5, 33.5));
    }

    [Fact]
    public void ShelterKeepsWarmerAtWinterNight()
    {
        var outside = WinterNight(sheltered: false);
        var inside = WinterNight(sheltered: true);

        Assert.True(inside.Warmth > outside.Warmth + 8, $"V úkrytu {inside.Warmth:0.0}, venku {outside.Warmth:0.0}");
        Assert.True(inside.Health >= outside.Health);
    }

    private static Character WinterNight(bool sheltered)
    {
        var clock = TestPlants.Clock();
        clock.Advance(9 * 32400 + clock.RealSecondsFor(13 * 60));
        var c = new Character(3, new Mulberry32(1)) { Satiety = 100, Hydration = 100 };
        Physiology.Run(c, clock, 12 * 60, sheltered: sheltered);
        return c;
    }

    /// <summary>Zdi kolem obdélníku dlaždic (x1, y1) až (x2, y2), vnitřek zůstane volný.</summary>
    private static void Ring(Island island, int x1, int y1, int x2, int y2)
    {
        for (var x = x1 - 1; x <= x2 + 1; x++)
        {
            island.Walls.Place(island.Index(x, y1 - 1));
            island.Walls.Place(island.Index(x, y2 + 1));
        }

        for (var y = y1; y <= y2; y++)
        {
            island.Walls.Place(island.Index(x1 - 1, y));
            island.Walls.Place(island.Index(x2 + 1, y));
        }
    }

    private static Island NewIsland()
    {
        return IslandGenerator.Build(12345, 84, 84);
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

    private static (double X, double Y) SpotWithFreeEast(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass && !Movement.IsBlocked(island, x, y) && !Movement.IsBlocked(island, x + 1, y)
                && !Movement.IsBlocked(island, x, y + 1) && Movement.CanStand(island, x + 0.5, y + 0.5, 0.28))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volná tráva.");
    }
}

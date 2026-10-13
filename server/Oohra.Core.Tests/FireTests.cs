using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Klacky, kameny, oheň, teplo u ohně a pečení (prototyp makeFire, nearFire a cook).</summary>
public class FireTests
{
    private static readonly Island _island = IslandGenerator.Build(12345, 84, 84);

    [Fact]
    public void TreeGivesSticksThreeTimesADay()
    {
        var (treeX, treeY) = TileOf(_island.Trees[0].TileIndex);
        var (x, y) = FreeNeighbour(treeX, treeY);
        var materials = new Materials();
        var inventory = new Inventory(10);
        var clock = TestPlants.Clock();
        var rng = new Mulberry32(7);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(MaterialResult.GotSticks, materials.GatherSticks(_island, treeX, treeY, clock, inventory, rng, x, y));
        }

        Assert.Equal(MaterialResult.NothingLeft, materials.GatherSticks(_island, treeX, treeY, clock, inventory, rng, x, y));
        Assert.InRange(inventory.Count(ItemKind.Stick), 3, 6);

        clock.Advance(clock.RealSecondsFor(1441));
        Assert.Equal(MaterialResult.GotSticks, materials.GatherSticks(_island, treeX, treeY, clock, inventory, rng, x, y));
    }

    [Fact]
    public void SticksOnlyFromTreeWithinReach()
    {
        var (treeX, treeY) = TileOf(_island.Trees[0].TileIndex);
        var materials = new Materials();
        var inventory = new Inventory(10);

        Assert.Equal(MaterialResult.TooFar, materials.GatherSticks(_island, treeX, treeY, TestPlants.Clock(), inventory, new Mulberry32(1), treeX + 3.5, treeY + 0.5));
        var (grassX, grassY) = FindTile(TileType.Grass);
        Assert.Equal(MaterialResult.WrongPlace, materials.GatherSticks(_island, grassX, grassY, TestPlants.Clock(), inventory, new Mulberry32(1), grassX + 0.5, grassY + 0.5));
        Assert.Empty(inventory.Stacks);
    }

    [Fact]
    public void StoneFromRock()
    {
        var (rockX, rockY) = FindTile(TileType.Rock);
        var inventory = new Inventory(10);

        Assert.Equal(MaterialResult.GotStone, Materials.TakeStone(_island, rockX, rockY, inventory, rockX + 0.5, rockY + 0.5));
        Assert.Equal(MaterialResult.GotStone, Materials.TakeStone(_island, rockX, rockY, inventory, rockX + 0.5, rockY + 0.5));

        Assert.Equal(2, inventory.Count(ItemKind.Stone));
        Assert.Single(inventory.Stacks);
        Assert.Equal(MaterialResult.TooFar, Materials.TakeStone(_island, rockX, rockY, inventory, rockX + 3, rockY));
    }

    [Fact]
    public void FireNeedsThreeSticksAndStone()
    {
        var (x, y) = SpotWithFreeEast();
        var fires = new Fires();
        var inventory = WithMaterials(sticks: 2, stones: 1);

        Assert.Equal(FireResult.NeedMaterials, fires.Light(_island, inventory, x, y, x + 1, y));

        inventory.TryAdd(Item.Stick);
        Assert.Equal(FireResult.Lit, fires.Light(_island, inventory, x, y, x + 1, y));

        var fire = Assert.Single(fires.All);
        Assert.Equal((x + 1, y), (fire.X, fire.Y));
        Assert.True(fire.Burning);
        Assert.Empty(inventory.Stacks);
    }

    [Fact]
    public void NoSecondFireOnSameTile()
    {
        var (x, y) = SpotWithFreeEast();
        var fires = new Fires();
        var inventory = WithMaterials(sticks: 6, stones: 2);

        fires.Light(_island, inventory, x, y, x + 1, y);

        Assert.Equal(FireResult.CannotBuildHere, fires.Light(_island, inventory, x, y, x + 1, y));
        Assert.Equal(3, inventory.Count(ItemKind.Stick));
    }

    [Fact]
    public void FireCannotBeBuiltIntoWater()
    {
        var (seaX, seaY) = FindTile(TileType.Sea);
        var fires = new Fires();

        // Postava stojí západně od moře a dívá se na východ, do vody.
        Assert.Equal(FireResult.CannotBuildHere, fires.Light(_island, WithMaterials(3, 1), seaX - 0.5, seaY + 0.5, seaX + 0.5, seaY + 0.5));
    }

    [Fact]
    public void FireBurnsOutAndStickRelightsIt()
    {
        var (x, y) = SpotWithFreeEast();
        var fires = new Fires();
        var inventory = WithMaterials(sticks: 5, stones: 1);
        fires.Light(_island, inventory, x, y, x + 1, y);
        var fire = fires.All[0];
        var version = fires.Version;

        Assert.Equal(FireResult.Fed, fires.Feed(fire.Id, inventory, x, y));
        Assert.Equal(720 + 300, fire.BurnMinutes);

        fires.Burn(1020);
        Assert.False(fire.Burning);
        Assert.True(fires.Version > version);

        Assert.Equal(FireResult.Relit, fires.Feed(fire.Id, inventory, x, y));
        Assert.Equal(300, fire.BurnMinutes);
        Assert.Equal(FireResult.NoStick, fires.Feed(fire.Id, inventory, x, y));
        Assert.Equal(FireResult.TooFar, fires.Feed(fire.Id, WithMaterials(1, 0), x + 5, y));
    }

    /// <summary>Oheň se staví v režimu stavby nejdál 1,2 m od postavy, kamkoli (nejen doprostřed dlaždice).</summary>
    [Fact]
    public void FireIsPlacedFreelyWithinReach()
    {
        var (x, y) = SpotWithFreeEast();
        var fires = new Fires();

        Assert.Equal(FireResult.TooFar, fires.Light(_island, WithMaterials(3, 1), x, y, x + 1.3, y));
        Assert.Equal(FireResult.Lit, fires.Light(_island, WithMaterials(3, 1), x, y, x + 0.83, y + 0.27));

        Assert.Equal((x + 0.83, y + 0.27), (fires.All[0].X, fires.All[0].Y));
    }

    [Fact]
    public void FireNotOnWall()
    {
        var island = IslandGenerator.Build(12345, 84, 84);
        var (x, y) = SpotWithFreeEast();
        island.Structures.Place(x + 1, y, Math.PI / 2);

        Assert.Equal(FireResult.CannotBuildHere, new Fires().Light(island, WithMaterials(3, 1), x, y, x + 1, y));
    }

    [Fact]
    public void FireWarmsOnlyNearbyAndCampHearthsAlwaysWarm()
    {
        var (x, y) = SpotWithFreeEast();
        var fires = new Fires();
        fires.Light(_island, WithMaterials(3, 1), x, y, x + 1, y);

        Assert.True(fires.WarmsAt(_island, x, y));
        Assert.False(fires.WarmsAt(_island, x + 4, y));

        fires.Burn(720);
        Assert.False(fires.WarmsAt(_island, x, y));

        var camp = _island.Camps[0];
        Assert.True(fires.WarmsAt(_island, camp.X + 0.5, camp.Y + 1.5));
    }

    /// <summary>Zimní noc bez ohně studí a bere zdraví, u ohně je postavě teplo.</summary>
    [Fact]
    public void WinterNightByFireKeepsWarm()
    {
        var cold = WinterNight(nearFire: false);
        var warm = WinterNight(nearFire: true);

        Assert.True(cold.Warmth < 18, $"Bez ohně teplo {cold.Warmth:0.0}");
        Assert.True(warm.Warmth > 25, $"U ohně teplo {warm.Warmth:0.0}");
        Assert.True(warm.Health > cold.Health);
        Assert.Equal(100, warm.Health, 1);
    }

    [Fact]
    public void BakingNeedsBurningFire()
    {
        var (x, y) = SpotWithFreeEast();
        var fires = new Fires();
        var inventory = new Inventory(10);
        inventory.TryAdd(TestPlants.Item(PlantPart.Root));
        inventory.TryAdd(TestPlants.Item(PlantPart.Root));
        inventory.TryAdd(Item.Stone);
        for (var i = 0; i < 3; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        Assert.Equal(FireResult.NoFireNear, fires.Bake(_island, inventory, 0, x, y));

        fires.Light(_island, inventory, x, y, x + 1, y);
        Assert.Equal(FireResult.Baked, fires.Bake(_island, inventory, 0, x, y));

        Assert.Equal(2, inventory.Stacks.Count);
        Assert.Equal(Cooking.Raw, inventory.Stacks[0].Item.Plant!.Cooking);
        Assert.Equal(Cooking.Baked, inventory.Stacks[1].Item.Plant!.Cooking);
        Assert.Equal(FireResult.CannotBake, fires.Bake(_island, inventory, 1, x, y));
    }

    [Fact]
    public void SticksAndStonesCannotBeBaked()
    {
        var (x, y) = SpotWithFreeEast();
        var fires = new Fires();
        var inventory = WithMaterials(4, 1);
        fires.Light(_island, inventory, x, y, x + 1, y);

        Assert.Equal(FireResult.CannotBake, fires.Bake(_island, inventory, 0, x, y));
        Assert.Equal(FireResult.CannotBake, fires.Bake(_island, inventory, 7, x, y));
    }

    [Fact]
    public void InventoryCountsAndRemovesMaterial()
    {
        var inventory = WithMaterials(sticks: 11, stones: 1);

        Assert.Equal(3, inventory.Stacks.Count);
        Assert.False(inventory.TryRemove(ItemKind.Stone, 2));
        Assert.True(inventory.TryRemove(ItemKind.Stick, 10));
        Assert.Equal(1, inventory.Count(ItemKind.Stick));
        Assert.Equal(1, inventory.Count(ItemKind.Stone));
    }

    private static Character WinterNight(bool nearFire)
    {
        // Testovací hodiny: měsíc 32 400 s, den 5 400 s, začínají v 7:00. Devět měsíců je přesně 54 dní,
        // takže po nich je zase 7:00. Přidáme 13 hodin a je 20:00 prvního zimního dne.
        var clock = TestPlants.Clock();
        clock.Advance(9 * 32400 + clock.RealSecondsFor(13 * 60));
        Assert.Equal(Season.Winter, clock.Season);
        Assert.Equal(20, clock.Hour, 3);
        var c = new Character(3, new Mulberry32(1)) { Satiety = 100, Hydration = 100 };

        Physiology.Run(c, clock, 12 * 60, nearFire: nearFire);
        return c;
    }

    private static Inventory WithMaterials(int sticks, int stones)
    {
        var inventory = new Inventory(10);
        for (var i = 0; i < sticks; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        for (var i = 0; i < stones; i++)
        {
            inventory.TryAdd(Item.Stone);
        }

        return inventory;
    }

    private static (int X, int Y) TileOf(int index)
    {
        return (index % _island.Width, index / _island.Width);
    }

    private static (int X, int Y) FindTile(TileType type)
    {
        var index = Array.FindIndex(_island.Tiles, t => t == type);
        return TileOf(index);
    }

    private static (double X, double Y) FreeNeighbour(int tileX, int tileY)
    {
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            if (!Movement.IsBlocked(_island, tileX + dx, tileY + dy))
            {
                return (tileX + dx + 0.5, tileY + dy + 0.5);
            }
        }

        throw new InvalidOperationException("Strom nemá volného souseda.");
    }

    /// <summary>Střed travnaté dlaždice, východně od které je volno (tam se rozdělá oheň).</summary>
    private static (double X, double Y) SpotWithFreeEast()
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = TileOf(i);
            if (_island.Tiles[i] == TileType.Grass && !Movement.IsBlocked(_island, x, y) && !Movement.IsBlocked(_island, x + 1, y))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volná tráva.");
    }
}

using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Střechy z materiálů (zatékání, oprava) a oheň ve stavbě s kamenným kruhem (návrh 0.8, kap. 6).</summary>
public class RoofMaterialAndFireTests
{
    /// <summary>Střecha z kůry stojí lýko, po svých dnech zatéká (před deštěm nechrání) a oprava ji vrátí.</summary>
    [Fact]
    public void BarkRoofLeaksAfterDaysAndRepairHelps()
    {
        var (world, x, y) = Open();
        var s = world.Structures;
        s.Place(BuildKind.Post, x - 0.8, y - 0.8, 0, length: 0.01);
        s.Place(BuildKind.Post, x + 0.8, y + 0.8, 0, length: 0.01);
        var bag = Items((ItemKind.Stick, 10), (ItemKind.Bast, 2));
        s.SetTime(1000);

        Assert.Equal(BuildResult.NeedMaterials, Build(world, bag, x, y, "Bark"));
        bag.TryAdd(Item.Bast);
        Assert.Equal(BuildResult.Built, Build(world, bag, x, y, "Bark"));
        var roof = s.All.Single(t => t.Kind == BuildKind.Roof);
        Assert.Equal(RoofMaterial.Bark, roof.Material);
        Assert.Equal(0, bag.Count(ItemKind.Bast));

        var days = Structures.RoofMaterialOf("Bark").Days;
        var later = 1000 + (days + 1) * 1440;
        Assert.True(Roofs.IsRoofed(s, x, y, 0, 1000 + 1440));
        Assert.True(Structures.Leaking(roof, later));
        Assert.False(Roofs.IsRoofed(s, x, y, 0, later), "Střecha, která zatéká, před deštěm nechrání.");

        Assert.Equal(BuildResult.NeedMaterials, s.Repair(roof.Id, bag, x, y + 1, -1, null, later));
        bag.TryAdd(Item.Bast);
        bag.TryAdd(Item.Bast);
        Assert.Equal(BuildResult.Repaired, s.Repair(roof.Id, bag, x, y + 1, -1, null, later));
        Assert.False(Structures.Leaking(roof, later + 10));
        Assert.Equal(0, bag.Count(ItemKind.Bast));
    }

    /// <summary>Střecha z větví je jen z klacků (výchozí materiál).</summary>
    [Fact]
    public void BranchRoofIsJustSticks()
    {
        var rules = Structures.RoofMaterialOf(null);
        Assert.Equal(RoofMaterial.Branches, rules.Material);
        Assert.True(Structures.RoofMaterialOf("Shingles").Days > Structures.RoofMaterialOf("Hide").Days);
        Assert.True(Structures.RoofMaterialOf("Hide").Days > rules.Days);
    }

    /// <summary>Ohniště u dřevěné zdi ji zapálí, oheň přeskočí na sousední zeď a zdi shoří.</summary>
    [Fact]
    public void FireNextToWallSetsItAblazeAndSpreads()
    {
        var (world, x, y) = Open();
        var s = world.Structures;
        var wall = s.Place(BuildKind.StickWall, x, y - 0.8, 0);
        var next = s.Place(BuildKind.StickWall, x + 1, y - 0.8, 0);
        var fires = Fire(world, x, y);

        var rng = new Mulberry32(3);
        var minute = 0;
        for (; minute < 2000 && wall.BurningSince is null; minute++)
        {
            BuildingFire.Update(world, fires, 1, WeatherKind.Clear, rng, minute);
        }

        Assert.NotNull(wall.BurningSince);
        var c = new Character(1, new Mulberry32(1));
        BuildingFire.Heat(s, c, x, y - 0.4, 0, 1, minute);
        Assert.True(c.Health < 100);
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.BuildingOnFire);

        Burn(world, fires, 600, minute);
        Assert.Null(s.Find(wall.Id));
        Assert.Null(s.Find(next.Id));
    }

    /// <summary>Ohniště uvnitř kruhu z kamenů nic nezapálí. Kruh jde postavit i kolem ohně, který už hoří.</summary>
    [Fact]
    public void StoneRingKeepsFireInside()
    {
        var (world, x, y) = Open();
        var s = world.Structures;
        var wall = s.Place(BuildKind.StickWall, x, y - 0.8, 0);
        var fires = Fire(world, x, y);
        var stones = Items((ItemKind.Stone, 4));

        Assert.Equal(BuildResult.Built, s.Build(world, fires, stones, x, y + 1.5, BuildKind.Decoration, x, y, 0, 1, [], decorationId: GameData.Building.BuildingFire.RingDecoration));
        Assert.True(BuildingFire.InRing(s, x, y));

        Burn(world, fires, 1200, 0);
        Assert.Null(wall.BurningSince);
    }

    /// <summary>V dešti chytá oheň méně: za stejnou dobu vzplane méně staveb.</summary>
    [Fact]
    public void RainSlowsIgnition()
    {
        var dry = Ignited(WeatherKind.Clear);
        var wet = Ignited(WeatherKind.Rain);
        Assert.True(wet < dry, $"za sucha {dry}, v dešti {wet}");
    }

    private static int Ignited(WeatherKind weather)
    {
        var count = 0;
        var (world, x, y) = Open();
        for (var seed = 1; seed <= 40; seed++)
        {
            var wall = world.Structures.Place(BuildKind.StickWall, x, y - 0.8, 0);
            var fires = Fire(world, x, y);
            var rng = new Mulberry32(seed);
            for (var minute = 0; minute < 60; minute++)
            {
                BuildingFire.Update(world, fires, 1, weather, rng, minute);
            }

            count += wall.BurningSince is null ? 0 : 1;
            world.Structures.Remove(wall.Id, new Inventory(10), x, y - 0.8);
        }

        return count;
    }

    private static void Burn(Island world, Fires fires, int minutes, double from)
    {
        var rng = new Mulberry32(3);
        for (var minute = 0; minute < minutes; minute++)
        {
            fires.All[0].BurnMinutes = 100;
            BuildingFire.Update(world, fires, 1, WeatherKind.Clear, rng, from + minute);
        }
    }

    private static Fires Fire(Island world, double x, double y)
    {
        var fires = new Fires();
        Assert.Equal(FireResult.Lit, fires.Light(world, Items((ItemKind.Stick, 3), (ItemKind.Stone, 1)), x, y + 1, x, y));
        return fires;
    }

    private static BuildResult Build(Island world, Inventory bag, double x, double y, string material)
    {
        return world.Structures.Build(world, new Fires(), bag, x, y + 1.5, BuildKind.Roof, x, y, 0, 1, [], decorationId: material);
    }

    private static Inventory Items(params (ItemKind Kind, int Count)[] items)
    {
        var inventory = new Inventory(40);
        foreach (var (kind, count) in items)
        {
            for (var i = 0; i < count; i++)
            {
                inventory.TryAdd(new Item(kind));
            }
        }

        return inventory;
    }

    private static (Island World, double X, double Y) Open()
    {
        var world = WorldGenerator.Build(1, 128, 128);
        for (var i = 0; i < world.Tiles.Length; i++)
        {
            var (x, y) = (i % world.Width, i / world.Width);
            if (world.Tiles[i] == TileType.Grass
                && Enumerable.Range(-4, 9).All(dy => Enumerable.Range(-4, 9).All(dx => world.InBounds(x + dx, y + dy)
                    && world.TileAt(x + dx, y + dy) is TileType.Grass or TileType.Forest or TileType.Sand && !Movement.IsBlocked(world, x + dx, y + dy))))
            {
                return (world, x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

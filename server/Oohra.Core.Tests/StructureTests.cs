using Oohra.Core.Data;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Režim stavby: zdi jako tenké příčky s libovolnou polohou a úhlem, a úkryt.</summary>
public class StructureTests
{
    [Fact]
    public void WallAtAnyAngleCostsTwoSticks()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        var inventory = Sticks(3);
        var angle = 15 * Math.PI / 180;

        Assert.Equal(BuildResult.Built, Build(island, inventory, x, y, x + 0.9, y + 0.3, angle));

        var wall = Assert.Single(island.Structures.All);
        Assert.Equal((x + 0.9, y + 0.3, angle), (wall.X, wall.Y, wall.Angle));
        Assert.Equal(1, inventory.Count(ItemKind.Stick));
    }

    [Fact]
    public void WallNeedsSticksAndReach()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);

        Assert.Equal(BuildResult.NeedMaterials, Build(island, Sticks(1), x, y, x + 1, y, 0));
        Assert.Equal(BuildResult.TooFar, Build(island, Sticks(2), x, y, x + GameData.Building.PlaceReach + 0.1, y, 0));
        Assert.Empty(island.Structures.All);
    }

    [Fact]
    public void NoWallOverWaterTrunkFireOrPlayer()
    {
        var island = NewIsland();
        var (seaX, seaY) = FindTile(island, TileType.Sea);
        Assert.Equal(BuildResult.CannotBuildHere, Build(island, Sticks(2), seaX - 0.5, seaY + 0.5, seaX + 0.2, seaY + 0.5, Math.PI / 2));

        var tree = island.Trees.First(t => Movement.CanStand(island, t.X - 1, t.Y, 0.28));
        Assert.Equal(BuildResult.CannotBuildHere, Build(island, Sticks(2), tree.X - 1, tree.Y, tree.X, tree.Y, Math.PI / 2));

        var (x, y) = OpenSpot(island);
        var fires = new Fires();
        var inventory = Sticks(5);
        inventory.TryAdd(Item.Stone);
        fires.Light(island, inventory, x, y, x + 1, y);
        Assert.Equal(BuildResult.CannotBuildHere, island.Structures.Build(island, fires, inventory, x, y, BuildKind.StickWall, x + 1, y, Math.PI / 2, 1.0, []));

        Assert.Equal(BuildResult.CannotBuildHere,
            island.Structures.Build(island, new Fires(), Sticks(2), x, y, BuildKind.StickWall, x, y + 1, 0, 1.0, [new Position(x, y + 1.2)]));
        Assert.Empty(island.Structures.All);
    }

    /// <summary>Zeď blokuje jen tam, kde stojí: přímo do ní se nejde, kolem jejího konce ano.</summary>
    [Fact]
    public void WallBlocksOnlyWhereItStands()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        island.Structures.Place(x + 1, y, Math.PI / 2);

        var (ax, ay) = (x, y);
        for (var i = 0; i < 30; i++)
        {
            (ax, ay) = Movement.Step(island, ax, ay, 1, 0, 0.05);
        }

        Assert.True(ax < x + 1 - 0.28 - 0.05 + 1e-9, $"Postava prošla zdí na x = {ax:0.00}.");

        // Za koncem zdi (zeď má 2 m, konec je metr od středu) se projde.
        var (bx, by) = (x, y + 1.5);
        for (var i = 0; i < 30; i++)
        {
            (bx, by) = Movement.Step(island, bx, by, 1, 0, 0.05);
        }

        Assert.True(bx > x + 1.3);
    }

    [Fact]
    public void RemovingWallGivesStickBack()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        var inventory = Sticks(2);
        Build(island, inventory, x, y, x + 1, y, 0.4);
        var id = island.Structures.All.Single().Id;
        var version = island.Structures.Version;

        Assert.Equal(BuildResult.TooFar, island.Structures.Remove(id, inventory, x - 3, y));
        Assert.Equal(BuildResult.Removed, island.Structures.Remove(id, inventory, x, y));

        Assert.Empty(island.Structures.All);
        Assert.Equal(1, inventory.Count(ItemKind.Stick));
        Assert.True(island.Structures.Version > version);
        Assert.Equal(BuildResult.NotFound, island.Structures.Remove(id, inventory, x, y));
        Assert.True(Movement.CanStand(island, x + 1, y, 0.28));
    }

    [Fact]
    public void FourWallsAroundMakeShelter()
    {
        var island = NewIsland();
        var walls = Box(island, 30.5, 30.5, 1.0);

        Assert.True(Shelter.IsSheltered(island, 30.5, 30.5));
        Assert.False(Shelter.IsSheltered(island, 33.5, 30.5));

        island.Structures.Remove(walls[0].Id, Sticks(0), 30.5, 30.5);
        Assert.False(Shelter.IsSheltered(island, 30.5, 30.5));
    }

    /// <summary>Šikmé zdi (kosočtverec) taky dělají úkryt.</summary>
    [Fact]
    public void RotatedWallsMakeShelter()
    {
        var island = NewIsland();
        var d = 0.5 / Math.Sqrt(2);
        island.Structures.Place(30.5 + d, 30.5 - d, Math.PI / 4 + Math.PI / 2);
        island.Structures.Place(30.5 + d, 30.5 + d, Math.PI / 4);
        island.Structures.Place(30.5 - d, 30.5 + d, Math.PI / 4 + Math.PI / 2);
        island.Structures.Place(30.5 - d, 30.5 - d, Math.PI / 4);

        Assert.True(Shelter.IsSheltered(island, 30.5, 30.5));
    }

    /// <summary>Velká ohrada (stěny dál než 3,5 m) ani ohrada s mezerami není úkryt.</summary>
    [Fact]
    public void LargeOrGappyEnclosureIsNoShelter()
    {
        var island = NewIsland();
        Box(island, 40.5, 40.5, 8.0);
        Assert.False(Shelter.IsSheltered(island, 40.5, 40.5));

        var gappy = NewIsland();
        gappy.Structures.Place(30.5, 29.5, 0);
        gappy.Structures.Place(30.5, 31.5, 0);
        Assert.False(Shelter.IsSheltered(gappy, 30.5, 30.5));
    }

    [Fact]
    public void ShelterKeepsWarmerAtWinterNight()
    {
        var outside = WinterNight(sheltered: false);
        var inside = WinterNight(sheltered: true);

        Assert.True(inside.Warmth > outside.Warmth + 8, $"V úkrytu {inside.Warmth:0.0}, venku {outside.Warmth:0.0}");
        Assert.True(inside.Health >= outside.Health);
    }

    /// <summary>Krátká zeď (poloviční, nebo dorovnání mezery) stojí 1 klacek a blokuje jen svou délku.</summary>
    [Fact]
    public void ShortWallIsCheaperAndShorter()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        var inventory = Sticks(2);

        Assert.Equal(BuildResult.Built, Build(island, inventory, x, y, x + 1, y, Math.PI / 2, length: 0.5));

        Assert.Equal(1, inventory.Count(ItemKind.Stick));
        Assert.Equal(0.5, island.Structures.All.Single().Length, 9);
        Assert.False(Movement.CanStand(island, x + 1, y, 0.28));
        Assert.True(Movement.CanStand(island, x + 1, y + 0.62, 0.28));

        // Rozebráním se vrátí polovina ceny (dolů): z 1 klacku nic.
        island.Structures.Remove(island.Structures.All.Single().Id, inventory, x, y);
        Assert.Equal(1, inventory.Count(ItemKind.Stick));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(2.2)]
    public void WallLengthMustBeInRange(double length)
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);

        Assert.Equal(BuildResult.CannotBuildHere, Build(island, Sticks(9), x, y, x + 1, y, 0, length: length));
    }

    [Fact]
    public void DoorFrameIsAlwaysFullLength()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);

        Build(island, Sticks(3), x, y, x + 1.5, y, Math.PI / 2, BuildKind.DoorFrame, length: 0.4);

        Assert.Equal(GameData.Building.Wall.Length, island.Structures.All.Single().Length, 9);
    }

    /// <summary>Zeď s otvorem blokuje jen sloupky po krajích, otvorem se projde.</summary>
    [Fact]
    public void DoorFrameBlocksOnlyPosts()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        Assert.Equal(BuildResult.Built, Build(island, Sticks(3), x, y, x + 1, y, Math.PI / 2, BuildKind.DoorFrame));

        Assert.True(Movement.CanStand(island, x + 1, y, 0.28));
        Assert.False(Movement.CanStand(island, x + 1, y + 0.45, 0.28));

        var (ax, ay) = (x, y);
        for (var i = 0; i < 40; i++)
        {
            (ax, ay) = Movement.Step(island, ax, ay, 1, 0, 0.05);
        }

        Assert.True(ax > x + 1.5, $"Postava neprošla otvorem, zůstala na x = {ax:0.00}.");
    }

    [Fact]
    public void DoorGoesIntoNearestFreeDoorway()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        Assert.Equal(BuildResult.NoDoorway, Build(island, Sticks(2), x, y, x + 1, y, 0, BuildKind.Door));

        Build(island, Sticks(3), x, y, x + 1, y, Math.PI / 2, BuildKind.DoorFrame);
        var frame = island.Structures.All.Single();
        var inventory = Sticks(4);

        Assert.Equal(BuildResult.Built, Build(island, inventory, x, y, x + 1.3, y + 0.2, 0, BuildKind.Door));

        var door = island.Structures.DoorIn(frame.Id)!;
        Assert.Equal((frame.X, frame.Y, frame.Angle), (door.X, door.Y, door.Angle));
        Assert.Equal(2, inventory.Count(ItemKind.Stick));
        Assert.Equal(BuildResult.NoDoorway, Build(island, inventory, x, y, x + 1, y, 0, BuildKind.Door));

        // Dveře neblokují: otvorem se dál projde.
        Assert.True(Movement.CanStand(island, x + 1, y, 0.28));
    }

    /// <summary>Úkryt s otvorem bez dveří není úkryt. Když se do otvoru vsadí dveře, je.</summary>
    [Fact]
    public void DoorClosesShelter()
    {
        var island = NewIsland();
        island.Structures.Place(30.5 - 0.5, 30.5, Math.PI / 2);
        island.Structures.Place(30.5, 30.5 + 0.5, 0);
        island.Structures.Place(30.5, 30.5 - 0.5, 0);
        var frame = island.Structures.Place(BuildKind.DoorFrame, 30.5 + 0.5, 30.5, Math.PI / 2);
        Assert.False(Shelter.IsSheltered(island, 30.5, 30.5));

        island.Structures.Place(BuildKind.Door, frame.X, frame.Y, frame.Angle, frame.Id);
        Assert.True(Shelter.IsSheltered(island, 30.5, 30.5));
    }

    [Fact]
    public void RemovingDoorFrameRemovesDoor()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        var frame = island.Structures.Place(BuildKind.DoorFrame, x + 1, y, Math.PI / 2);
        island.Structures.Place(BuildKind.Door, frame.X, frame.Y, frame.Angle, frame.Id);
        var inventory = Sticks(0);

        Assert.Equal(BuildResult.Removed, island.Structures.Remove(frame.Id, inventory, x, y));

        Assert.Empty(island.Structures.All);
        Assert.Equal(2, inventory.Count(ItemKind.Stick));
    }

    [Fact]
    public void SegmentGeometry()
    {
        Assert.Equal(1, Geometry.DistanceToSegment(0, 1, -1, 0, 1, 0), 9);
        Assert.Equal(Math.Sqrt(2), Geometry.DistanceToSegment(2, 1, -1, 0, 1, 0), 9);
        Assert.Equal(2, Geometry.RayHitsSegment(0, 0, 1, 0, 2, -1, 2, 1)!.Value, 9);
        Assert.Null(Geometry.RayHitsSegment(0, 0, -1, 0, 2, -1, 2, 1));
        Assert.Null(Geometry.RayHitsSegment(0, 0, 1, 0, 2, 0.5, 2, 1));
    }

    private static BuildResult Build(
        Island island, Inventory inventory, double px, double py, double x, double y, double angle,
        BuildKind kind = BuildKind.StickWall, double length = 1.0)
    {
        return island.Structures.Build(island, new Fires(), inventory, px, py, kind, x, y, angle, length, [new Position(px, py)]);
    }

    /// <summary>Čtverec ze 4 zdí se stranou size kolem bodu (cx, cy). Zdi jsou 1 m dlouhé, větší čtverec má mezery.</summary>
    private static List<Structure> Box(Island island, double cx, double cy, double size)
    {
        var h = size / 2;
        return
        [
            island.Structures.Place(cx + h, cy, Math.PI / 2),
            island.Structures.Place(cx - h, cy, Math.PI / 2),
            island.Structures.Place(cx, cy + h, 0),
            island.Structures.Place(cx, cy - h, 0),
        ];
    }

    private static Character WinterNight(bool sheltered)
    {
        var clock = TestPlants.Clock();
        clock.Advance(9 * 32400 + clock.RealSecondsFor(13 * 60));
        var c = new Character(3, new Mulberry32(1)) { Satiety = 100, Hydration = 100 };
        Physiology.Run(c, clock, 12 * 60, sheltered: sheltered);
        return c;
    }

    private static Island NewIsland()
    {
        return WorldGenerator.Build(12345, 128, 128);
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

    private static (int X, int Y) FindTile(Island island, TileType type)
    {
        var index = Array.FindIndex(island.Tiles, t => t == type);
        return (index % island.Width, index / island.Width);
    }

    /// <summary>Střed travnaté dlaždice, kolem které je ve čtverci 5 × 5 sucho a žádný strom.</summary>
    private static (double X, double Y) OpenSpot(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass && Enumerable.Range(-2, 5).All(dy => Enumerable.Range(-2, 5).All(dx => !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není volné místo.");
    }
}

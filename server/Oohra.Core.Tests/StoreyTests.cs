using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Druhé podlaží (návrh 0.8, kap. 6): podlaha patra, stavby v patře, žebřík, pád.</summary>
public class StoreyTests
{
    /// <summary>Podlaha patra drží jako střecha (dvě podpory). Na stejném místě nemůže být i střecha přízemí.</summary>
    [Fact]
    public void UpperFloorNeedsSupportsLikeRoof()
    {
        var (world, x, y) = Open();
        var s = world.Structures;
        var sticks = Sticks();

        Assert.Equal(BuildResult.NoSupport, Build(world, sticks, BuildKind.Floor, x, y, level: 1));
        Posts(s, x, y);
        Assert.Equal(BuildResult.Built, Build(world, sticks, BuildKind.Floor, x, y, level: 1));
        Assert.Equal(BuildResult.CannotBuildHere, Build(world, sticks, BuildKind.Roof, x, y));
        Assert.Equal(1, s.All.Single(t => t.Kind == BuildKind.Floor).Level);
        Assert.True(Roofs.IsRoofed(s, x, y), "Podlaha patra je strop přízemí.");
        Assert.False(Roofs.IsRoofed(s, x, y, level: 1), "V patře bez střechy prší.");
    }

    /// <summary>V patře se staví jen na podlahu. Zeď v patře překáží jen v patře, zeď dole jen dole.</summary>
    [Fact]
    public void UpperWallsStandOnFloorAndBlockOnlyUpstairs()
    {
        var (world, x, y) = UpperFloor();
        var s = world.Structures;
        var sticks = Sticks();

        Assert.Equal(BuildResult.Built, Build(world, sticks, BuildKind.StickWall, x, y + 0.5, level: 1, playerLevel: 1));
        Assert.Equal(BuildResult.NoFloor, Build(world, sticks, BuildKind.StickWall, x + 4, y, level: 1, playerLevel: 1));
        Assert.True(s.Blocks(x, y + 0.5, 0.28, level: 1));
        Assert.False(s.Blocks(x, y + 0.5, 0.28));

        Assert.True(Movement.CanStand(world, x, y - 0.5, 0.28, level: 1));
        Assert.False(Movement.CanStand(world, x + 3, y, 0.28, level: 1), "Mimo podlahu patra se nahoře stát nedá.");
        Assert.False(Movement.CanStand(world, x, y + 0.5, 0.28, level: 1), "Zeď v patře překáží.");
    }

    /// <summary>Žebřík stojí u podlahy patra. Po něm se leze nahoru na podlahu a dolů k jeho patě.</summary>
    [Fact]
    public void LadderLeadsUpAndDown()
    {
        var (world, x, y) = UpperFloor();
        var sticks = Sticks();
        var foot = (X: x + 1.4, Y: y);

        Assert.Equal(BuildResult.NoFloor, Build(world, sticks, BuildKind.Ladder, x + 4, y));
        Assert.Equal(BuildResult.Built, Build(world, sticks, BuildKind.Ladder, foot.X, foot.Y));
        var ladder = world.Structures.All.Single(t => t.Kind == BuildKind.Ladder);

        var (up, level, top) = Storeys.Climb(world, ladder.Id, 0, foot.X + 0.3, foot.Y);
        Assert.Equal(ClimbResult.Up, up);
        Assert.Equal(1, level);
        Assert.True(Movement.CanStand(world, top.X, top.Y, GameData.Movement.Radius, 1));

        var (down, ground, bottom) = Storeys.Climb(world, ladder.Id, 1, top.X, top.Y);
        Assert.Equal(ClimbResult.Down, down);
        Assert.Equal(0, ground);
        Assert.Equal((ladder.X, ladder.Y), (bottom.X, bottom.Y));
        Assert.Equal(ClimbResult.TooFar, Storeys.Climb(world, ladder.Id, 0, foot.X + 5, foot.Y).Result);
    }

    /// <summary>Střecha patra leží na zdech nebo sloupcích v patře.</summary>
    [Fact]
    public void UpperRoofRestsOnUpperPosts()
    {
        var (world, x, y) = UpperFloor();
        var s = world.Structures;
        var sticks = Sticks();

        Assert.Equal(BuildResult.NoSupport, Build(world, sticks, BuildKind.Roof, x, y, level: 1, playerLevel: 1));
        s.Place(BuildKind.Post, x - 0.8, y - 0.8, 0, length: 0.01, level: 1);
        s.Place(BuildKind.Post, x + 0.8, y + 0.8, 0, length: 0.01, level: 1);
        Assert.Equal(BuildResult.Built, Build(world, sticks, BuildKind.Roof, x, y, level: 1, playerLevel: 1));
        Assert.True(Roofs.IsRoofed(s, x, y, level: 1));
    }

    /// <summary>Když se podlaha patra zřítí, spadne i všechno, co na ní stálo.</summary>
    [Fact]
    public void FallingFloorTakesUpperWallsWithIt()
    {
        var (world, x, y) = UpperFloor();
        var s = world.Structures;
        var wall = s.Place(BuildKind.StickWall, x, y + 0.5, 0, level: 1);
        var post = s.All.First(t => t.Kind == BuildKind.Post);
        s.Update(1);

        s.Remove(post.Id, new Inventory(5), post.X, post.Y);
        var cracked = s.Update(1).Cracked;
        Assert.Contains(cracked, t => t.Kind == BuildKind.Floor);
        Assert.Contains(wall, cracked);

        var fallen = s.Update(GameData.Building.Roof.WarnSeconds + 1).Fallen;
        Assert.Contains(wall, fallen);
        Assert.DoesNotContain(s.All, t => t.Level > 0);
    }

    [Fact]
    public void JumpingDownHurtsALittleFallingMore()
    {
        var jumper = new Character(1, new Mulberry32(1));
        var faller = new Character(1, new Mulberry32(1));
        Storeys.JumpDown(jumper, 0);
        Storeys.Fall(faller, 0);

        Assert.True(jumper.Health < 100);
        Assert.True(faller.Health < jumper.Health);
        Assert.Contains(jumper.TakeMessages(), m => m.Event == BodyEvent.JumpedDown);
    }

    [Fact]
    public void LevelIsSaved()
    {
        var (world, x, y) = UpperFloor();
        var copy = new Structures();
        copy.Restore(world.Structures.Export());

        Assert.Equal(1, copy.All.Single(t => t.Kind == BuildKind.Floor).Level);
    }

    private static BuildResult Build(Island world, Inventory sticks, BuildKind kind, double x, double y, int level = 0, int playerLevel = 0)
    {
        return world.Structures.Build(world, new Fires(), sticks, x, y + (playerLevel > 0 ? 0 : 1.5), kind, x, y, 0, 1, [], level: level);
    }

    /// <summary>Volné místo a podlaha patra na dvou sloupcích nad ním.</summary>
    private static (Island World, double X, double Y) UpperFloor()
    {
        var (world, x, y) = Open();
        Posts(world.Structures, x, y);
        world.Structures.Place(BuildKind.Floor, x, y, 0, length: GameData.Building.Floor.Size, level: 1);
        return (world, x, y);
    }

    private static void Posts(Structures s, double x, double y)
    {
        s.Place(BuildKind.Post, x - 0.8, y - 0.8, 0, length: 0.01);
        s.Place(BuildKind.Post, x + 0.8, y + 0.8, 0, length: 0.01);
    }

    /// <summary>Nový ostrov (vlastní stavby) a volné místo na trávě 9 × 9 m.</summary>
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

using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Nosnost (statika, druhý krok): váha jde shora dolů, přetížený sloupek nebo zeď neunese.</summary>
public class LoadTests
{
    [Fact]
    public void RoofWeightDependsOnMaterial()
    {
        var weights = GameData.Building.Load.RoofWeights;
        var branches = new Structure(2, BuildKind.Roof, 0, 0, 0, 2);
        Assert.True(weights[RoofMaterial.Shingles] > weights[RoofMaterial.Branches]);
        Assert.Equal(weights[RoofMaterial.Branches], Roofs.Weight(branches));
        Assert.Equal(2 * Roofs.Weight(new Structure(3, BuildKind.StickWall, 0, 0, 0, 1)), Roofs.Weight(new Structure(4, BuildKind.StickWall, 0, 0, 0, 2)), 6);
    }

    /// <summary>Podlaha patra na dvou sloupcích unese jen tolik zdí, kolik sloupky snesou. Pak „moc těžké“.</summary>
    [Fact]
    public void TooManyWallsUpstairsAreTooHeavy()
    {
        var (world, x, y) = UpperFloor();
        var sticks = Sticks();
        var built = 0;
        BuildResult result;
        do
        {
            result = world.Structures.Build(world, new Fires(), sticks, x, y, BuildKind.StickWall, x, y - 0.8 + built * 0.25, 0, 1, [], level: 1);
            built += result == BuildResult.Built ? 1 : 0;
        }
        while (result == BuildResult.Built && built < 20);

        Assert.Equal(BuildResult.TooHeavy, result);
        var capacity = 2 * GameData.Building.Load.PostCapacity;
        Assert.InRange(built, (int)Math.Floor(capacity - 1.5) - 1, (int)Math.Ceiling(capacity - 1.5));
        Assert.All(world.Structures.All, s => Assert.Contains(s.Id, Roofs.Stable(world.Structures)));
    }

    /// <summary>Lidé v patře váží: když se jich na slabou podlahu nahrne moc, sloupky povolí a podlaha praská.</summary>
    [Fact]
    public void CrowdOnWeakFloorCracksIt()
    {
        var (world, x, y) = UpperFloor();
        var s = world.Structures;
        var few = new List<(double X, double Y, int Level)> { (x, y, 1), (x + 0.3, y, 1) };
        var crowd = Enumerable.Range(0, 8).Select(i => (x + (i % 3 - 1) * 0.4, y + (i / 3 - 1) * 0.4, 1)).ToList();
        var floor = s.All.Single(t => t.Kind == BuildKind.Floor);

        Assert.Contains(floor.Id, Roofs.Stable(s, null, few));
        Assert.DoesNotContain(floor.Id, Roofs.Stable(s, null, crowd));

        s.Update(1, crowd);
        s.Update(1, crowd);
        Assert.NotNull(floor.CrackingSince);
        s.Update(1, few);
        Assert.Null(floor.CrackingSince);
    }

    /// <summary>Kdo má málo vlastních podpor, opře se o sousední desku: její sloupky nesou víc.</summary>
    [Fact]
    public void NeighbourDeckCarriesPartOfTheLoad()
    {
        var (world, x, y) = Open();
        var s = world.Structures;
        var a = s.Place(BuildKind.Post, x - 0.8, y, 0, length: 0.01);
        // Sloupek b je jen pod první střechou (druhá začíná až za ním).
        var b = s.Place(BuildKind.Post, x + 0.5, y, 0, length: 0.01);
        s.Place(BuildKind.Roof, x, y, 0, length: 2);
        var lonely = s.Place(BuildKind.Post, x + 2.5, y, 0, length: 0.01);
        s.Place(BuildKind.Roof, x + 2, y, 0, length: 2);

        var stable = Roofs.Stable(s);
        var carried = Roofs.Carried(s.All.ToList(), stable, []);
        Assert.True(carried[a.Id] > 0.5, $"sloupek a nese {carried[a.Id]}");
        Assert.True(carried[lonely.Id] > 0);
        Assert.True(carried[a.Id] + carried[b.Id] > carried[lonely.Id]);
    }

    private static (Island World, double X, double Y) UpperFloor()
    {
        var (world, x, y) = Open();
        world.Structures.Place(BuildKind.Post, x - 0.8, y - 0.8, 0, length: 0.01);
        world.Structures.Place(BuildKind.Post, x + 0.8, y + 0.8, 0, length: 0.01);
        world.Structures.Place(BuildKind.Floor, x, y, 0, length: GameData.Building.Floor.Size, level: 1);
        return (world, x, y);
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

    private static Inventory Sticks()
    {
        var inventory = new Inventory(60);
        for (var i = 0; i < 50; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        return inventory;
    }
}

using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Stavební mřížka (políčka 2 × 2 m): první díl založí plán, další se k němu přichytí, nic není křivé.</summary>
public class BuildGridTests
{
    private static Island NewIsland()
    {
        return WorldGenerator.Build(12345, 128, 128);
    }

    /// <summary>Volné místo na trávě 9 × 9 m (pro pár políček stavby).</summary>
    private static (double X, double Y) OpenSpot(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass && Enumerable.Range(-4, 9).All(dy => Enumerable.Range(-4, 9).All(dx => !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Volné místo nenalezeno.");
    }

    private static Inventory Sticks(int count)
    {
        var inventory = new Inventory(20);
        for (var i = 0; i < count; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        return inventory;
    }

    private static BuildResult Put(Island island, BuildKind kind, double px, double py, double x, double y, double angle)
    {
        var snap = island.Structures.Snap(kind, x, y, angle);
        return island.Structures.BuildOnGrid(island, new Fires(), Sticks(20), px, py, kind, snap, []);
    }

    [Fact]
    public void FirstPieceMakesPlanUnderCursorWithRoundedAngle()
    {
        var snap = BuildGrid.NewPlan(BuildKind.Floor, 10, 20, 0.1);

        Assert.True(snap.NewPlan);
        Assert.Equal((10.0, 20.0), (snap.X, snap.Y));
        Assert.Equal(0, snap.PlanAngle, 9);
        var plan = new GridPlan(1, snap.PlanX, snap.PlanY, snap.PlanAngle);
        Assert.Equal((0.5, 0.5), BuildGrid.ToLocal(plan, 10, 20));

        var turned = BuildGrid.NewPlan(BuildKind.StickWall, 10, 20, 0.3);
        Assert.Equal(Math.PI / 12, turned.PlanAngle, 9);
    }

    /// <summary>Druhá podlaha poblíž (i s jiným úhlem) leží přesně vedle první, ve stejném natočení.</summary>
    [Fact]
    public void FloorsSnapSideBySide()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        Assert.Equal(BuildResult.Built, Put(island, BuildKind.Floor, x - 3, y, x, y, 0.1));
        Assert.Equal(BuildResult.Built, Put(island, BuildKind.Floor, x - 3, y, x + 2.3, y + 0.4, 0.5));

        var floors = island.Structures.All.OrderBy(s => s.X).ToList();
        Assert.Equal(2, floors.Count);
        Assert.Equal(floors[0].X + BuildGrid.Cell, floors[1].X, 6);
        Assert.Equal(floors[0].Y, floors[1].Y, 6);
        Assert.Equal(floors[0].Angle, floors[1].Angle, 6);
        Assert.Equal(floors[0].PlanId, floors[1].PlanId);
        Assert.Single(island.Structures.Plans);
    }

    /// <summary>Zeď leží na hraně políčka: úhel hráče vybere směr hrany, kurzor kterou hranu. Sloupek do rohu.</summary>
    [Fact]
    public void WallsGoOnEdgesAndPostsIntoCorners()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        Put(island, BuildKind.Floor, x - 3, y, x, y, 0);
        var plan = island.Structures.Plans.Single();

        // Kurzor u východního okraje podlahy, úhel skoro svisle (křivě): zeď je přesně na hraně, svisle, 2 m.
        Assert.Equal(BuildResult.Built, Put(island, BuildKind.StickWall, x - 3, y, x + 0.9, y + 0.2, Math.PI / 2 + 0.2));
        var wall = island.Structures.All.Single(s => s.Kind == BuildKind.StickWall);
        Assert.Equal((1.0, 0.5), Round(BuildGrid.ToLocal(plan, wall.X, wall.Y)));
        Assert.Equal(Math.PI / 2, wall.Angle, 6);
        Assert.Equal(BuildGrid.Cell, wall.Length, 6);

        Assert.Equal(BuildResult.Built, Put(island, BuildKind.Post, x - 3, y, x + 1.1, y + 1.2, 0.7));
        var post = island.Structures.All.Single(s => s.Kind == BuildKind.Post);
        Assert.Equal((1.0, 1.0), Round(BuildGrid.ToLocal(plan, post.X, post.Y)));
    }

    [Fact]
    public void PlansAreSavedAndEmptyOnesForgotten()
    {
        var island = NewIsland();
        var (x, y) = OpenSpot(island);
        Put(island, BuildKind.Floor, x - 3, y, x, y, 0.5);
        var saved = island.Structures.ExportPlans();
        Assert.Single(saved);
        Assert.Equal(island.Structures.All.Single().PlanId, saved[0].Id);

        var copy = NewIsland();
        copy.Structures.Restore(island.Structures.Export());
        copy.Structures.RestorePlans(saved, island.Structures.NextPlanId);
        Assert.Equal(island.Structures.Plans, copy.Structures.Plans);

        island.Structures.Remove(island.Structures.All.Single().Id, Sticks(0), x, y);
        Assert.Empty(island.Structures.Plans);
        Assert.Empty(island.Structures.ExportPlans());
    }

    private static (double U, double V) Round((double U, double V) p)
    {
        return (Math.Round(p.U, 6), Math.Round(p.V, 6));
    }
}

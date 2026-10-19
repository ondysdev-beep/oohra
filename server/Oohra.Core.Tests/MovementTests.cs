using Oohra.Core.Characters;
using Oohra.Core.Rng;
using Oohra.Core.World;
using static Oohra.Core.Tests.TestPlants;

namespace Oohra.Core.Tests;

public class MovementTests
{
    private static readonly Island _island = WorldGenerator.Build(12345, 128, 128);

    [Fact]
    public void SeaPondAndTreesBlock()
    {
        Assert.True(Movement.IsBlocked(_island, 0, 0));
        Assert.True(Movement.IsBlocked(_island, -1, 5));

        var tree = _island.Trees[0];
        Assert.True(Movement.IsBlocked(_island, (int)tree.X, (int)tree.Y));

        var pond = Array.IndexOf(_island.Tiles, TileType.Pond);
        Assert.True(Movement.IsBlocked(_island, pond % _island.Width, pond / _island.Width));
    }

    /// <summary>Strom blokuje jen kmen: na okraj jeho dlaždice se vejdeš, skrz střed ne.</summary>
    [Fact]
    public void TreeBlocksOnlyTrunk()
    {
        var tree = _island.Trees.First(t => AloneOnDryLand((int)t.X, (int)t.Y));
        int tx = (int)tree.X, ty = (int)tree.Y;

        Assert.False(Movement.CanStand(_island, tx + 0.5, ty + 0.5, 0.28));
        Assert.True(Movement.CanStand(_island, tx + 0.5, ty + 0.05, 0.28));
        Assert.True(Movement.CanStand(_island, tx + 0.05, ty + 0.5, 0.28));

        // Chůze na východ přímo na kmen: postava ho obejde a pokračuje dál.
        var (x, y) = (tx - 0.5, ty + 0.5);
        for (var i = 0; i < 60; i++)
        {
            (x, y) = Movement.Step(_island, x, y, 1, 0, 0.05);
            Assert.True(Movement.CanStand(_island, x, y, 0.28));
        }

        Assert.True(x > tx + 1, $"Postava se zasekla o kmen na x = {x:0.00}.");
    }

    /// <summary>
    /// Mezerou mezi dvěma sousedními stromy (stojí aspoň rozestup od sebe) se dá projít, i když postava nemíří
    /// přesně doprostřed mezery.
    /// </summary>
    [Theory]
    [InlineData(-0.8)]
    [InlineData(0.0)]
    [InlineData(0.8)]
    public void WalksBetweenNeighbouringTrees(double offset)
    {
        var (a, b) = TreePairOnDryLand();
        var (ax, ay) = ((b.X - a.X) / Distance(a, b), (b.Y - a.Y) / Distance(a, b));
        var (px, py) = (-ay, ax);
        var (mx, my) = ((a.X + b.X) / 2 + ax * offset, (a.Y + b.Y) / 2 + ay * offset);
        var (x, y) = (mx - px * 3, my - py * 3);

        for (var i = 0; i < 160; i++)
        {
            (x, y) = Movement.Step(_island, x, y, px, py, 0.05);
        }

        var passed = (x - mx) * px + (y - my) * py;
        Assert.True(passed > 2, $"Postava se mezi stromy zasekla na x = {x:0.00}, y = {y:0.00}.");
    }

    private static double Distance(Tree a, Tree b)
    {
        return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    /// <summary>Dva stromy 4 až 6 m od sebe, kolem nich (5 m od středu mezery) sucho a žádné další stromy.</summary>
    private static (Tree A, Tree B) TreePairOnDryLand()
    {
        var trees = _island.Trees.ToList();
        foreach (var a in trees)
        {
            foreach (var b in trees)
            {
                var d = Distance(a, b);
                if (a.TileIndex >= b.TileIndex || d < 4 || d > 6)
                {
                    continue;
                }

                var (mx, my) = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                var lonely = trees.All(t => t.TileIndex == a.TileIndex || t.TileIndex == b.TileIndex
                    || Math.Sqrt((t.X - mx) * (t.X - mx) + (t.Y - my) * (t.Y - my)) > 6);
                var dry = Enumerable.Range(-5, 11).All(dy => Enumerable.Range(-5, 11).All(dx =>
                    _island.InBounds((int)mx + dx, (int)my + dy) && _island.TileAt((int)mx + dx, (int)my + dy) is not (TileType.Sea or TileType.Pond or TileType.Rock or TileType.LayeredRock)));
                if (lonely && dry)
                {
                    return (a, b);
                }
            }
        }

        throw new InvalidOperationException("Na ostrově nejsou dva osamělé sousední stromy.");
    }

    /// <summary>Rostliny nepřekáží: postava jimi projde.</summary>
    [Fact]
    public void PlantsDoNotBlock()
    {
        var plant = _island.Plants.First(p => Enumerable.Range(0, 21).All(i => Movement.CanStand(_island, p.X - 1 + i * 0.1, p.Y, 0.28)));
        var (x, y) = (plant.X - 1, plant.Y);

        for (var i = 0; i < 20; i++)
        {
            (x, y) = Movement.Step(_island, x, y, 1, 0, 0.1);
        }

        Assert.True(x > plant.X + 0.9);
        Assert.Equal(plant.Y, y);
    }

    /// <summary>Strom na suchu, kolem kterého nejsou jiné stromy ani voda (pro test kmene).</summary>
    private static bool AloneOnDryLand(int tx, int ty)
    {
        for (var y = ty - 1; y <= ty + 1; y++)
        {
            for (var x = tx - 1; x <= tx + 1; x++)
            {
                var tile = _island.TileAt(x, y);
                if (tile == TileType.Sea || tile == TileType.Pond || ((x, y) != (tx, ty) && _island.HasTree(_island.Index(x, y))))
                {
                    return false;
                }
            }
        }

        return true;
    }

    [Fact]
    public void CampIsWalkable()
    {
        var camp = _island.Camps[0];

        Assert.True(Movement.IsWalkable(_island, camp.X + 0.5, camp.Y + 0.5, 0.28));
    }

    [Fact]
    public void CannotWalkIntoSea()
    {
        var start = SpawnPoint.PickWild(_island, new Mulberry32(1), firstLife: true);
        var (x, y) = (start.X, start.Y);

        for (var i = 0; i < 2000; i++)
        {
            (x, y) = Movement.Step(_island, x, y, 1, 0, 0.1);
        }

        Assert.True(Movement.CanStand(_island, x, y, 0.28));
        Assert.NotEqual(TileType.Sea, _island.TileAt((int)x, (int)y));
    }

    [Fact]
    public void StepMovesByDistanceOnOpenGround()
    {
        var camp = _island.Camps[0];

        var (x, y) = Movement.Step(_island, camp.X + 0.5, camp.Y + 0.5, 3, 4, 0.5);

        Assert.Equal(camp.X + 0.5 + 0.3, x, 9);
        Assert.Equal(camp.Y + 0.5 + 0.4, y, 9);
    }

    [Fact]
    public void BogIsSlower()
    {
        var bog = Array.IndexOf(_island.Tiles, TileType.Bog);
        var grassIndex = Array.IndexOf(_island.Tiles, TileType.Grass);

        var width = _island.Width;
        Assert.Equal(0.72, Movement.TerrainSpeed(_island, bog % width + 0.5, bog / width + 0.5));
        Assert.Equal(1, Movement.TerrainSpeed(_island, grassIndex % width + 0.5, grassIndex / width + 0.5));
    }

    [Fact]
    public void PoisonAndFeverSlowDown()
    {
        var healthy = Person(generation: 1);
        var sick = Person(generation: 1);
        sick.Toxin = 100;
        sick.Fever = 90;

        Assert.True(Movement.BodySpeed(sick) < Movement.BodySpeed(healthy));
        Assert.True(Movement.BodySpeed(sick) >= 0.35);
    }

    [Fact]
    public void AncientIsSlightlySlowerThanNew()
    {
        Assert.Equal(0.95, Movement.BodySpeed(Person(generation: 0)), 9);
        Assert.Equal(1.02, Movement.BodySpeed(Person(generation: 1)), 9);
        Assert.Equal(1.1, Movement.BodySpeed(Person(generation: 6)), 9);
    }

    [Fact]
    public void FirstLifeSpawnsOnWalkableSandNearWater()
    {
        foreach (var seed in new[] { 1, 2, 3, 4, 5 })
        {
            var spawn = SpawnPoint.PickWild(_island, new Mulberry32(seed), firstLife: true);
            var tx = (int)spawn.X;
            var ty = (int)spawn.Y;

            Assert.True(Movement.IsWalkable(_island, spawn.X, spawn.Y, 0.3));
            Assert.Contains(_island.TileAt(tx, ty), new[] { TileType.Sand, TileType.Grass });
            Assert.True(_island.PondNear(tx, ty, 12));
        }
    }

    /// <summary>
    /// Na ostrově se seedem 1 (84 × 84) se první hráč na serveru dřív narodil na písku mezi stromy a mořem
    /// a nemohl odejít. Místo narození teď musí mít kolem sebe dost volného místa.
    /// </summary>
    [Fact]
    public void SpawnIsNeverEnclosed()
    {
        // Ve starém ostrově z prototypu bylo u (68, 59) uzavřené místo, kde se hráč narodil a nemohl odejít.
        var island = WorldGenerator.Build(1, 128, 128);

        for (var seed = 1; seed <= 300; seed++)
        {
            var spawn = SpawnPoint.PickWild(island, new Mulberry32(seed), firstLife: seed % 2 == 0);
            Assert.True(SpawnPoint.OpenTiles(island, (int)spawn.X, (int)spawn.Y, 40) >= 40, $"Seed {seed}: {spawn}");
        }
    }

    [Fact]
    public void SpawnIsAwayFromCamps()
    {
        var spawn = SpawnPoint.PickWild(_island, new Mulberry32(7), firstLife: true);

        foreach (var camp in _island.Camps)
        {
            var distance = Math.Sqrt(Math.Pow(spawn.X - camp.X, 2) + Math.Pow(spawn.Y - camp.Y, 2));
            Assert.True(distance > 8);
        }
    }
}

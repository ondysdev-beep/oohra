using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Kopce a svahy: voda ve výšce 0, souš se zvedá, vrchovina je vysoko, do kopce se jde pomaleji, sráz nejde.</summary>
public class TerrainTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 384, 384);

    [Fact]
    public void WaterIsAtSeaLevelAndLandRises()
    {
        var e = _island.Elevation;
        for (var i = 0; i < _island.Tiles.Length; i += 7)
        {
            var (x, y) = (i % _island.Width + 0.5, i / _island.Width + 0.5);
            var tile = _island.Tiles[i];
            if (tile is TileType.Sea or TileType.Shallows or TileType.Pond)
            {
                Assert.True(e.At(x, y) < 0.6, $"Voda ({tile}) je moc vysoko: {e.At(x, y):0.00} m.");
            }
            else if (tile is TileType.Grass or TileType.Forest or TileType.Highland)
            {
                Assert.True(e.At(x, y) > 0, "Souš je pod hladinou.");
            }
        }

        Assert.Equal(_island.Elevation.Corners, WorldGenerator.Build(1, 384, 384).Elevation.Corners);
    }

    [Fact]
    public void HighlandIsHigherWithRockRidges()
    {
        double Average(RegionKind region) => Enumerable.Range(0, _island.Tiles.Length)
            .Where(i => _island.Regions[i] == region)
            .Average(i => _island.Elevation.At(i % _island.Width + 0.5, i / _island.Width + 0.5));

        Assert.True(Average(RegionKind.Highland) > Average(RegionKind.Meadow) + 5, "Vrchovina má být výš než louka.");
        var ridge = Enumerable.Range(0, _island.Tiles.Length).Where(i => _island.Tiles[i] == TileType.Rock)
            .Max(i => _island.Elevation.At(i % _island.Width + 0.5, i / _island.Width + 0.5));
        Assert.True(ridge > 20, $"Skalní hřbety mají být vysoké (nejvyšší {ridge:0.0} m).");
    }

    /// <summary>Do kopce se jde pomaleji, z kopce rychleji. Na sráz strmější než maxGrade se nevyleze, dolů ano.</summary>
    [Fact]
    public void SlopesSlowUphillAndCliffsStopClimbing()
    {
        var e = _island.Elevation;
        var (x, y, dx, dy) = Steepest(0.3);
        Assert.True(Movement.SlopeSpeed(_island, x, y, dx, dy) < 1);
        Assert.True(Movement.SlopeSpeed(_island, x, y, -dx, -dy) > 1);
        Assert.Equal(1, Movement.SlopeSpeed(_island, x, y, 0, 0));

        var max = GameData.Movement.Slope.MaxGrade;
        Assert.True(Movement.Climbable(_island, x, y, x - dx * 0.2, y - dy * 0.2), "Z kopce dolů jde vždy.");
        var cliff = Enumerable.Range(0, _island.Tiles.Length)
            .Select(i => (X: i % _island.Width + 0.5, Y: i / _island.Width + 0.5))
            .FirstOrDefault(p => e.Grade(p.X, p.Y, 1, 0) > max * 1.5);
        if (cliff != default)
        {
            Assert.False(Movement.Climbable(_island, cliff.X - 0.1, cliff.Y, cliff.X + 0.1, cliff.Y), "Na sráz se vylézt nedá.");
        }
    }

    /// <summary>Na strmém svahu se nestaví, na rovině ano.</summary>
    [Fact]
    public void NoBuildingOnSteepSlope()
    {
        var (x, y, _, _) = Steepest(GameData.Building.MaxRise);
        Assert.False(Placement.LevelEnough(_island, [new Position(x - 1, y), new Position(x + 1, y), new Position(x, y - 1), new Position(x, y + 1)]));

        var flat = Enumerable.Range(0, _island.Tiles.Length)
            .Where(i => _island.Tiles[i] == TileType.Grass)
            .Select(i => new Position(i % _island.Width + 0.5, i / _island.Width + 0.5))
            .First(p => _island.Elevation.Steepness(p.X, p.Y) < 0.05);
        Assert.True(Placement.LevelEnough(_island, [flat]));
    }

    /// <summary>Svahy neodříznou kus ostrova: skoro všude se dá dojít pěšky (chůze mezi středy sousedních dlaždic).</summary>
    [Fact]
    public void LandStaysReachableOnFoot()
    {
        var (w, h) = (_island.Width, _island.SurfaceHeight);
        bool Walkable(int i) => Movement.CanStand(_island, i % w + 0.5, i / w + 0.5, 0.05);
        var land = Enumerable.Range(0, w * h).Where(i => Walkable(i) && _island.Tiles[i] is TileType.Grass or TileType.Forest or TileType.Highland or TileType.Sand).ToList();
        var seen = new HashSet<int> { land[land.Count / 2] };
        var queue = new Queue<int>(seen);
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            foreach (var n in new[] { i - 1, i + 1, i - w, i + w })
            {
                if (n < 0 || n >= w * h || seen.Contains(n) || !Walkable(n)
                    || !Movement.Climbable(_island, i % w + 0.5, i / w + 0.5, n % w + 0.5, n / w + 0.5))
                {
                    continue;
                }

                seen.Add(n);
                queue.Enqueue(n);
            }
        }

        var reached = land.Count(seen.Contains) / (double)land.Count;
        Assert.True(reached > 0.97, $"Pěšky se dá dojít jen na {reached:P1} souše.");
    }

    /// <summary>Místo na souši, kde svah stoupá aspoň o grade metrů na metr (a směr do kopce).</summary>
    private static (double X, double Y, double Dx, double Dy) Steepest(double grade)
    {
        var e = _island.Elevation;
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            if (_island.Tiles[i] is not (TileType.Grass or TileType.Forest or TileType.Highland))
            {
                continue;
            }

            var (x, y) = (i % _island.Width + 0.5, i / _island.Width + 0.5);
            var (gx, gy) = (e.Grade(x, y, 1, 0), e.Grade(x, y, 0, 1));
            var g = Math.Sqrt(gx * gx + gy * gy);
            if (g > grade)
            {
                return (x, y, gx / g, gy / g);
            }
        }

        throw new InvalidOperationException("Na ostrově není svah se sklonem " + grade);
    }
}

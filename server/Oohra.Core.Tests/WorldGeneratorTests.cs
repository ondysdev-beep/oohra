using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Nový generátor světa 384 × 384 s oblastmi (návrh 0.8, kap. 1).</summary>
public class WorldGeneratorTests
{
    private const int Size = 384;

    private static readonly Dictionary<int, Lazy<Island>> _worlds = new[] { 1, 12345, 987654321 }
        .ToDictionary(seed => seed, seed => new Lazy<Island>(() => WorldGenerator.Build(seed, Size, Size)));

    public static TheoryData<int> Seeds => new() { 1, 12345, 987654321 };

    private static Island World(int seed) => _worlds[seed].Value;

    [Fact]
    public void SameSeedGivesSameWorld()
    {
        var again = WorldGenerator.Build(1, Size, Size);
        var first = World(1);

        Assert.Equal(first.Tiles, again.Tiles);
        Assert.Equal(first.Regions, again.Regions);
        Assert.Equal(first.Trees, again.Trees);
        Assert.Equal(first.Plants, again.Plants);
        Assert.Equal(first.Camps, again.Camps);
        Assert.Equal(first.RuinSpots, again.RuinSpots);
        Assert.NotEqual(first.Tiles, World(12345).Tiles);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RegionsHaveSharesFromRules(int seed)
    {
        var world = World(seed);
        var shares = GameData.WorldGen.Shares;
        var land = world.Regions.Count(r => r != RegionKind.Sea);

        Assert.InRange(land / (double)(Size * Size), 0.45, 0.6);
        Assert.InRange(Share(world, RegionKind.Highland), shares.Highland - 0.02, shares.Highland + 0.005);
        Assert.InRange(Share(world, RegionKind.Marsh), shares.Marsh - 0.005, shares.Marsh + 0.005);
        Assert.InRange(Share(world, RegionKind.Forest), shares.Forest - 0.01, shares.Forest + 0.005);
        Assert.InRange(Share(world, RegionKind.Lake), shares.Lake - 0.01, shares.Lake + 0.005);
        Assert.InRange(Share(world, RegionKind.River), 0.002, 0.03);
        Assert.InRange(Share(world, RegionKind.Meadow), 0.15, 0.4);
        Assert.InRange(Share(world, RegionKind.Coast), 0.01, 0.12);
    }

    /// <summary>Jeden ostrov: na každé místo souše se dá dojít pěšky (přes řeku po brodech).</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AllLandIsOneWalkableIsland(int seed)
    {
        var world = World(seed);
        var walkable = Enumerable.Range(0, world.Tiles.Length).Where(i => !Movement.IsWaterTile(world.Tiles[i])).ToList();
        var reached = Reach(world, walkable[0]);

        Assert.Equal(walkable.Count, reached);
        Assert.All(Border(world), i => Assert.Equal(TileType.Sea, world.Tiles[i]));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RiverRunsFromHighlandThroughLakeToSeaWithFords(int seed)
    {
        var world = World(seed);
        var river = Indices(world, i => world.Regions[i] == RegionKind.River).ToList();

        Assert.NotEmpty(river);
        Assert.Contains(river, i => Neighbours(world, i).Any(n => world.Tiles[n] == TileType.Sea));
        Assert.Contains(river, i => Neighbours(world, i).Any(n => world.Regions[n] == RegionKind.Lake));
        Assert.Contains(river, i => Neighbours(world, i).Any(n => world.Regions[n] == RegionKind.Highland));
        Assert.True(world.Tiles.Count(t => t == TileType.Ford) > 10);
        Assert.All(Indices(world, i => world.Tiles[i] == TileType.Ford), i => Assert.Equal(RegionKind.River, world.Regions[i]));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TilesMatchTheirRegions(int seed)
    {
        var world = World(seed);
        for (var i = 0; i < world.Tiles.Length; i++)
        {
            var (tile, region) = (world.Tiles[i], world.Regions[i]);
            if (tile is TileType.Rock or TileType.LayeredRock or TileType.Highland) Assert.Equal(RegionKind.Highland, region);
            if (tile == TileType.Bog) Assert.Equal(RegionKind.Marsh, region);
            if (tile == TileType.Sand) Assert.Equal(RegionKind.Coast, region);
            if (region == RegionKind.Sea) Assert.Equal(TileType.Sea, tile);
        }

        Assert.True(world.Tiles.Count(t => t == TileType.LayeredRock) > 100, "Vrstvená skála (pazourek) chybí.");
        Assert.True(Indices(world, i => world.Tiles[i] == TileType.Grass && world.Regions[i] == RegionKind.Forest).Count() > 500, "V lese chybí světliny.");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TreesAndPlantsStandOnLand(int seed)
    {
        var world = World(seed);

        Assert.InRange(world.Trees.Count, 2000, 12000);
        Assert.All(world.Trees, t => Assert.DoesNotContain(world.Tiles[t.TileIndex], new[]
        {
            TileType.Sea, TileType.Pond, TileType.Ford, TileType.Sand, TileType.Rock, TileType.LayeredRock,
        }));
        Assert.All(world.Plants, p => Assert.DoesNotContain(world.TileAt((int)p.X, (int)p.Y), new[]
        {
            TileType.Sea, TileType.Pond, TileType.Ford,
        }));
        Assert.Equal(world.Trees.Count, world.Trees.Select(t => t.TileIndex).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void CampsAndRuinsAreOnDryLandFarApart(int seed)
    {
        var world = World(seed);
        var camps = GameData.WorldGen.Camps;

        Assert.Equal(camps.Count, world.Camps.Count);
        Assert.Equal(GameData.WorldGen.Ruins.Count, world.RuinSpots.Count);
        Assert.All(world.Camps, c => Assert.Contains(world.RegionAt(c.X, c.Y), new[] { RegionKind.Meadow, RegionKind.Forest }));
        Assert.All(world.Camps, c => Assert.True(world.PondNear(c.X, c.Y, camps.WaterWithin), "Tábor nemá vodu poblíž."));
        Assert.True(Distance(world.Camps[0].X, world.Camps[0].Y, world.Camps[1].X, world.Camps[1].Y) >= camps.MinDistance);
        Assert.All(world.RuinSpots, r => Assert.DoesNotContain(world.TileAt((int)r.X, (int)r.Y), new[]
        {
            TileType.Sea, TileType.Pond, TileType.Ford, TileType.Bog,
        }));
    }

    /// <summary>Stromy mají druh podle oblasti a výšku v rozsahu druhu (trees.json), všech pět druhů roste.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TreesHaveSpeciesByRegionAndHeight(int seed)
    {
        var world = World(seed);
        var rules = GameData.Trees;

        Assert.All(world.Trees, t =>
        {
            var species = rules.For(t.Kind);
            Assert.InRange(t.Height, species.MinHeight, species.MaxHeight);
        });
        Assert.All(Enum.GetValues<TreeKind>(), kind => Assert.Contains(world.Trees, t => t.Kind == kind));
        Assert.All(world.Trees.Where(t => world.Regions[t.TileIndex] == RegionKind.Highland), t =>
            Assert.Contains(t.Kind, new[] { TreeKind.Conifer, TreeKind.Dead, TreeKind.Willow }));
        Assert.DoesNotContain(world.Trees, t => world.Regions[t.TileIndex] == RegionKind.Marsh && t.Kind == TreeKind.Oak);
        Assert.Equal(world.Trees, WorldGenerator.Build(seed, Size, Size).Trees);
    }

    /// <summary>Brod: dá se po něm jít a pít z něj (čistá voda), stavět na něm nejde.</summary>
    [Fact]
    public void FordCanBeCrossedAndDrunkFrom()
    {
        var world = World(1);
        var ford = Indices(world, i => world.Tiles[i] == TileType.Ford).First();
        var (x, y) = (ford % world.Width, ford / world.Width);
        var c = new Oohra.Core.Characters.Character(1, new Oohra.Core.Rng.Mulberry32(1)) { Hydration = 20 };

        Assert.True(Movement.IsWalkable(world, x + 0.5, y + 0.5, 0.1));
        Assert.False(Placement.OnDryLand(world, x + 0.5, y + 0.5));
        Assert.Equal(Oohra.Core.Characters.DrinkResult.Clean, Oohra.Core.Characters.Drinking.Drink(c, world, x, y, x + 0.5, y + 0.5, TestPlants.Clock()));
        Assert.True(c.Hydration > 20);
    }

    private static double Share(Island world, RegionKind region)
    {
        var land = world.Regions.Count(r => r != RegionKind.Sea);
        return world.Regions.Count(r => r == region) / (double)land;
    }

    private static int Reach(Island world, int start)
    {
        var seen = new HashSet<int> { start };
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            foreach (var n in Neighbours(world, queue.Dequeue()))
            {
                if (!Movement.IsWaterTile(world.Tiles[n]) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }

        return seen.Count;
    }

    private static IEnumerable<int> Neighbours(Island world, int i)
    {
        var (x, y) = (i % world.Width, i / world.Width);
        if (x > 0) yield return i - 1;
        if (x < world.Width - 1) yield return i + 1;
        if (y > 0) yield return i - world.Width;
        if (y < world.Height - 1) yield return i + world.Width;
    }

    private static IEnumerable<int> Border(Island world)
    {
        return Indices(world, i => i % world.Width == 0 || i / world.Width == 0 || i % world.Width == world.Width - 1 || i / world.Width == world.Height - 1);
    }

    private static IEnumerable<int> Indices(Island world, Func<int, bool> match)
    {
        return Enumerable.Range(0, world.Tiles.Length).Where(match);
    }

    private static double Distance(int ax, int ay, int bx, int by)
    {
        return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }
}

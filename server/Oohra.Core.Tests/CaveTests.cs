using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Jeskyně (návrh 0.8, kap. 4): podzemí pod ostrovem, vchody ve skále, tma a stálá teplota.</summary>
public class CaveTests
{
    private static readonly Island _world = WorldGenerator.BuildWorld(1, 128, 128);
    private static readonly Lazy<Island> _big = new(() => WorldGenerator.BuildWorld(1, 384, 384));

    [Fact]
    public void SameSeedGivesSameCavesAndSurfaceStaysTheSame()
    {
        var again = WorldGenerator.BuildWorld(1, 128, 128);
        var surface = WorldGenerator.Build(1, 128, 128);

        Assert.Equal(_world.Tiles, again.Tiles);
        Assert.Equal(_world.CaveEntrances, again.CaveEntrances);
        Assert.Equal(128, _world.SurfaceHeight);
        Assert.True(_world.Height > 128 + GameData.Caves.Gap);
        Assert.Equal(surface.Tiles, _world.Tiles.Take(surface.Tiles.Length));
        Assert.Equal(surface.Trees.Count, _world.Trees.Count);

        // Mezi ostrovem a podzemím je jen moře.
        var gap = Enumerable.Range(128 * 128, 128 * GameData.Caves.Gap);
        Assert.All(gap, i => Assert.Equal(TileType.Sea, _world.Tiles[i]));
    }

    /// <summary>
    /// Na velkém ostrově je jeskyní víc (podle vrchoviny), každá je celá průchozí: z místa u vchodu se dojde
    /// na každou podlahu i k pazourku. Na malém ostrově může být i jen jedna (málo skal pro vchody).
    /// </summary>
    [Fact]
    public void EveryCaveIsConnectedAndClosedInRock()
    {
        var world = _big.Value;
        Assert.InRange(world.Caves.Count, GameData.Caves.MinCaves, GameData.Caves.MaxCaves);
        Assert.Contains(world.Caves, c => c.Size == CaveSize.Large || c.Size == CaveSize.Medium);
        Assert.NotEmpty(_world.Caves);
        foreach (var cave in world.Caves)
        {
            var start = world.Index((int)cave.Rooms[0].X, (int)cave.Rooms[0].Y);
            Assert.Equal(TileType.CaveFloor, world.Tiles[start]);
            var reached = Reachable(world, start);
            var floor = Enumerable.Range(0, world.Tiles.Length)
                .Where(i => Caves.At(world, i % world.Width + 0.5, i / world.Width + 0.5) == cave && world.Tiles[i] == TileType.CaveFloor)
                .ToList();
            Assert.All(floor, i => Assert.Contains(i, reached));
            Assert.All(reached, i => Assert.Equal(RegionKind.Cave, world.Regions[i]));
            Assert.All(cave.Rooms, r => Assert.Contains(world.Index((int)r.X, (int)r.Y), reached));
        }
    }

    [Fact]
    public void EntrancesAreAtRockOnLandAndLeadIntoTheirCave()
    {
        Assert.NotEmpty(_world.CaveEntrances);
        foreach (var e in _world.CaveEntrances)
        {
            var (x, y) = ((int)e.X, (int)e.Y);
            Assert.True(y < _world.SurfaceHeight);
            Assert.True(Movement.IsWalkable(_world, e.X, e.Y, 0.28), $"Vchod {e.Id} není na chodné souši.");
            var rock = (x + (int)Math.Round(Math.Cos(e.Facing)), y + (int)Math.Round(Math.Sin(e.Facing)));
            Assert.Contains(_world.TileAt(rock.Item1, rock.Item2), new[] { TileType.Rock, TileType.LayeredRock });
            Assert.True(_world.InCave(e.InsideX, e.InsideY));
            Assert.Equal(e.CaveId, Caves.At(_world, e.InsideX, e.InsideY)!.Id);
            Assert.All(_world.CaveEntrances.Where(o => o.Id != e.Id), o =>
                Assert.True(Math.Sqrt(Math.Pow(o.X - e.X, 2) + Math.Pow(o.Y - e.Y, 2)) >= GameData.Caves.EntranceSpacing));
        }
    }

    /// <summary>U vchodu venku se vchází dovnitř, u východu uvnitř ven. Z dálky to nejde.</summary>
    [Fact]
    public void PassingThroughEntranceMovesInAndOut()
    {
        var e = _world.CaveEntrances.First(e => !e.Hidden);
        var c = Person();

        var (result, inside) = Caves.Pass(_world, c, e.Id, e.X + 0.5, e.Y);
        Assert.Equal(CaveResult.Entered, result);
        Assert.True(_world.InCave(inside.X, inside.Y));
        Assert.True(Movement.IsWalkable(_world, inside.X, inside.Y, 0.28));

        var (back, outside) = Caves.Pass(_world, c, e.Id, inside.X, inside.Y);
        Assert.Equal(CaveResult.Left, back);
        Assert.Equal((e.X, e.Y), (outside.X, outside.Y));
        Assert.Equal(CaveResult.TooFar, Caves.Pass(_world, c, e.Id, e.X + 10, e.Y).Result);
    }

    /// <summary>Nenápadný vchod postava vidí, až když k němu dojde. Nová postava (po smrti) ho zase nezná.</summary>
    [Fact]
    public void HiddenEntranceIsFoundOnlyUpClose()
    {
        var world = Enumerable.Range(1, 30).Select(s => WorldGenerator.BuildWorld(s, 128, 128)).First(w => w.CaveEntrances.Any(e => e.Hidden));
        var hidden = world.CaveEntrances.First(e => e.Hidden);
        var c = Person();

        Assert.DoesNotContain(hidden, Caves.Visible(world, c));
        Assert.Equal(CaveResult.Unknown, Caves.Pass(world, c, hidden.Id, hidden.X + 10, hidden.Y).Result);
        Assert.False(Caves.Discover(world, c, hidden.X + GameData.Caves.DiscoverRange + 2, hidden.Y, 0));

        Assert.True(Caves.Discover(world, c, hidden.X + 1, hidden.Y, 0));
        Assert.Contains(hidden, Caves.Visible(world, c));
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.FoundCave);
        Assert.True(Character.FromSave(c.ToSave()).KnowsCave(hidden.Id), "Objevený vchod se ukládá.");
        Assert.DoesNotContain(hidden, Caves.Visible(world, Person()));
    }

    /// <summary>V jeskyni je stálá teplota: v zimě je tu tepleji než venku.</summary>
    [Fact]
    public void CaveIsWarmerThanWinterOutside()
    {
        var winter = GameClock.FromSettings(2 * 60);
        winter.Restore(2 * 60, 10.5);
        var outside = Person();
        var inside = Person();
        for (var hour = 0; hour < 24; hour++)
        {
            Physiology.Update(outside, winter, 60);
            Physiology.Update(inside, winter, 60, sheltered: true, ambient: GameData.Caves.Temperature);
        }

        Assert.True(inside.Warmth > outside.Warmth + 10, $"jeskyně {inside.Warmth}, venku {outside.Warmth}");
    }

    /// <summary>V jeskyni nejde stavět zeď (stěny dává skála), zásoby ano. Pazourek má hromadu kamenů.</summary>
    [Fact]
    public void NoWallsInCaveButStockpileAndFlint()
    {
        var cave = _world.Caves.First(c => c.Rooms.Count > 0);
        var (x, y) = (cave.Rooms[0].X, cave.Rooms[0].Y);
        var sticks = new Inventory(20);
        for (var i = 0; i < 15; i++)
        {
            sticks.TryAdd(Item.Stick);
        }

        Assert.Equal(BuildResult.NotInCave, _world.Structures.Build(_world, new Fires(), sticks, x, y, BuildKind.StickWall, x + 1, y, 0, 1, []));
        var flint = Enumerable.Range(0, _world.Tiles.Length).Where(i => _world.Regions[i] == RegionKind.Cave && _world.Tiles[i] == TileType.LayeredRock).ToList();
        Assert.NotEmpty(flint);
        Assert.All(flint, i => Assert.Contains(_world.Rocks.All, p => p.TileIndex == i));
        Assert.Contains(Enumerable.Range(0, _big.Value.Tiles.Length), i => _big.Value.Regions[i] == RegionKind.Cave && _big.Value.Tiles[i] == TileType.Pond);
    }

    [Fact]
    public void AnimalsAreNotBornInCavesAndCountsUseIslandOnly()
    {
        var animals = new Animals(3);
        animals.Populate(_world, []);

        Assert.All(animals.All, a => Assert.False(_world.InCave(a.X, a.Y)));
        Assert.Equal(Animals.TargetCount(WorldGenerator.Build(1, 128, 128), AnimalKind.Deer), Animals.TargetCount(_world, AnimalKind.Deer));
    }

    private static HashSet<int> Reachable(Island island, int start)
    {
        var seen = new HashSet<int> { start };
        var queue = new Queue<int>([start]);
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            var (x, y) = (i % island.Width, i / island.Width);
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var (nx, ny) = (x + dx, y + dy);
                if (!island.InBounds(nx, ny) || island.TileAt(nx, ny) is TileType.CaveWall or TileType.Pond or TileType.Sea)
                {
                    continue;
                }

                if (seen.Add(island.Index(nx, ny)))
                {
                    queue.Enqueue(island.Index(nx, ny));
                }
            }
        }

        return seen;
    }

    private static Character Person() => new(1, new Mulberry32(1));
}

using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Social;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Život v jeskyních (návrh 0.8, kap. 4): zvěř v doupatech, zátaras u vchodu, kouř bez odvětrání.</summary>
public class CaveLifeTests
{
    private static readonly Lazy<Island> _big = new(() => WorldGenerator.BuildWorld(1, 384, 384));

    /// <summary>Část jeskyní je obsazená zvěří podle velikosti. Zvířata se rodí uvnitř své jeskyně.</summary>
    [Fact]
    public void DensHoldAnimalsBySize()
    {
        var world = _big.Value;
        var dens = world.Caves.Where(c => c.Den is not null).ToList();
        Assert.NotEmpty(dens);
        Assert.True(dens.Count < world.Caves.Count, "Většina jeskyní je prázdná.");
        foreach (var cave in dens)
        {
            var rules = GameData.Caves.Dens.Single(d => d.Size == cave.Size);
            Assert.Equal(rules.Kind, cave.Den);
            Assert.InRange(cave.DenCount, rules.Min, rules.Max);
        }

        var animals = new Animals(5);
        animals.Populate(world, []);
        foreach (var cave in dens)
        {
            var inside = animals.All.Where(a => a.CaveId == cave.Id).ToList();
            Assert.Equal(cave.DenCount, inside.Count);
            Assert.All(inside, a => Assert.Equal(cave, Caves.At(world, a.X, a.Y)));
            Assert.All(inside, a => Assert.True(Movement.IsWalkable(world, a.X, a.Y, 0.2)));
        }

        Assert.All(animals.All.Where(a => a.CaveId == 0), a => Assert.False(world.InCave(a.X, a.Y)));
    }

    /// <summary>V jeskyni je pořád tma: vlk v doupěti se plíží k hráči i v poledne. Pochodeň ho odradí.</summary>
    [Fact]
    public void DenWolvesHuntAtNoonUnlessTorch()
    {
        var world = _big.Value;
        var cave = world.Caves.First(c => c.Den == AnimalKind.Wolf);
        var noon = Clock(hour: 12);

        Assert.True(WolfStalks(world, cave, noon, torch: false));
        Assert.False(WolfStalks(world, cave, noon, torch: true));
    }

    /// <summary>Doupě se doplní, ale jen když v jeskyni nikdo není.</summary>
    [Fact]
    public void DenRefillsOnlyWhenNobodyInside()
    {
        var world = _big.Value;
        var cave = world.Caves.First(c => c.Den is not null);
        var animals = new Animals(5);
        animals.Populate(world, []);
        foreach (var a in animals.All.Where(a => a.CaveId == cave.Id).ToList())
        {
            a.State = AnimalState.Dead;
            a.DeadAt = -10000;
        }

        var inside = new AnimalTarget(1, cave.Rooms[0].X, cave.Rooms[0].Y, Person());
        var respawn = GameData.Animals.RespawnMinutes;
        animals.Update(world, new Fires(), new PlantStates(), Clock(hour: 12), 0.01, [inside]);
        animals.Update(world, new Fires(), new PlantStates(), Clock(hour: 12, plusMinutes: respawn + 1), 0.01, [inside]);
        Assert.Equal(0, animals.All.Count(a => a.CaveId == cave.Id && !a.IsDead));

        animals.Update(world, new Fires(), new PlantStates(), Clock(hour: 12, plusMinutes: 2 * respawn + 2), 0.01, []);
        Assert.Equal(1, animals.All.Count(a => a.CaveId == cave.Id && !a.IsDead));
    }

    /// <summary>Zátaras se staví jen u vchodu, jeden na vchod. Cizí neprojde dovnitř, ven ano. Kmen projde.</summary>
    [Fact]
    public void BarricadeLetsInOnlyOwnerAndTribe()
    {
        var world = WorldGenerator.BuildWorld(1, 384, 384);
        var e = world.CaveEntrances.First(e => !e.Hidden);
        var sticks = Sticks(20);

        Assert.Equal(BuildResult.NoCaveEntrance, world.Structures.Build(world, new Fires(), sticks, e.X + 6, e.Y, BuildKind.Barricade, e.X + 6.5, e.Y, 0, 1, [], ownerId: 1));
        Assert.Equal(BuildResult.Built, world.Structures.Build(world, new Fires(), sticks, e.X, e.Y, BuildKind.Barricade, e.X, e.Y, 0, 1, [], ownerId: 1));
        Assert.Equal(BuildResult.NoCaveEntrance, world.Structures.Build(world, new Fires(), sticks, e.X, e.Y, BuildKind.Barricade, e.X, e.Y, 0, 1, [], ownerId: 1));
        Assert.NotNull(Caves.BarricadeAt(world, e));
        Assert.True(Movement.IsWalkable(world, e.X, e.Y, 0.28), "Zátaras nepřekáží v chůzi.");

        var tribes = new Tribes();
        Assert.Equal(CaveResult.Barricaded, Caves.Pass(world, Person(), e.Id, e.X, e.Y, 2, tribes).Result);
        Assert.Equal(CaveResult.Entered, Caves.Pass(world, Person(), e.Id, e.X, e.Y, 1, tribes).Result);
        Assert.Equal(CaveResult.Left, Caves.Pass(world, Person(), e.Id, e.InsideX, e.InsideY, 2, tribes).Result);
    }

    /// <summary>Oheň v jeskyni s jediným vchodem dusí. Kde je druhý východ, táhne a kouř nevadí.</summary>
    [Fact]
    public void FireInClosedCaveSmokes()
    {
        var world = _big.Value;
        var closed = world.Caves.First(c => !Caves.Ventilated(world, c));
        Assert.Equal(1, Smoked(world, closed, withFire: true));
        Assert.Equal(0, Smoked(world, closed, withFire: false));

        var open = world.Caves.FirstOrDefault(c => Caves.Ventilated(world, c));
        if (open is not null)
        {
            Assert.Equal(0, Smoked(world, open, withFire: true));
        }
    }

    [Fact]
    public void NothingIsBuiltInsideCaveWalls()
    {
        var world = _big.Value;
        var wall = Enumerable.Range(0, world.Tiles.Length).First(i => world.Tiles[i] == TileType.CaveWall);
        Assert.False(Placement.OnDryLand(world, wall % world.Width + 0.5, wall / world.Width + 0.5));
    }

    /// <summary>Kolik hlášek o kouři dostane postava za 30 minut v jeskyni (a jestli ztratí zdraví).</summary>
    private static int Smoked(Island world, Cave cave, bool withFire)
    {
        var room = cave.Rooms[0];
        var fires = new Fires();
        if (withFire)
        {
            Assert.Equal(FireResult.Lit, fires.Light(world, Sticks(3, stone: true), room.X, room.Y, room.X + 1, room.Y));
        }

        var c = Person();
        var health = c.Health;
        for (var i = 0; i < 30; i++)
        {
            Caves.Smoke(world, fires, c, room.X, room.Y, 1, i);
        }

        var warnings = c.TakeMessages().Count(m => m.Event == BodyEvent.Smoke);
        Assert.Equal(withFire && !Caves.Ventilated(world, cave), c.Health < health);
        return Math.Min(1, warnings);
    }

    private static bool WolfStalks(Island world, Cave cave, GameClock clock, bool torch)
    {
        var animals = new Animals(1);
        animals.Populate(world, []);
        var wolf = animals.All.First(a => a.CaveId == cave.Id);
        var player = new AnimalTarget(1, wolf.X + 3, wolf.Y, Person());
        if (!Movement.IsWalkable(world, player.X, player.Y, 0.28))
        {
            player = player with { X = wolf.X - 3 };
        }

        if (!Movement.IsWalkable(world, player.X, player.Y, 0.28))
        {
            player = player with { X = wolf.X, Y = wolf.Y + 3 };
        }

        player.Character.TorchMinutes = torch ? 60 : 0;
        for (var i = 0; i < 40; i++)
        {
            animals.Update(world, new Fires(), new PlantStates(), clock, 0.05, [player]);
        }

        return wolf.State != AnimalState.Wander;
    }

    private static Inventory Sticks(int count, bool stone = false)
    {
        var inventory = new Inventory(30);
        for (var i = 0; i < count; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        if (stone)
        {
            inventory.TryAdd(Item.Stone);
        }

        return inventory;
    }

    private static Character Person() => new(1, new Mulberry32(1));

    private static GameClock Clock(double hour, double plusMinutes = 0)
    {
        var clock = GameClock.FromSettings(hour * 60);
        clock.Restore(hour * 60 + plusMinutes, 4.5);
        return clock;
    }
}

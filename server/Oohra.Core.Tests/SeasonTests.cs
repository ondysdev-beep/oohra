using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Sezónní nebezpečí a stopy (návrh 0.8, kap. 5): hladoví vlci v zimě, jarní záplava, stopy v bahně.</summary>
public class SeasonTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    [Fact]
    public void WinterHasMoreWolves()
    {
        Assert.True(Animals.TargetCount(_island, AnimalKind.Wolf, Season.Winter) > Animals.TargetCount(_island, AnimalKind.Wolf, Season.Summer));
        Assert.Equal(Animals.TargetCount(_island, AnimalKind.Boar, Season.Summer), Animals.TargetCount(_island, AnimalKind.Boar, Season.Winter));
    }

    /// <summary>V zimě vlci loví už za soumraku (v 18 hodin), jindy až v noci.</summary>
    [Fact]
    public void HungryWolvesHuntAtDuskInWinter()
    {
        Assert.True(Animals.WolvesHunt(Clock(hour: 18, month: 10)));
        Assert.False(Animals.WolvesHunt(Clock(hour: 18, month: 4)));
        Assert.True(Animals.WolvesHunt(Clock(hour: 23, month: 4)));
    }

    /// <summary>V létě oheň chrání hráče 3 m od něj, v zimě se hladový vlk odváží blíž.</summary>
    [Fact]
    public void HungryWolvesComeCloserToFireInWinter()
    {
        Assert.False(WolfStalksNearFire(Clock(hour: 23, month: 4)), "V létě oheň vlka odradí.");
        Assert.True(WolfStalksNearFire(Clock(hour: 23, month: 10)), "V zimě se vlk odváží až k ohni.");
    }

    /// <summary>Na jaře se bažina rozleje do okolní nízké souše, v létě je zase suchá.</summary>
    [Fact]
    public void MarshFloodsInSpringAndDriesInSummer()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        island.Flood.Update(Clock(hour: 12, month: 1));
        var flooded = island.Flood.FloodedTiles().ToList();
        Assert.Equal(GameData.Seasons.Flood.MaxRange, island.Flood.Ring);
        Assert.NotEmpty(flooded);
        Assert.All(flooded, i => Assert.Contains(island.Tiles[i], new[] { TileType.Grass, TileType.Forest, TileType.Sand }));
        Assert.All(flooded, i => Assert.True(NearBog(island, i, GameData.Seasons.Flood.MaxRange)));

        island.Flood.Update(Clock(hour: 12, month: 2));
        Assert.True(island.Flood.FloodedTiles().Count() < flooded.Count, "Ke konci jara voda opadá.");

        var version = island.Flood.Version;
        island.Flood.Update(Clock(hour: 12, month: 5));
        Assert.Empty(island.Flood.FloodedTiles());
        Assert.NotEqual(version, island.Flood.Version);
    }

    /// <summary>Zatopená souš zpomalí jako bažina a chytají se tam pijavice.</summary>
    [Fact]
    public void FloodedLandSlowsAndHasLeeches()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var spring = Clock(hour: 12, month: 1);
        island.Flood.Update(spring);
        var tile = island.Flood.FloodedTiles().First();
        var (x, y) = (tile % island.Width + 0.5, tile / island.Width + 0.5);

        Assert.Equal(GameData.Movement.TerrainSpeed[TileType.Bog], Movement.TerrainSpeed(island, x, y));

        var c = new Character(1, new Mulberry32(1));
        var rng = new Mulberry32(4);
        for (var i = 0; i < 300 && c.Leeches == 0; i++)
        {
            Hazards.Update(c, island, new Fires(), x, y, spring, 1, rng);
        }

        Assert.True(c.Leeches > 0);

        island.Flood.Update(Clock(hour: 12, month: 5));
        Assert.Equal(GameData.Movement.TerrainSpeed[island.Tiles[tile]], Movement.TerrainSpeed(island, x, y));
    }

    /// <summary>Kdo jde bažinou, nechává stopy (krok po kroku, se směrem). Na suché trávě ne.</summary>
    [Fact]
    public void WalkingThroughMudLeavesTracks()
    {
        var tracks = new Tracks();
        var (bx, by) = BogRow(_island);
        var walker = new AnimalTarget(1, bx, by, new Character(1, new Mulberry32(1)));
        for (var step = 0; step <= 8; step++)
        {
            walker = walker with { X = bx + step * 0.5 };
            tracks.Update(_island, [walker], [], 0.1, WeatherKind.Clear);
        }

        Assert.InRange(tracks.All.Count, 3, 5);
        Assert.All(tracks.All, t => Assert.Equal(TrackMaker.Human, t.Maker));
        Assert.All(tracks.All, t => Assert.Equal(0, t.Dir, 3));

        var dry = new Tracks();
        var (gx, gy) = Dry(_island);
        for (var step = 0; step <= 8; step++)
        {
            dry.Update(_island, [walker with { X = gx + step * 0.5, Y = gy }], [], 0.1, WeatherKind.Clear);
        }

        Assert.Empty(dry.All);
    }

    /// <summary>Stopy časem zmizí, v dešti mnohem dřív. Stopy zvířat poznáš podle toho, kdo je udělal.</summary>
    [Fact]
    public void RainWashesTracksAway()
    {
        var rules = GameData.Seasons.Tracks;
        var (bx, by) = BogRow(_island);
        var animals = new Animals(1);
        var boar = animals.Place(AnimalKind.Boar, bx, by);
        var tracks = new Tracks();
        tracks.Update(_island, [], animals.All, 0, WeatherKind.Clear);
        boar.X += 1;
        tracks.Update(_island, [], animals.All, 0, WeatherKind.Clear);
        Assert.Equal(TrackMaker.Boar, Assert.Single(tracks.All).Maker);

        tracks.Update(_island, [], animals.All, rules.LifeMinutes / 2, WeatherKind.Clear);
        Assert.Single(tracks.All);
        tracks.Update(_island, [], animals.All, rules.LifeMinutes / rules.RainFactor, WeatherKind.Rain);
        Assert.Empty(tracks.All);
    }

    private static bool WolfStalksNearFire(GameClock clock)
    {
        var (px, py) = OpenSpot();
        var fires = new Fires();
        var materials = new Inventory(10);
        for (var i = 0; i < 3; i++)
        {
            materials.TryAdd(Item.Stick);
        }

        materials.TryAdd(Item.Stone);
        Assert.Equal(FireResult.Lit, fires.Light(_island, materials, px, py, px + 1, py));

        // Hráč 3 m od ohně, vlk 6 m od hráče.
        var player = new AnimalTarget(1, px + 4, py, new Character(1, new Mulberry32(1)));
        var animals = new Animals(1);
        var wolf = animals.Place(AnimalKind.Wolf, px + 4, py + 6);
        for (var i = 0; i < 40; i++)
        {
            animals.Update(_island, fires, new PlantStates(), clock, 0.05, [player]);
        }

        return wolf.State != AnimalState.Wander;
    }

    private static bool NearBog(Island island, int tile, int range)
    {
        var (x, y) = (tile % island.Width, tile / island.Width);
        for (var dy = -range; dy <= range; dy++)
        {
            for (var dx = -range; dx <= range; dx++)
            {
                if (island.InBounds(x + dx, y + dy) && island.TileAt(x + dx, y + dy) is TileType.Bog or TileType.Pond)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Začátek řady aspoň pěti dlaždic bažiny ve směru osy x.</summary>
    private static (double X, double Y) BogRow(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (Enumerable.Range(0, 5).All(k => island.InBounds(x + k, y) && island.TileAt(x + k, y) == TileType.Bog))
            {
                return (x + 0.2, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není bažina.");
    }

    /// <summary>Začátek řady pěti travnatých dlaždic daleko od bažiny (nezatopí se).</summary>
    private static (double X, double Y) Dry(Island island)
    {
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (Enumerable.Range(0, 5).All(k => island.InBounds(x + k, y) && island.TileAt(x + k, y) == TileType.Grass && !NearBog(island, island.Index(x + k, y), 6)))
            {
                return (x + 0.2, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není suchá louka.");
    }

    /// <summary>Střed travnaté dlaždice, kolem které je ve čtverci 13 × 13 volno.</summary>
    private static (double X, double Y) OpenSpot()
    {
        for (var i = 0; i < _island.Tiles.Length; i++)
        {
            var (x, y) = (i % _island.Width, i / _island.Width);
            if (_island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-6, 13).All(dy => Enumerable.Range(-6, 13).All(dx => !Movement.IsBlocked(_island, x + dx, y + dy))))
            {
                return (x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Na ostrově není dost volného místa.");
    }

    /// <summary>Hodiny v daný čas a měsíc kalendáře (jaro 0 až 2, léto 3 až 5, zima 9 až 11).</summary>
    private static GameClock Clock(double hour, double month)
    {
        var clock = GameClock.FromSettings(hour * 60);
        clock.Restore(hour * 60, month + 0.5);
        return clock;
    }
}

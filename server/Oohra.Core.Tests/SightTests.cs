using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Přes zeď se nedá udeřit zvíře ani sebrat věc ze země (Sight).</summary>
public class SightTests
{
    private static (Island Island, double X, double Y) WithWallBetween()
    {
        var island = WorldGenerator.Build(12345, 128, 128);
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] == TileType.Grass
                && Enumerable.Range(-3, 7).All(dy => Enumerable.Range(-3, 7).All(dx => !Movement.IsBlocked(island, x + dx, y + dy))))
            {
                // Svislá zeď (úhel 90°) těsně na východ od postavy.
                var sticks = new Inventory(10);
                for (var s = 0; s < 4; s++) sticks.TryAdd(Item.Stick);
                var result = island.Structures.Build(island, new Fires(), sticks, x + 0.5, y + 0.5, BuildKind.StickWall, x + 1.1, y + 0.5, Math.PI / 2, 1.0, []);
                Assert.Equal(BuildResult.Built, result);
                return (island, x + 0.5, y + 0.5);
            }
        }

        throw new InvalidOperationException("Volné místo nenalezeno.");
    }

    [Fact]
    public void WallBlocksSightButOpenGroundDoesNot()
    {
        var (island, x, y) = WithWallBetween();

        Assert.False(Sight.Clear(island, x, y, x + 1.3, y));
        Assert.True(Sight.Clear(island, x, y, x - 1.3, y));
        Assert.True(Sight.Clear(island, x, y, x, y + 1.3));
    }

    [Fact]
    public void NoHittingAnimalThroughWall()
    {
        var (island, x, y) = WithWallBetween();
        var animals = new Animals(1);
        var boar = animals.Place(AnimalKind.Boar, x + 1.3, y);
        var hunter = new AnimalTarget(1, x, y, new Character(0, new Mulberry32(1)));

        Assert.Equal(HuntResult.Blocked, animals.Attack(boar.Id, hunter, new Inventory(10), 0, island));

        var other = animals.Place(AnimalKind.Boar, x - 1.3, y);
        Assert.Equal(HuntResult.HitFist, animals.Attack(other.Id, hunter, new Inventory(10), 0, island));
    }

    [Fact]
    public void NoPickingUpThroughWall()
    {
        var (island, x, y) = WithWallBetween();
        var behind = island.Ground.PutAt(island, Item.Stone, 2, x + 1.3, y, 0, 0);

        Assert.Equal(GroundResult.Blocked, island.Ground.PickUp(island, behind.Id, new Inventory(10), x, y, 0));
    }
}

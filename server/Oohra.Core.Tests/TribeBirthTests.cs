using Oohra.Core.Data;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Narození u kmene (návrh 4.1): okraj území kmene, mimo domy.</summary>
public class TribeBirthTests
{
    [Fact]
    public void BornAtEdgeOfTribeHomeOutsideHouses()
    {
        var island = IslandGenerator.Build(12345, 84, 84);
        var home = SpawnPoint.PickWild(island, new Mulberry32(3), firstLife: true);
        island.Structures.Place(BuildKind.Stockpile, home.X, home.Y, 0, ownerId: 1);
        var rules = GameData.Tribes.Birth;

        for (var seed = 1; seed <= 20; seed++)
        {
            var birth = SpawnPoint.PickNearTribe(island, new Mulberry32(seed), [home]);

            Assert.NotNull(birth);
            var distance = Distance(birth.Value, home);
            Assert.InRange(distance, rules.MinDistance, rules.MaxDistance);
            Assert.True(Movement.IsWalkable(island, birth.Value.X, birth.Value.Y, GameData.Movement.Spawn.Clearance));
            Assert.Empty(island.Structures.Near(birth.Value.X, birth.Value.Y, rules.MinDistance));
        }
    }

    [Fact]
    public void NoHomeMeansNoPlace()
    {
        var island = IslandGenerator.Build(12345, 84, 84);

        Assert.Null(SpawnPoint.PickNearTribe(island, new Mulberry32(1), []));
    }

    /// <summary>Uprostřed moře se narodit nedá: místo se nenajde a hráč se narodí v divočině.</summary>
    [Fact]
    public void NoPlaceInTheSea()
    {
        var island = IslandGenerator.Build(12345, 84, 84);

        Assert.Null(SpawnPoint.PickNearTribe(island, new Mulberry32(1), [new Position(-40, -40)]));
    }

    private static double Distance(Position a, Position b)
    {
        return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }
}

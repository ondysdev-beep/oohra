using Oohra.Core.Data;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Spánek a dovednosti (prototyp toggleSleep, endSleep, practice, skillLvl a consolidate).</summary>
public class SleepSkillTests
{
    private static readonly Island _island = WorldGenerator.Build(12345, 128, 128);

    [Fact]
    public void SleepOnlyWhenTiredAndWarm()
    {
        var clock = TestPlants.Clock();
        var rested = new Character(0, new Mulberry32(1)) { Energy = 90 };
        Assert.False(rested.TryFallAsleep(clock.TotalMinutes));
        Assert.Contains(rested.TakeMessages(), m => m.Event == BodyEvent.NotTired);

        var cold = new Character(0, new Mulberry32(1)) { Energy = 50, Warmth = 15 };
        Assert.False(cold.TryFallAsleep(clock.TotalMinutes));
        Assert.Contains(cold.TakeMessages(), m => m.Event == BodyEvent.TooColdToSleep);

        var tired = new Character(0, new Mulberry32(1)) { Energy = 50 };
        Assert.True(tired.TryFallAsleep(clock.TotalMinutes));
        Assert.True(tired.Sleeping);
    }

    [Fact]
    public void SleepRestoresEnergyAndWakesRested()
    {
        var clock = TestPlants.Clock();
        var c = new Character(0, new Mulberry32(1)) { Energy = 30, Satiety = 100, Hydration = 100 };
        c.TryFallAsleep(clock.TotalMinutes);

        for (var minute = 0; minute < 600 && c.Sleeping; minute++)
        {
            Physiology.Run(c, clock, 1);
        }

        Assert.False(c.Sleeping);
        Assert.True(c.Energy > 88, $"Síla po spánku {c.Energy:0.0}");
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.WokeRested);
    }

    [Fact]
    public void SleepSlowsHungerAndThirst()
    {
        var awake = new Character(0, new Mulberry32(1)) { Energy = 20 };
        var asleep = new Character(0, new Mulberry32(1)) { Energy = 20 };
        asleep.TryFallAsleep(0);

        Physiology.Run(awake, TestPlants.Clock(), 60);
        Physiology.Run(asleep, TestPlants.Clock(), 60);

        Assert.True(asleep.Sleeping);
        Assert.True(asleep.Hydration > awake.Hydration);
        Assert.True(asleep.Satiety > awake.Satiety);
        Assert.True(asleep.Energy > awake.Energy + 15);
    }

    /// <summary>V zimní noci venku teplo klesne a postava se zimou probudí.</summary>
    [Fact]
    public void ColdWakesUp()
    {
        var clock = TestPlants.Clock();
        clock.Advance(9 * 32400 + clock.RealSecondsFor(13 * 60));
        var c = new Character(3, new Mulberry32(1)) { Energy = 30, Warmth = 30, Satiety = 100, Hydration = 100 };
        Assert.True(c.TryFallAsleep(clock.TotalMinutes));

        Physiology.Run(c, clock, 600);

        Assert.False(c.Sleeping);
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.WokeCold);
    }

    /// <summary>Nácvik se utvrdí až po spánku delším než 60 minut. Krátký spánek nestačí.</summary>
    [Fact]
    public void SkillsSettleOnlyAfterLongEnoughSleep()
    {
        var c = new Character(0, new Mulberry32(1)) { Energy = 50 };
        c.Practice(SkillKind.Gather, 8);
        Assert.Equal(0, c.Skills.Level(SkillKind.Gather));

        c.TryFallAsleep(0);
        c.SleepMinutes = 30;
        c.WakeUp(30, BodyEvent.WokeUp);
        Assert.Equal(0, c.Skills.Level(SkillKind.Gather));
        Assert.Equal(8, c.Skills.Pending(SkillKind.Gather), 9);

        c.TryFallAsleep(100);
        c.SleepMinutes = 61;
        c.WakeUp(161, BodyEvent.WokeUp);

        Assert.Equal(2, c.Skills.Level(SkillKind.Gather));
        Assert.Equal(0, c.Skills.Pending(SkillKind.Gather), 9);
        Assert.Contains(c.TakeMessages(), m => m.Event == BodyEvent.SkillLevelUp && m.Skill == SkillKind.Gather && m.Level == 2);
    }

    [Fact]
    public void SmallPracticeSettlesWithoutLevelUp()
    {
        var c = new Character(0, new Mulberry32(1)) { Energy = 50 };
        c.Practice(SkillKind.Build, 1);
        c.TryFallAsleep(0);
        c.SleepMinutes = 70;
        c.WakeUp(70, BodyEvent.WokeUp);

        var messages = c.TakeMessages();
        Assert.Contains(messages, m => m.Event == BodyEvent.LearnedInSleep);
        Assert.DoesNotContain(messages, m => m.Event == BodyEvent.SkillLevelUp);
    }

    [Theory]
    [InlineData(1.9, 0)]
    [InlineData(2, 1)]
    [InlineData(8, 2)]
    [InlineData(18, 3)]
    [InlineData(50, 5)]
    [InlineData(500, 5)]
    public void LevelIsSquareRootOfHalfPoints(double points, int level)
    {
        Assert.Equal(level, Trained(SkillKind.Craft, points).Skills.Level(SkillKind.Craft));
    }

    [Fact]
    public void NewGenerationsLearnFaster()
    {
        var ancient = new Character(0, new Mulberry32(1));
        var newer = new Character(3, new Mulberry32(1));

        ancient.Practice(SkillKind.Gather, 1);
        newer.Practice(SkillKind.Gather, 1);

        Assert.Equal(1, ancient.Skills.Pending(SkillKind.Gather), 9);
        Assert.Equal(1.24, newer.Skills.Pending(SkillKind.Gather), 9);
    }

    [Fact]
    public void HarvestPracticesGathering()
    {
        var c = new Character(0, new Mulberry32(1));
        var plant = _island.Plants[0];

        Gathering.Harvest(_island, new PlantStates(), plant, PlantPart.Root, TestPlants.Clock(), new Inventory(10), plant.X, plant.Y, c);

        Assert.Equal(1.2, c.Skills.Pending(SkillKind.Gather), 9);
    }

    /// <summary>Zkušený sběrač (úroveň 5, šance 60 %) občas utrhne list navíc.</summary>
    [Fact]
    public void SkilledGathererSometimesGetsExtraLeaf()
    {
        var c = Trained(SkillKind.Gather, 50);
        var inventory = new Inventory(13);
        var states = new PlantStates();
        var clock = TestPlants.Clock();
        var picked = 0;
        foreach (var plant in _island.Plants.Take(30))
        {
            if (Gathering.Harvest(_island, states, plant, PlantPart.Leaf, clock, inventory, plant.X, plant.Y, c) == HarvestResult.Picked)
            {
                picked++;
            }
        }

        Assert.True(inventory.Stacks.Sum(s => s.Count) > picked);
    }

    [Fact]
    public void SkilledCrafterNeedsFewerSticksForFire()
    {
        Assert.Equal(3, Fires.FireSticks(new Character(0, new Mulberry32(1))));
        Assert.Equal(2, Fires.FireSticks(Trained(SkillKind.Craft, 18)));
    }

    [Fact]
    public void SkilledBuilderNeedsFewerSticksForWall()
    {
        Assert.Equal(2, Structures.SticksCost(BuildKind.StickWall, 1.0, new Character(0, new Mulberry32(1))));
        Assert.Equal(1, Structures.SticksCost(BuildKind.StickWall, 1.0, Trained(SkillKind.Build, 18)));
        Assert.Equal(GameData.Building.DoorFrame.SticksCost, Structures.SticksCost(BuildKind.DoorFrame, 1.0, Trained(SkillKind.Build, 18)));
        // Plná zeď (2 m) za 4 klacky, zkušený stavitel za 3.
        Assert.Equal(4, Structures.SticksCost(BuildKind.StickWall, GameData.Building.Wall.Length));
        Assert.Equal(3, Structures.SticksCost(BuildKind.StickWall, GameData.Building.Wall.Length, Trained(SkillKind.Build, 18)));
    }

    /// <summary>Postava s utvrzenými body dovednosti (Pradávný, učí se tempem 1).</summary>
    private static Character Trained(SkillKind skill, double points)
    {
        var c = new Character(0, new Mulberry32(1));
        c.Skills.Practice(skill, points, 1);
        c.Skills.Consolidate(out _);
        return c;
    }
}

using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>
/// Sběr rostlin (prototyp pluck v part5.js): list doroste za necelý půlden, plody za den,
/// vykopáním kořene rostlina na několik dní zmizí.
/// </summary>
public static class Gathering
{
    public static bool InReach(PlantInstance plant, double x, double y)
    {
        return Distance(plant.X, plant.Y, x, y) <= GameData.Gathering.Reach.Plant;
    }

    /// <summary>Zralost plodů na rostlině teď, nebo null (druh neplodí, není sezóna, plody už někdo utrhl).</summary>
    public static Ripeness? FruitStage(Island island, PlantStates states, PlantInstance plant, GameClock clock)
    {
        var secret = island.SpeciesSecrets[plant.SpeciesId];
        if (!GameData.Archetypes[secret.ArchetypeIndex].HasFruit || states.IsGone(plant.Id, clock.TotalMinutes))
        {
            return null;
        }

        var state = states.Find(plant.Id);
        if (state is not null && state.FruitAt > clock.TotalMinutes)
        {
            return null;
        }

        return FruitCalendar.Stage(island.Species[plant.SpeciesId], plant.FruitOffset, clock.CalendarMonths, GameData.Settings.MonthsPerYear);
    }

    /// <summary>Utrhne list nebo plod, nebo vykope kořen a dá ho do batohu.</summary>
    public static HarvestResult Harvest(
        Island island,
        PlantStates states,
        PlantInstance plant,
        PlantPart part,
        GameClock clock,
        Inventory inventory,
        double playerX,
        double playerY,
        Character? picker = null)
    {
        var now = clock.TotalMinutes;
        if (!InReach(plant, playerX, playerY))
        {
            return HarvestResult.TooFar;
        }

        if (states.IsGone(plant.Id, now))
        {
            return HarvestResult.PlantGone;
        }

        var regrow = GameData.Gathering.Regrow;
        var state = states.For(plant.Id);
        var ripeness = Ripeness.Ripe;
        switch (part)
        {
            case PlantPart.Leaf when state.LeavesAt > now:
                return HarvestResult.NoLeavesLeft;
            case PlantPart.Fruit:
                var stage = FruitStage(island, states, plant, clock);
                if (stage is null)
                {
                    return HarvestResult.NoFruit;
                }

                ripeness = stage.Value;
                break;
        }

        // Plod si pamatuje den utržení (časem se zkazí). Listy a kořeny se nekazí.
        var item = new PlantItem(plant.SpeciesId, part, ripeness, clock.Season, plant.Tile, Day: part == PlantPart.Fruit ? clock.Day : 0);
        if (!inventory.TryAdd(item))
        {
            return HarvestResult.InventoryFull;
        }

        switch (part)
        {
            case PlantPart.Leaf:
                state.LeavesAt = now + regrow.LeafMinutes;
                break;
            case PlantPart.Fruit:
                state.FruitAt = now + regrow.FruitMinutes;
                break;
            case PlantPart.Root:
                state.GoneUntil = now + regrow.RootMinutes;
                break;
        }

        if (picker is not null)
        {
            PracticeAndBonus(picker, part, item, inventory);
        }

        return HarvestResult.Picked;
    }

    /// <summary>
    /// Sbírání se procvičí (prototyp practice('gather')). Zkušený sběrač utrhne s nějakou šancí list navíc
    /// (extraLeafChancePerLevel v skills.json za každou úroveň).
    /// </summary>
    private static void PracticeAndBonus(Character picker, PlantPart part, PlantItem item, Inventory inventory)
    {
        var rules = GameData.Skills;
        picker.Practice(SkillKind.Gather, part switch
        {
            PlantPart.Leaf => rules.Practice.Leaf,
            PlantPart.Fruit => rules.Practice.Fruit,
            _ => rules.Practice.Root,
        });

        var chance = rules.Effects.ExtraLeafChancePerLevel * picker.Skills.Level(SkillKind.Gather);
        if (part == PlantPart.Leaf && chance > 0 && picker.Rng.NextDouble() < chance)
        {
            inventory.TryAdd(item);
        }
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

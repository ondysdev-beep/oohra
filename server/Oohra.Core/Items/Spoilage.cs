using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Time;

namespace Oohra.Core.Items;

/// <summary>
/// Kažení jídla (prototyp spoiled v part2.js). Maso, ryba a plody se po několika dnech od získání zkazí,
/// upečené vydrží déle, uzené a sušené (MeatRacks) nejdéle. Syrová kůže zahnije (crafting.json). Listy a kořeny se nekazí. Stáří se měří od poloviny dne, kdy byla věc získaná.
/// </summary>
public static class Spoilage
{
    public static bool IsSpoiled(Item item, double now)
    {
        var rules = GameData.Body.Spoilage;
        if (item.IsFlesh)
        {
            var days = item.Cooking switch
            {
                Cooking.Raw => rules.MeatRawDays,
                Cooking.Smoked => rules.MeatSmokedDays,
                Cooking.Dried => rules.MeatDriedDays,
                _ => rules.MeatBakedDays,
            };
            return AgeDays(item.Day, now) > days;
        }

        if (item.Kind == ItemKind.Hide)
        {
            return AgeDays(item.Day, now) > GameData.Crafting.HideRotDays;
        }

        if (item.Plant is { Part: PlantPart.Fruit } fruit)
        {
            return AgeDays(fruit.Day, now) > (fruit.Cooking == Cooking.Raw ? rules.FruitRawDays : rules.FruitBakedDays);
        }

        return false;
    }

    private static double AgeDays(int day, double now)
    {
        return now / GameClock.MinutesPerDay - (day + 0.5);
    }
}

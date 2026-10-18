using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Time;

namespace Oohra.Core.Items;

/// <summary>
/// Kažení jídla (prototyp spoiled v part2.js). Maso, ryba a plody se po několika dnech od získání zkazí,
/// upečené vydrží déle. Syrová kůže zahnije (crafting.json). Listy a kořeny se nekazí. Stáří se měří od poloviny dne, kdy byla věc získaná.
/// </summary>
public static class Spoilage
{
    public static bool IsSpoiled(Item item, double now)
    {
        var rules = GameData.Body.Spoilage;
        if (item.IsFlesh)
        {
            return AgeDays(item.Day, now) > (item.Cooking == Cooking.Raw ? rules.MeatRawDays : rules.MeatBakedDays);
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

using Oohra.Core.Data;

namespace Oohra.Core.Plants;

/// <summary>Kdy rostlina plodí a jak zralé jsou plody (prototyp fruitStage v part2.js).</summary>
public static class FruitCalendar
{
    /// <summary>
    /// Zralost plodů v daný okamžik kalendáře, nebo null, když rostlina zrovna plody nemá.
    /// Platí jen pro druhy, které plodí (to rozhoduje archetyp).
    /// </summary>
    public static Ripeness? Stage(PlantSpecies species, int fruitOffset, double calendarMonths, int monthsPerYear)
    {
        var sinceStart = ((calendarMonths + fruitOffset - species.StartMonth) % monthsPerYear + monthsPerYear) % monthsPerYear;
        var ends = GameData.PlantRules.FruitStageEndMonths;
        if (sinceStart < ends[0])
        {
            return Ripeness.Unripe;
        }

        if (sinceStart < ends[1])
        {
            return Ripeness.Ripe;
        }

        if (sinceStart < ends[2])
        {
            return Ripeness.Overripe;
        }

        return null;
    }
}

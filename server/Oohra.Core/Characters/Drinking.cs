using Oohra.Core.Data;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Characters;

/// <summary>
/// Pití přímo u zdroje (prototyp drinkKind v part5.js): jezírko je čisté, bažina kalná
/// (může z ní být nemoc), moře slané (žízeň zhorší). Nádoby a převařování přijdou později.
/// </summary>
public static class Drinking
{
    public static DrinkResult Drink(Character c, Island island, int tileX, int tileY, double playerX, double playerY, GameClock clock)
    {
        if (!island.InBounds(tileX, tileY))
        {
            return DrinkResult.NotWater;
        }

        var dx = tileX + 0.5 - playerX;
        var dy = tileY + 0.5 - playerY;
        var rules = GameData.Gathering.Water;
        if (Math.Sqrt(dx * dx + dy * dy) > GameData.Gathering.Reach.Water)
        {
            return DrinkResult.TooFar;
        }

        switch (island.TileAt(tileX, tileY))
        {
            case TileType.Pond:
            case TileType.Ford:
                c.Hydration = Math.Min(100, c.Hydration + rules.CleanHydration);
                return DrinkResult.Clean;
            case TileType.Bog:
                return DrinkMuddy(c, clock.TotalMinutes, rules);
            case TileType.Sea:
                c.Hydration = Math.Max(0, c.Hydration + rules.SaltyHydration);
                return DrinkResult.Salty;
            default:
                return DrinkResult.NotWater;
        }
    }

    public static bool IsWater(TileType tile)
    {
        return tile is TileType.Pond or TileType.Ford or TileType.Bog or TileType.Sea;
    }

    private static DrinkResult DrinkMuddy(Character c, double now, WaterRules rules)
    {
        var instinct = c.Traits.Generation >= rules.InstinctFromGeneration;
        c.Hydration = Math.Min(100, c.Hydration + (instinct ? rules.MuddyHydrationInstinct : rules.MuddyHydration));

        var chance = instinct ? rules.SicknessChanceInstinct : rules.SicknessChance;
        if (c.Rng.NextDouble() < chance)
        {
            var delay = rules.SicknessDelayMin + c.Rng.NextDouble() * rules.SicknessDelayRange;
            var strength = rules.SicknessStrengthMin + c.Rng.NextDouble() * rules.SicknessStrengthRange;
            c.Schedule(now + delay, EffectType.Sickness, strength);
            if (c.Rng.NextDouble() < rules.DiarrheaChance)
            {
                c.Schedule(now + rules.DiarrheaDelayMin + c.Rng.NextDouble() * rules.DiarrheaDelayRange, EffectType.Diarrhea, rules.DiarrheaStrength);
            }
        }

        return instinct ? DrinkResult.MuddySip : DrinkResult.Muddy;
    }
}

namespace Oohra.Core.Data;

/// <summary>Nebezpečné okolí a nemoci z Data/hazards.json (návrh 0.8, kap. 5).</summary>
public sealed record HazardRules(
    MarshFeverRules MarshFever,
    DysenteryRules Dysentery,
    double CureFactor,
    double BakedCureMultiplier,
    MosquitoRules Mosquitoes,
    double SmearMinutes,
    LeechRules Leeches,
    TickFeverRules TickFever,
    SnakeVenomRules SnakeVenom,
    TickRules Ticks,
    WaspRules Wasps);

public sealed record MarshFeverRules(
    double IncubationMin, double IncubationRange, double StrengthMin, double StrengthRange,
    double CycleMinutes, double FeverShare, double FeverFactor, double EnergyDrainPerMinute, double HealPerDay);

public sealed record DysenteryRules(
    double IncubationMin, double IncubationRange, double StrengthMin, double StrengthRange,
    double DiarrheaAbove, double HydrationPerMinute, double HealPerDay);

public sealed record MosquitoRules(
    double[] Seasons, double[] Dawn, double[] Dusk, double DayFactor, double NearWater,
    double BiteEveryMinutes, double InfectChance, double SmokeRange, double WarnEveryMinutes);

public sealed record LeechRules(double ChancePerMinute, int Max, double HealthPerLeechPerMinute);

public sealed record TickFeverRules(
    double IncubationMin, double IncubationRange, double StrengthMin, double StrengthRange,
    double FeverFactor, double Limp, double HealPerDay);

public sealed record SnakeVenomRules(
    double IncubationMin, double IncubationRange, double StrengthMin, double StrengthRange,
    double ToxinPerMinute, double Limp, double HealPerDay);

public sealed record TickRules(double[] Seasons, double ChancePerMinute, int Max, double TransmitMinutes, double InfectChance);

public sealed record WaspRules(
    double ChancePerTree, double AngerRange, double AngrySeconds, double ChaseRange,
    double StingSeconds, double StingToxin, double StingDamage, double SmokeReach, double RegrowDays);

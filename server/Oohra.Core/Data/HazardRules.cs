namespace Oohra.Core.Data;

/// <summary>Nebezpečné okolí a nemoci z Data/hazards.json (návrh 0.8, kap. 5).</summary>
public sealed record HazardRules(
    MarshFeverRules MarshFever,
    DysenteryRules Dysentery,
    double CureFactor,
    double BakedCureMultiplier,
    MosquitoRules Mosquitoes,
    double SmearMinutes,
    LeechRules Leeches);

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

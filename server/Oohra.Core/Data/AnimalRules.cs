namespace Oohra.Core.Data;

// Pravidla zvířat z Data/animals.json. Význam čísel je popsaný v komentářích v JSON.

public sealed record AnimalRules(
    PopulationRules Population,
    double RespawnMinutes,
    double CorpseMinutes,
    double CampDistance,
    double NightDaylightBelow,
    BoarRules Boar,
    WolfRules Wolf,
    PreyRules Rabbit,
    PreyRules Deer,
    HuntRules Hunt,
    WoundRules Wounds,
    MeatRules Meat);

public sealed record PopulationRules(int Boar, int Rabbit, int Deer, int Wolf, double ReferenceArea);

public sealed record BoarRules(
    int Hp, int Meat, double Radius, double SpawnAvoid,
    double WanderSpeed, double WanderRange,
    double AggroRange, double AggroSeconds, double AlertRange,
    double AlertSeconds, double AlertBonusPerHuntLevel,
    double ChargeSpeed, double ChargeSeconds, double ExhaustSeconds, double ExhaustAfterHitSeconds,
    double WoundMin, double WoundRange,
    double WakeSleeperRange);

public sealed record WolfRules(
    int Hp, int Meat, double Radius, double SpawnAvoid,
    double WanderSpeed, double DenRangeDay, double DenRangeNight,
    double StalkRange, double FireRange, double StalkSeconds, double StalkSecondsRandom,
    double StalkFastSpeed, double StalkSlowSpeed, double StalkSlowWithin, double GiveUpRange,
    double AlertRange, double AlertSeconds,
    double ChargeSpeed, double ChargeSeconds, double ExhaustSeconds, double ExhaustAfterHitSeconds,
    double WoundMin, double WoundRange);

/// <summary>Kořist (králík, jelen): utíká, unaví se, pase se. Jelen má navíc stádo.</summary>
public sealed record PreyRules(
    int Hp, int Meat, double Radius, double SpawnAvoid,
    double FleeRange, double FleeRangeAsleep, double FleeSpeed, double WalkSpeed,
    double Stamina, double TiredSeconds,
    double GrazeChance, double PlantSearch, double GrazeSeconds, double WanderRange,
    double HerdRange = 0, double HerdSpawnSpread = 0);

public sealed record HuntRules(
    double Reach, double CooldownSeconds,
    int FistDamage, int SpearDamage, int SpearBonusLevel,
    double ExhaustedMultiplier, double HitAggroSeconds, double ButcherReach,
    double PracticeHit, double PracticeSmall, double PracticeKill, double PracticeSpear);

public sealed record WoundRules(
    double HuntReductionPerLevel, double HealthFactor,
    double BleedPerSeverity, double MaxBleed, double BleedDecayPerMinute,
    double LimpPerSeverity, double MaxLimp, double LimpDecayPerMinute,
    double StunSeconds,
    double InfectionAbove, double InfectionChance, double InfectionDelayMin, double InfectionDelayRange, double InfectionFactor,
    double LimpSymptomAbove);

public sealed record MeatRules(double[] Raw, double[] Baked);

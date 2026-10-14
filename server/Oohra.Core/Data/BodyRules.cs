namespace Oohra.Core.Data;

// Pravidla těla z Data/body.json. Význam jednotlivých čísel je popsaný v komentářích v JSON.

public sealed record BodyRules(
    StartRules Start,
    NeedsRules Needs,
    WarmthRules Warmth,
    FeverRules Fever,
    RecoveryRules Recovery,
    DamageRules Damage,
    ProtectionRules FirstPoisonProtection,
    EatingRules Eating,
    EffectRules Effects,
    OutcomeRules Outcomes,
    InstinctRules Instinct,
    TasteRules Taste,
    SymptomRules Symptoms,
    SleepRules Sleep,
    SpoilageRules Spoilage);

public sealed record StartRules(double Satiety, double Hydration, double Energy, double Warmth, double Health);

public sealed record NeedsRules(
    double SatietyEmptyMinutes,
    double HydrationEmptyMinutes,
    double EnergyEmptyMinutes,
    double HotWarmthAbove,
    double HotThirstMultiplier,
    double FeverThirstAbove,
    double FeverThirstMultiplier,
    double DiarrheaThirstMultiplier,
    double StimulantEnergyDrainMultiplier);

public sealed record WarmthRules(
    double Base,
    double ComfortAmbient,
    double AmbientFactor,
    double ApproachPerMinute,
    double[] SeasonAmbient,
    double DaylightSwing);

public sealed record FeverRules(
    double ToxinAbove,
    double ToxinFactor,
    double InfectionFactor,
    double ApproachPerMinute,
    double NoticeAbove,
    double ResetBelow);

public sealed record RecoveryRules(
    double ToxinPerMinute,
    double InfectionPerMinute,
    double HealthPerMinute,
    double HealMinSatiety,
    double HealMinHydration,
    double HealMaxFever,
    double HealMaxToxin,
    double HealMinWarmth);

public sealed record DamageRules(
    double ThirstPerMinute,
    double HungerPerMinute,
    double ColdBelow,
    double ColdPerMinute,
    double ColdScale,
    double FeverAbove,
    double FeverPerMinute,
    double ToxinAbove,
    double ToxinFactor,
    double BlameHalfLifeMinutes);

public sealed record ProtectionRules(
    double HealthFloor,
    double MinHealthToProtect,
    double EpisodeToxinAbove,
    double EpisodeEndToxinBelow,
    double EpisodeEndFeverBelow);

public sealed record EatingRules(double EatDose, double TasteDose, double NutritionToSatiety);

public sealed record EffectRules(
    VomitEffect Vomit,
    DiarrheaEffect Diarrhea,
    ToxinEffect Toxin,
    NeurotoxinEffect Neurotoxin,
    StimulantEffect Stimulant,
    MedicineEffect Medicine);

public sealed record VomitEffect(double MinIrritant, double ChanceFactor, double DelayMin, double DelayRange, double MinStrength, double MaxStrength);

public sealed record DiarrheaEffect(double MinIrritant, double Chance, double DelayMin, double DelayRange, double MinStrength, double MaxStrength);

public sealed record ToxinEffect(double MinDose, double DelayMin, double DelayRange, double Factor);

public sealed record NeurotoxinEffect(double MinDose, double DelayMin, double DelayRange);

public sealed record StimulantEffect(double MinDose, double DurationFactor, double EnergyFactor, double CrashDelayMin, double CrashDelayRange);

public sealed record MedicineEffect(double MinDose, double FeverFactor, double ToxinFactor, double InfectionFactor);

public sealed record OutcomeRules(
    double VomitSatietyFactor,
    double VomitSatietyBase,
    double VomitHydration,
    double DiarrheaMinutesFactor,
    double HeavyFeelingAbove,
    double TremorMinutesFactor,
    double FaintAbove,
    double FaintMinutesFactor,
    double FaintDamageFactor,
    double SevereNeuroAbove,
    double SevereNeuroDamage,
    double CrashEnergyFactor,
    double CrashEnergyBase);

public sealed record InstinctRules(
    int FromGeneration,
    double WorryAbove,
    double NeurotoxinWeight,
    double IrritantAbove,
    double IrritantWeight,
    double BaseChance,
    double ChancePerGeneration,
    double MaxChance,
    double DoseKept);

public sealed record TasteRules(
    double SweetAbove,
    double BlandAbove,
    double BurnsAbove,
    double IrritatesAbove,
    double VeryBitterAbove,
    double VeryBitterChance,
    double VeryBitterChancePerGeneration,
    double VeryBitterMaxChance,
    double BitterAbove,
    double SharpnessBase,
    double SharpnessPerGeneration,
    double IrritatesBonus,
    double SharpnessMaxChance,
    double SoapyAbove,
    double SoapyChance,
    double FreshAbove,
    double HerbalAbove);

public sealed record SymptomRules(double HeavyBodyToxinAbove, double FeverAbove, double InflammationAbove);

public sealed record SleepRules(
    double MaxEnergyToSleep,
    double MinWarmthToSleep,
    double EnergyPerMinute,
    double HydrationMultiplier,
    double SatietyMultiplier,
    double HealMultiplier,
    double WarmthBonus,
    double WakeEnergyInDaylight,
    double WakeDaylightAbove,
    double WakeWarmthBelow);

public sealed record SpoilageRules(
    double MeatRawDays,
    double MeatBakedDays,
    double FruitRawDays,
    double FruitBakedDays,
    double NutritionFactor,
    double SicknessChance,
    double SicknessChanceTaste,
    double SicknessDelayMin,
    double SicknessDelayRange,
    double SicknessMin,
    double SicknessRange,
    double TasteFactor,
    double VomitChance,
    double VomitDelayMin,
    double VomitDelayRange,
    double VomitStrength);

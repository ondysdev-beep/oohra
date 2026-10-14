namespace Oohra.Core.Data;

/// <summary>Pravidla cizích kmenů (NPC) z Data/strangers.json.</summary>
public sealed record StrangerRules(
    int PerTribe,
    double RingRadius,
    double WalkSpeed,
    double FleeSpeed,
    double IdleMinSeconds,
    double IdleMaxSeconds,
    double WanderRadius,
    double StuckSeconds,
    double GotoEatSeconds,
    double GotoDrinkSeconds,
    double EatSeconds,
    double DrinkSeconds,
    double FleeSeconds,
    double EatChance,
    double DrinkChance,
    double EatSearchRadius,
    double PondSearchRadius,
    double DangerDistance,
    double PlayerDangerDistance,
    double PlayerNearDistance,
    double TalkDistance,
    double TalkCooldownSeconds,
    double AnswerDistance,
    double AnswerDelaySeconds,
    double AskDistance,
    double AskCooldownSeconds,
    double PointPondRadius,
    double PointPoisonRadius,
    double GiveDistance,
    double GiftEveryDays,
    double GiveBackEveryDays,
    int GiftCount,
    SafeFoodRules Safe,
    HarmfulRules Harmful,
    int SeedSalt);

/// <summary>Bezpečné jídlo pro cizince (prototyp bestSafePart).</summary>
public sealed record SafeFoodRules(double MinNutrition, double MaxIrritant, double MaxToxin, double MaxNeurotoxin);

/// <summary>Škodlivá věc (prototyp harmful): cizinec ji zahodí.</summary>
public sealed record HarmfulRules(double Irritant, double Toxin, double Neurotoxin);

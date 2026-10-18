namespace Oohra.Core.Data;

/// <summary>Kácení a dřevo z Data/woodcutting.json.</summary>
public sealed record WoodRules(
    AxeRules Axe,
    double Reach,
    double ChopCooldownSeconds,
    double WarnSeconds,
    double FallWidth,
    double FallSeverity,
    int FallAnimalDamage,
    double StumpHeight,
    double TrunkRadius,
    int SplintersPerWood,
    double CarrySpeed,
    double WoodBurnMultiplier);

public sealed record AxeRules(int Sticks, int Stones, int Uses);

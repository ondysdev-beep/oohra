namespace Oohra.Core.Data;

/// <summary>Kácení a dřevo z Data/woodcutting.json.</summary>
public sealed record WoodRules(
    AxeRules Axe,
    double Reach,
    double ChopCooldownSeconds,
    double CrackSeconds,
    double FallSeconds,
    CrownStickRules CrownSticks,
    double FallWidth,
    double FallSeverity,
    int FallAnimalDamage,
    double StumpHeight,
    double TrunkShare,
    double TrunkRadius,
    int SplintersPerWood,
    double CarrySpeed,
    double WoodBurnMultiplier)
{
    /// <summary>Od posledního seknutí do dopadu (praskání a pád).</summary>
    public double WarnSeconds => CrackSeconds + FallSeconds;
}

public sealed record AxeRules(int Sticks, int Stones, int Uses);

/// <summary>Klacky z koruny pokáceného stromu (na metr výšky, rozsah, suchý strom víc, kolik hromádek).</summary>
public sealed record CrownStickRules(double PerMeter, int Min, int Max, double DeadMultiplier, int Piles);

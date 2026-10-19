namespace Oohra.Core.Data;

/// <summary>Pravidla věcí na zemi z Data/ground.json.</summary>
public sealed record GroundRules(
    double DropDistance, double MergeRadius, int MaxPile, double Reach, double CraftReach, double LifetimeDays, double ViewDistance, int MaxSent);

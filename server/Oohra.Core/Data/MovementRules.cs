using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Pravidla pohybu z Data/movement.json.</summary>
public sealed record MovementRules(
    double WalkSpeed,
    double Radius,
    Dictionary<TileType, double> TerrainSpeed,
    BodySpeedRules Body,
    SpawnRules Spawn);

public sealed record BodySpeedRules(
    double ToxinAbove,
    double ToxinScale,
    double ToxinMaxSlowdown,
    double FeverAbove,
    double FeverScale,
    double FeverMaxSlowdown,
    double TiredEnergyBelow,
    double TiredSlowdown,
    double StimulantBonus,
    double MinMultiplier,
    double MaxMultiplier);

public sealed record SpawnRules(
    int Attempts,
    int SandOnlyAttempts,
    int NearPondAttempts,
    int PondRadius,
    double CampDistance,
    double Clearance);

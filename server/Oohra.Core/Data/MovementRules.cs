using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Pravidla pohybu z Data/movement.json.</summary>
public sealed record MovementRules(
    double WalkSpeed,
    double Radius,
    double TreeTrunkRadius,
    Dictionary<TileType, double> TerrainSpeed,
    SlopeRules Slope,
    BodySpeedRules Body,
    SpawnRules Spawn);

/// <summary>Svahy: zpomalení do kopce, zrychlení z kopce, nejmenší rychlost a nejstrmější svah, na který se vyleze.</summary>
public sealed record SlopeRules(double UphillSlowdown, double DownhillBoost, double MinSpeed, double MaxGrade);

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
    double LimpMaxSlowdown,
    double MinMultiplier,
    double MaxMultiplier);

public sealed record SpawnRules(
    int Attempts,
    int SandOnlyAttempts,
    int NearPondAttempts,
    int PondRadius,
    double CampDistance,
    double Clearance,
    double AvoidDistance,
    int AvoidAttempts,
    int MinOpenTiles);

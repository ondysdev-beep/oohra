using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Jeskyně z Data/caves.json (návrh 0.8, kap. 4).</summary>
public sealed record CaveRules(
    int Gap,
    int CellSize,
    int TilesPerCave,
    int MinCaves,
    int MaxCaves,
    List<CaveSizeRules> Sizes,
    double CorridorRadius,
    double SecondExitChance,
    double HiddenChance,
    double DiscoverRange,
    double EntranceSpacing,
    double CampDistance,
    double UseReach,
    double PoolChance,
    double FlintChance,
    double Temperature,
    double OccupiedChance,
    int MinDens,
    List<DenRules> Dens,
    CaveSmokeRules Smoke);

public sealed record DenRules(CaveSize Size, AnimalKind Kind, int Min, int Max);

public sealed record CaveSmokeRules(double HealthPerMinute, double EnergyPerMinute, double WarnEveryMinutes);

public sealed record CaveSizeRules(CaveSize Size, double Weight, int Rooms, double[] RoomRadius, double[] RoomStep);

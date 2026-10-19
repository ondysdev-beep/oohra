using Oohra.Core.World;

namespace Oohra.Core.Data;

/// <summary>Pravidla nového generátoru světa z Data/worldgen.json (návrh 0.8, kap. 1).</summary>
public sealed record WorldGenRules(
    double LandShare,
    double ShapeScale,
    double ShapeNoise,
    double DetailScale,
    int BeachWidth,
    RegionShares Shares,
    HighlandRules Highland,
    ForestRules Forest,
    double MoistureScale,
    double MarshWaterBonus,
    int MarshWaterDistance,
    LakeRules Lake,
    RiverRules River,
    PondRules Ponds,
    CampRules Camps,
    RuinRules Ruins,
    ShallowRules Shallows,
    TerrainRules Terrain,
    Dictionary<TileType, double> TreeChance,
    TreeClumpRules TreeClumps,
    Dictionary<TileType, double> PlantChance);

/// <summary>Podíl oblastí z celé souše (zbytek po pláži, vodě a ostatních oblastech je louka).</summary>
public sealed record RegionShares(double Highland, double Marsh, double Forest, double Lake);

/// <summary>Ve vrchovině: kolik je skal a kolik ze skal je vrstvená skála.</summary>
public sealed record HighlandRules(double RockShare, double LayeredShare, double RockScale, double LayeredScale);

/// <summary>Les se střídá s loukou podle šumu (regionScale), v lese jsou světliny (clearingShare).</summary>
public sealed record ForestRules(double RegionScale, double ClearingShare, double ClearingScale);

public sealed record LakeRules(int MinCoastDistance);

/// <summary>Řeka z vrchoviny do jezera a z jezera do moře. Šířka roste k ústí, brody po fordEvery metrech.</summary>
public sealed record RiverRules(double MinWidth, double MaxWidth, double WobbleScale, double Meander, int FordEvery, int FordSkipEnds);

public sealed record PondRules(int Count, double MinRadius, double MaxRadius);

public sealed record CampRules(int Count, int MinDistance, int WaterWithin);

public sealed record RuinRules(int Count, int MinDistance, int CampDistance);

/// <summary>Výška terénu: velikost a výška vlnění, výška vrchoviny, skalní hřbety, jak pozvolna se souš zvedá od vody.</summary>
public sealed record TerrainRules(double HillScale, double Lowland, double Highland, double RockBonus, double CoastRise, double Marsh, double Bank, int Smoothing);

/// <summary>Mělčiny u moře a jezera (šířka v dlaždicích) a jak hluboko v bažině začíná močál.</summary>
public sealed record ShallowRules(int SeaWidth, int LakeWidth, int MireDepth);

/// <summary>Shluky stromů a mýtiny: šum o velikosti skvrn Scale metrů násobí šanci na strom (Min až Max).</summary>
public sealed record TreeClumpRules(double Scale, double Min, double Max);

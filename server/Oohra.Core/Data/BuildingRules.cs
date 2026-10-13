namespace Oohra.Core.Data;

/// <summary>Pravidla staveb z Data/building.json.</summary>
public sealed record BuildingRules(WallRules Wall, ShelterRules Shelter);

public sealed record WallRules(int SticksCost, int SticksBack, double Reach);

public sealed record ShelterRules(int MaxTiles, double WarmthBonus);

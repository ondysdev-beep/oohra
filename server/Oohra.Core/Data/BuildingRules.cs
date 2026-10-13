namespace Oohra.Core.Data;

/// <summary>Pravidla staveb z Data/building.json.</summary>
public sealed record BuildingRules(double PlaceReach, WallRules Wall, ShelterRules Shelter);

public sealed record WallRules(double Length, double Thickness, int SticksCost, int SticksBack, double RemoveReach);

public sealed record ShelterRules(int Rays, double MaxDistance, double WarmthBonus);

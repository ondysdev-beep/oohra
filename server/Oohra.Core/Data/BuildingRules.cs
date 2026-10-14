namespace Oohra.Core.Data;

/// <summary>Pravidla staveb z Data/building.json.</summary>
public sealed record BuildingRules(double PlaceReach, WallRules Wall, DoorFrameRules DoorFrame, DoorRules Door, TrapRules Trap, ShelterRules Shelter);

public sealed record TrapRules(int SticksCost, int SticksBack, double FootprintRadius);

public sealed record WallRules(
    double Length, double Thickness, double MinLength, int SticksCost, double ShortMaxLength, int ShortSticksCost, double RemoveReach);

public sealed record DoorFrameRules(double OpeningWidth, int SticksCost, int SticksBack);

public sealed record DoorRules(double SnapDistance, int SticksCost, int SticksBack);

public sealed record ShelterRules(int Rays, double MaxDistance, double WarmthBonus);

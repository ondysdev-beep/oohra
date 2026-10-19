namespace Oohra.Core.Data;

/// <summary>Pravidla staveb z Data/building.json.</summary>
public sealed record BuildingRules(double PlaceReach, WallRules Wall, DoorFrameRules DoorFrame, DoorRules Door, TrapRules Trap, PostRules Post, FloorRules Floor, RoofRules Roof, LadderRules Ladder, StoreyRules Storey, List<RoofMaterialRules> RoofMaterials, double RepairShare, BuildingFireRules BuildingFire, BarricadeRules Barricade, StockpileRules Stockpile, ShelterRules Shelter, BirthGuardRules BirthGuard);

public sealed record PostRules(int SticksCost, int SticksBack, double Radius);

public sealed record RoofMaterialRules(World.RoofMaterial Material, int Sticks, Items.ItemKind Item, int Count, double Days);

public sealed record BuildingFireRules(
    double IgniteRange, double IgniteChancePerMinute, double RainFactor, double SpreadRange, double SpreadChancePerMinute,
    double BurnMinutes, double HeatRange, double HeatDamagePerMinute, double WarnEveryMinutes, string RingDecoration);

public sealed record LadderRules(int SticksCost, int SticksBack, double Reach, double FloorMargin);

public sealed record StoreyRules(double JumpDamage, double FallDamage);

public sealed record FloorRules(int SticksCost, int SticksBack, double Size);

public sealed record RoofRules(
    int SticksCost, int SticksBack, double Size, double SupportTolerance, double MinWallSupport, int MinSupports,
    double NeighbourDistance, double WarnSeconds, double Damage, double CheckSeconds);

/// <summary>Zátaras u vchodu do jeskyně.</summary>
public sealed record BarricadeRules(int SticksCost, int SticksBack, double EntranceReach);

/// <summary>Ochrana místa narození před cizí stavbou (návrh 4.1).</summary>
public sealed record BirthGuardRules(double Hours, double Radius);

public sealed record StockpileRules(int SticksCost, int SticksBack, int Capacity, double FootprintRadius, double Reach);

public sealed record TrapRules(int SticksCost, int SticksBack, double FootprintRadius);

public sealed record WallRules(
    double Length, double Thickness, double MinLength, int SticksCost, double ShortMaxLength, int ShortSticksCost, double RemoveReach);

public sealed record DoorFrameRules(double OpeningWidth, int SticksCost, int SticksBack);

public sealed record DoorRules(double SnapDistance, int SticksCost, int SticksBack);

public sealed record ShelterRules(int Rays, double MaxDistance, double WarmthBonus);

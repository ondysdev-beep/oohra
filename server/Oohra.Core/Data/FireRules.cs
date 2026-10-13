namespace Oohra.Core.Data;

/// <summary>Pravidla ohně a materiálu z Data/fire.json.</summary>
public sealed record FireRules(StickRules Sticks, MaterialReachRules Reach, CampfireRules Fire);

public sealed record StickRules(int PickingsPerTree, double RefreshMinutes, double ExtraStickChance);

public sealed record MaterialReachRules(double Tree, double Rock, double Fire);

public sealed record CampfireRules(
    int SticksCost,
    int StonesCost,
    double BurnMinutes,
    double FeedMinutes,
    double RelightMinutes,
    double WarmthRadius,
    double WarmthBonus,
    double CookRadius);

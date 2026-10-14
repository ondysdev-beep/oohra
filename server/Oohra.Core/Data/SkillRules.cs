namespace Oohra.Core.Data;

/// <summary>Pravidla dovedností z Data/skills.json.</summary>
public sealed record SkillRules(double LevelDivisor, int MaxLevel, double MinSleepMinutes, PracticeRules Practice, SkillEffectRules Effects);

public sealed record PracticeRules(double Leaf, double Fruit, double Root, double Fire, double Bake, double Wall, double DoorFrame, double Door);

public sealed record SkillEffectRules(
    double ExtraLeafChancePerLevel, int CheapFireLevel, int CheapFireSticks, int CheapWallLevel, int CheapWallSticks);

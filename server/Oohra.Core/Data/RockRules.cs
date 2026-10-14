namespace Oohra.Core.Data;

/// <summary>Pravidla hromad kamenů z Data/rocks.json.</summary>
public sealed record RockRules(double PileChance, int MinStones, int MaxStones, double Radius, double RegrowDays, int SeedSalt);

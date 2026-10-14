namespace Oohra.Core.Data;

/// <summary>Pravidla značek z Data/signs.json.</summary>
public sealed record SignRules(
    double DrawReach, double EraseReach, double MinSpacing, double WeatherDays, int MaxPerAuthor, RuinCarvingRules Ruins);

public sealed record RuinCarvingRules(
    int SeedSalt, double WrongChance, double DropChance,
    double FillingAbove, double BitterAbove, double BurnsAbove, double VomitAbove, double DiarrheaAbove,
    double FeverAbove, double TremorAbove, double FaintAbove, double StrengthenedAbove, double HealsAbove);

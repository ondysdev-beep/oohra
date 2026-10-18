namespace Oohra.Core.Data;

/// <summary>Pravidla počasí a pochodně z Data/weather.json.</summary>
public sealed record WeatherRules(
    double SegmentMinutes,
    double FadeShare,
    int SeedSalt,
    double FogMorning,
    double FogDay,
    double FogNight,
    double Rain,
    double StormShare,
    double[] SeasonRain,
    double[] SeasonFog,
    double RainWarmthPenalty,
    double StormWarmthPenalty,
    double RainBurnMultiplier,
    double StormBurnMultiplier,
    double TorchMinutes,
    double TorchWarnMinutes);

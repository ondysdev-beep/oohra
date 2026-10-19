namespace Oohra.Core.Data;

/// <summary>Sezónní nebezpečí a stopy z Data/seasons.json (návrh 0.8, kap. 5).</summary>
public sealed record SeasonRules(WinterWolfRules WinterWolves, FloodRules Flood, TrackRules Tracks);

public sealed record WinterWolfRules(double PopulationFactor, double HuntDaylightBelow, double FireRangeFactor, double RoamFactor);

public sealed record FloodRules(double[] MonthLevels, int MaxRange);

public sealed record TrackRules(double StepMeters, double LifeMinutes, double RainFactor, int MaxTracks, double ViewDistance, int MaxSent, double SendEverySeconds);

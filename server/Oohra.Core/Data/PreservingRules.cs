namespace Oohra.Core.Data;

/// <summary>Uzení a sušení masa z Data/preserving.json.</summary>
public sealed record PreservingRules(
    double SmokeMinutes, int FireSlots, double DryMinutes, double DryMinDaylight, int RackSlots, string RackDecoration, double Reach);

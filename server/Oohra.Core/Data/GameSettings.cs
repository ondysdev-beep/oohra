namespace Oohra.Core.Data;

/// <summary>Nastavení světa z Data/settings.json.</summary>
public sealed class GameSettings
{
    public required int WorldWidth { get; init; }

    public required int WorldHeight { get; init; }

    public required int MonthsPerYear { get; init; }
}

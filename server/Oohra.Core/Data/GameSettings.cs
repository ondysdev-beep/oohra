namespace Oohra.Core.Data;

/// <summary>Nastavení světa z Data/settings.json.</summary>
public sealed class GameSettings
{
    public required int WorldWidth { get; init; }

    public required int WorldHeight { get; init; }

    public required int MonthsPerYear { get; init; }

    public required int TicksPerSecond { get; init; }

    /// <summary>Kolik reálných sekund trvá jeden herní den (den a noc).</summary>
    public required double DayLengthSeconds { get; init; }

    /// <summary>Kolik reálných sekund trvá jeden měsíc kalendáře.</summary>
    public required double MonthLengthSeconds { get; init; }
}

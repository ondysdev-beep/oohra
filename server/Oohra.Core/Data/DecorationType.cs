namespace Oohra.Core.Data;

/// <summary>Druh dekorace z Data/decorations.json.</summary>
public sealed record DecorationType(string Id, string Name, int Sticks, int Stones, double Radius, bool Blocks);

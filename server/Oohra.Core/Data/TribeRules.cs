namespace Oohra.Core.Data;

/// <summary>Pravidla kmenů z Data/tribes.json.</summary>
public sealed record TribeRules(double InviteDistance, double InviteMinutes, TribeBirthRules Birth);

/// <summary>Narození u kmene (návrh 4.1): okraj území kmene, mimo domy.</summary>
public sealed record TribeBirthRules(double MinDistance, double MaxDistance, int Attempts);

using Oohra.Core.Language;

namespace Oohra.Core.Data;

/// <summary>Pravidla jazyka z Data/language.json.</summary>
public sealed record LanguageRules(
    double HearDistance, double ContextDistance, double SayCooldownSeconds, double GestureDistance,
    SpeechContextRules Context, CommonWordRules CommonWords);

public sealed record SpeechContextRules(
    double WaterDistance, double FireDistance, double AnimalDistance, double EatingMinutes, double GestureSeconds);

public sealed record CommonWordRules(int SeedSalt, IReadOnlyList<Concept> Concepts, IReadOnlyList<Concept> OldVoice);

namespace Oohra.Core.Characters;

/// <summary>Co se s tělem stalo. Klient si k tomu přiřadí text (v prototypu hlášky say(...)).</summary>
public enum BodyEvent
{
    /// <summary>Snědl rostlinu.</summary>
    Ate,

    /// <summary>Ochutnal, zpráva nese nápovědy chuti.</summary>
    Tasted,

    /// <summary>Instinkt: hořkost ho zastavila, polkl jen část.</summary>
    BitternessStopped,

    Vomited,
    Diarrhea,

    /// <summary>Velká dávka toxinu: „cítíš se divně, jako by tě něco zevnitř tížilo“.</summary>
    HeavyFeeling,

    Tremor,
    Fainted,

    /// <summary>Probral se z bezvědomí.</summary>
    CameTo,

    /// <summary>Povzbuzení odeznělo, přišla únava.</summary>
    Crash,

    /// <summary>Začala horečka („hoří ti čelo“).</summary>
    FeverStarted,

    Died,
}

/// <summary>Nápověda při ochutnání (prototyp tasteText).</summary>
public enum TasteNote
{
    Sweet,
    Bland,
    Tasteless,
    Burns,
    Irritates,
    VeryBitter,
    Bitter,
    Soapy,
    Fresh,
    Herbal,
}

/// <summary>Příčina smrti: to, co tělu ubralo nejvíc zdraví.</summary>
public enum DeathCause
{
    Hunger,
    Thirst,
    Cold,
    Fever,
    Infection,
    Toxin,
    Neurotoxin,
}

/// <summary>Viditelný příznak ve stavu těla.</summary>
public enum Symptom
{
    HeavyBody,
    Fever,
    Tremor,
    Agitated,
    Diarrhea,
    Inflammation,
    Unconscious,
}

/// <summary>Zpráva o těle s časem (herní minuty). U ochutnání nese nápovědy chuti.</summary>
public sealed record BodyMessage(double At, BodyEvent Event, IReadOnlyList<TasteNote>? Notes = null);

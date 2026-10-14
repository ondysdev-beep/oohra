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

    /// <summary>Nemoc z kalné vody: „je ti horko a špatně od žaludku“.</summary>
    FeltSick,

    Died,

    /// <summary>Usnul.</summary>
    FellAsleep,

    /// <summary>Probudil se odpočatý (síla je plná, nebo skoro plná a je den).</summary>
    WokeRested,

    /// <summary>Vstal sám (tlačítko nebo pohyb).</summary>
    WokeUp,

    /// <summary>Zimou nemůže spát, probudil se.</summary>
    WokeCold,

    /// <summary>Chtěl spát, ale není dost unavený.</summary>
    NotTired,

    /// <summary>Chtěl spát, ale je mu moc zima.</summary>
    TooColdToSleep,

    /// <summary>Ve spánku se uložilo, co dělal, ale žádná dovednost nestoupla.</summary>
    LearnedInSleep,

    /// <summary>Po spánku stoupla dovednost (zpráva nese dovednost a úroveň).</summary>
    SkillLevelUp,

    /// <summary>Divočák ho zasáhl klem.</summary>
    WoundedByBoar,

    /// <summary>Vlk ho pokousal.</summary>
    WoundedByWolf,

    /// <summary>Něco ho vzbudilo, poblíž je divočák.</summary>
    WokeByAnimal,

    /// <summary>Něco ho sleduje ze tmy (vlk se plíží). Oheň ho odradí.</summary>
    Stalked,

    /// <summary>Divočák se na něj zaměřil. Až se rozběhne, je čas uhnout.</summary>
    BoarAlert,

    /// <summary>Rána se zanítila (zánět, z něj horečka).</summary>
    WoundInflamed,

    /// <summary>Pradávný snědl zkažené jídlo a železný žaludek to zvládl.</summary>
    IronStomach,
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

    /// <summary>Zkažené: páchne to a chutná odporně.</summary>
    Rotten,
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

    /// <summary>Vykrvácel po zranění od zvířete.</summary>
    Bleeding,

    /// <summary>Zemřel na následky zranění od zvířete.</summary>
    Wound,
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
    Bleeding,
    Limping,
}

/// <summary>
/// Zpráva o těle s časem (herní minuty). U ochutnání nese nápovědy chuti,
/// u nové úrovně dovednosti dovednost a úroveň.
/// </summary>
public sealed record BodyMessage(double At, BodyEvent Event, IReadOnlyList<TasteNote>? Notes = null, SkillKind? Skill = null, int Level = 0);

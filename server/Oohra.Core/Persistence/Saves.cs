using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.World;

namespace Oohra.Core.Persistence;

// Uložený stav světa (soubor world.json na serveru). Jen data, žádná pravidla: co se mění během hry a musí přežít
// restart serveru. Ostrov sám (dlaždice, stromy, rostliny, druhy) se znovu vytvoří ze seedu, zvířata a cizinci
// se narodí znovu.

/// <summary>Postava se vším, co má v těle a v hlavě (skryté hodnoty, naplánované otravy, dovednosti, zápisník).</summary>
public sealed class CharacterSave
{
    public int Generation { get; init; }

    /// <summary>Stav generátoru náhody postavy (Mulberry32.State).</summary>
    public int RngState { get; init; }

    public double Satiety { get; init; }

    public double Hydration { get; init; }

    public double Energy { get; init; }

    public double Warmth { get; init; }

    public double Health { get; init; }

    public double LastAte { get; init; }

    public double WarmUntil { get; init; } = double.NegativeInfinity;

    public double ColdInstinctUntil { get; init; } = double.NegativeInfinity;

    public double LastColdInstinct { get; init; } = double.NegativeInfinity;

    public double TorchMinutes { get; init; }

    public bool TorchWarned { get; init; }

    public List<string> Recipes { get; init; } = [];

    public List<Disease> Diseases { get; init; } = [];

    public int Leeches { get; init; }

    public List<double> Ticks { get; init; } = [];

    public List<int> KnownCaves { get; init; } = [];

    public double SmearedUntil { get; init; } = double.NegativeInfinity;

    public double LastMosquitoWarning { get; init; } = double.NegativeInfinity;

    public double Toxin { get; init; }

    public double Fever { get; init; }

    public double Infection { get; init; }

    public double TremorMinutes { get; init; }

    public double FaintMinutes { get; init; }

    public double StimulantMinutes { get; init; }

    public double DiarrheaMinutes { get; init; }

    public bool PoisonProtected { get; init; }

    public bool InToxinEpisode { get; init; }

    public bool FeverNoticed { get; init; }

    public double BlameMinutes { get; init; }

    public bool IsDead { get; init; }

    public DeathCause? DeathCause { get; init; }

    public double Bleed { get; init; }

    public double Limp { get; init; }

    public double StunSeconds { get; init; }

    public bool Sleeping { get; init; }

    public double SleepMinutes { get; init; }

    /// <summary>Otravy a nemoci, které se teprve projeví.</summary>
    public List<ScheduledEffect> Scheduled { get; init; } = [];

    public Dictionary<DeathCause, double> Blame { get; init; } = [];

    public Dictionary<SkillKind, double> SkillPoints { get; init; } = [];

    public Dictionary<SkillKind, double> SkillPending { get; init; } = [];

    public List<WordSave> Words { get; init; } = [];

    public byte[]? LastHeard { get; init; }
}

/// <summary>Slovo v zápisníku.</summary>
public sealed record WordSave(byte[] Word, int Heard, int[] Contexts, Concept? Meaning, bool OldVoice);

/// <summary>Stavba (u koše i s obsahem, u pasti s úlovkem).</summary>
public sealed record StructureSave(
    int Id, BuildKind Kind, double X, double Y, double Angle, double Length, int FrameId, int OwnerId, string DecorationId,
    List<ItemStack>? Store, AnimalKind? Caught, int Level = 0, RoofMaterial Material = RoofMaterial.Branches, double RepairedAt = 0, double? BurningSince = null);

public sealed record FireSave(int Id, double X, double Y, double BurnMinutes, double Burned = 0, int AshDay = -1);

public sealed record PlantStateSave(int PlantId, double LeavesAt, double FruitAt, double GoneUntil);

public sealed record RockPileSave(int TileIndex, int Stones, double RegrowAt);

/// <summary>Maso v kouři ohně nebo na sušáku.</summary>
public sealed record RackSave(RackHolder Holder, int Id, List<RackPieceSave> Pieces);

public sealed record RackPieceSave(Item Item, double Progress);

/// <summary>Vykouřené vosí hnízdo: do kdy není.</summary>
public sealed record WaspNestSave(int TileIndex, double GoneUntil);

/// <summary>Značka hráče (staré rytiny u ruin se neukládají, vzniknou znovu ze seedu).</summary>
public sealed record SignSave(int Id, double X, double Y, byte[] Bits, int AuthorId, double CreatedAt, bool Carved, Signature? Signature, bool ByAncient);

public sealed record CorpseSave(int Id, double X, double Y, int OwnerId, int Generation, double DiedAt, int Capacity, List<ItemStack> Items, Signature? Signature);

/// <summary>Co se na ostrově změnilo: stavby, ohně, sklizené rostliny, kameny, značky, těla a chráněná místa narození.</summary>
public sealed class IslandSave
{
    public List<StructureSave> Structures { get; init; } = [];

    public List<FireSave> Fires { get; init; } = [];

    public int NextFireId { get; init; } = 1;

    public List<PlantStateSave> Plants { get; init; } = [];

    public List<RockPileSave> Rocks { get; init; } = [];

    public List<SignSave> Signs { get; init; } = [];

    public List<CorpseSave> Corpses { get; init; } = [];

    public int NextCorpseId { get; init; } = 1;

    public List<BirthGuard> BirthGuards { get; init; } = [];

    public ForestrySave Forestry { get; init; } = new();

    public List<WaspNestSave> Wasps { get; init; } = [];

    public List<RackSave> Racks { get; init; } = [];
}

/// <summary>Kácení: rozsekané stromy (počet seknutí), pařezy (kdy padly) a dřevo na zemi.</summary>
public sealed class ForestrySave
{
    public Dictionary<int, int> Hits { get; init; } = [];

    public Dictionary<int, double> Stumps { get; init; } = [];

    public List<TimberSave> Timbers { get; init; } = [];

    public int NextTimberId { get; init; } = 1;

    /// <summary>Mladé stromy (kdy zasazené, měsíce kalendáře) a dorostlé stromy (PlantedMonths se nepoužije).</summary>
    public List<SaplingSave> Saplings { get; init; } = [];

    public List<SaplingSave> Grown { get; init; } = [];
}

public sealed record SaplingSave(int TileIndex, TreeKind Kind, double Height, double PlantedMonths);

public sealed record TimberSave(int Id, TimberKind Kind, TreeKind Tree, double X, double Y, double Angle, int Length, bool Stripped = false);

/// <summary>Kmeny: kdo patří do kterého kmene.</summary>
public sealed class TribesSave
{
    public Dictionary<int, int> TribeOf { get; init; } = [];

    public int NextTribe { get; init; } = 1;
}

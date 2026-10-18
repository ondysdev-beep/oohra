namespace Oohra.Core.World;

/// <summary>
/// Oblast ostrova (návrh 0.8, kap. 1). Vzniká ze seedu podle pravidel. Jedna oblast má víc druhů dlaždic:
/// v lese jsou světliny s trávou, ve vrchovině skály i kamenitá půda.
/// </summary>
public enum RegionKind : byte
{
    Sea = 0,

    /// <summary>Pláž a pobřeží.</summary>
    Coast = 1,
    Forest = 2,
    Meadow = 3,

    /// <summary>Skalnatá vrchovina (skály, později jeskyně a pazourek).</summary>
    Highland = 4,

    /// <summary>Bažina: nízké vlhké místo u vody (komáři, pijavice, špatná voda).</summary>
    Marsh = 5,
    Lake = 6,
    River = 7,
}

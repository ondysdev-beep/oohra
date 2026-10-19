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

    /// <summary>Jeskyně v podzemí (tma, stálá teplota, neprší).</summary>
    Cave = 8,
}

/// <summary>Velikost jeskyně (návrh 0.8, kap. 4).</summary>
public enum CaveSize : byte
{
    /// <summary>Malá skrýš pro 1 až 3 lidi.</summary>
    Small = 0,

    /// <summary>Střední jeskyně pro 5 až 8 lidí.</summary>
    Medium = 1,

    /// <summary>Velká síť chodeb pro kmen.</summary>
    Large = 2,
}

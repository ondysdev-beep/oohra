namespace Oohra.Protocol;

/// <summary>Základní údaje o protokolu. Klient a server musí mít stejnou verzi.</summary>
public static class ProtocolInfo
{
    public const int Version = 22;

    public const int DefaultPort = 9050;

    /// <summary>Heslo spojení LiteNetLib (není to ochrana, jen odfiltruje cizí programy).</summary>
    public const string ConnectionKey = "oohra";
}

/// <summary>První bajt každé zprávy říká, co v ní je.</summary>
public enum MessageType : byte
{
    // Klient → server: úmysly hráče. Server rozhodne, co se opravdu stane.
    Login = 1,
    Input = 2,
    Harvest = 3,
    Drink = 4,
    Eat = 5,
    Taste = 6,
    Drop = 7,
    Reborn = 8,
    GatherSticks = 9,
    TakeStone = 10,
    FeedFire = 12,
    Bake = 13,
    Place = 14,
    RemoveStructure = 15,
    Sleep = 16,
    Attack = 17,
    Butcher = 18,
    MakeSpear = 19,
    Fish = 20,
    TakeCatch = 21,
    Say = 22,
    Gesture = 23,
    SetMeaning = 24,
    DrawSign = 25,
    EraseSign = 26,
    InviteToTribe = 27,
    AnswerInvite = 28,
    LeaveTribe = 29,
    StoreItem = 30,
    TakeItem = 31,
    TakeFromCorpse = 32,
    SetSignature = 33,
    Report = 34,
    GiveToStranger = 35,
    ShowItem = 36,

    // Server → klient: výsledky a stav světa.
    LoginResult = 100,
    World = 101,
    Snapshot = 102,
    PlayerJoined = 103,
    PlayerLeft = 104,
    SelfState = 105,
    Inventory = 106,
    Event = 107,
    PlantStates = 108,
    Fires = 109,
    Structures = 110,
    Animals = 111,
    Speech = 112,
    GestureSeen = 113,
    Vocabulary = 114,
    Signs = 115,
    TribeInvite = 116,
    Tribe = 117,
    Stockpiles = 118,
    DecorationTypes = 119,
    RockPiles = 120,
    Corpses = 121,
    Signature = 122,
    Strangers = 123,
}

/// <summary>Jak rostlina vypadá (plody nebo vykopaná). Klient nevidí, proč tomu tak je.</summary>
public enum PlantVisual : byte
{
    Plain = 0,
    UnripeFruit = 1,
    RipeFruit = 2,
    OverripeFruit = 3,
    Gone = 4,
}

/// <summary>Druh zprávy o události pro hráče (hláška).</summary>
public enum EventKind : byte
{
    Body = 0,
    Harvest = 1,
    Drink = 2,
    Material = 3,
    Fire = 4,
    Build = 5,
    Hunt = 6,
    Sign = 7,
    Tribe = 8,
    Corpse = 9,
    Report = 10,
    Stranger = 11,
}

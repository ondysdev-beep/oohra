namespace Oohra.Protocol;

/// <summary>Základní údaje o protokolu. Klient a server musí mít stejnou verzi.</summary>
public static class ProtocolInfo
{
    public const int Version = 1;

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
}

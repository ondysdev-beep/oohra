using LiteNetLib;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.World;

namespace Oohra.Server;

/// <summary>
/// Hráč na serveru: přezdívka, postava, batoh a poloha. Drží se i po odpojení,
/// takže se hráč může vrátit ke své postavě (ukládání na disk přijde ve Fázi E).
/// </summary>
public sealed class PlayerSession
{
    public PlayerSession(int id, string nickname, Character character, Inventory inventory, double x, double y)
    {
        Id = id;
        Nickname = nickname;
        Character = character;
        Inventory = inventory;
        X = x;
        Y = y;
    }

    public int Id { get; }

    public string Nickname { get; }

    /// <summary>Spojení s klientem, nebo null, když hráč není ve hře.</summary>
    public NetPeer? Peer { get; set; }

    public bool Online => Peer is not null;

    public Character Character { get; set; }

    public Inventory Inventory { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>Směr, kterým hráč chce jít (od klienta). (0, 0) = stojí.</summary>
    public double InputX { get; set; }

    public double InputY { get; set; }

    /// <summary>Kam se postava dívá (úhel v radiánech na mapě).</summary>
    public double Facing { get; set; }

    public int Lives { get; set; } = 1;

    /// <summary>Které hromádky byly zkažené, když se batoh naposledy posílal (ať se pošle znovu, až se něco zkazí).</summary>
    public string SentSpoilage { get; set; } = string.Empty;

    /// <summary>Podpis hráče (jméno ze slabik a znak), nebo null. Patří hráči, přežije smrt.</summary>
    public Signature? Signature { get; set; }

    /// <summary>Po smrti už zůstalo tělo (aby nevzniklo dvakrát). Nové narození to vynuluje.</summary>
    public bool BodyLeft { get; set; }

    /// <summary>Verze kmenů, která byla naposledy poslána (-1 = poslat znovu).</summary>
    public int SentTribeVersion { get; set; } = -1;

    /// <summary>Stav zásob, který byl naposledy poslán (prázdné = poslat znovu).</summary>
    public string SentStockKey { get; set; } = string.Empty;

    /// <summary>Verze zápisníku slov, která byla naposledy poslána (-1 = poslat znovu).</summary>
    public int SentVocabularyVersion { get; set; } = -1;

    /// <summary>Kdy hráč naposledy něco řekl (sekundy od startu serveru), kvůli pauze mezi slovy.</summary>
    public double LastSaidAt { get; set; } = double.NegativeInfinity;

    /// <summary>Poslední gesto a kdy ho udělal (kývnutí a vrtění hlavou dávají slovům souvislost).</summary>
    public Gesture? LastGesture { get; set; }

    public double LastGestureAt { get; set; } = double.NegativeInfinity;

    /// <summary>Kdy hráč naposledy něco nahlásil (sekundy od startu serveru), nejvýš jedno za reportCooldownSeconds.</summary>
    public double LastReportAt { get; set; } = double.NegativeInfinity;

    /// <summary>Kdy se současná postava narodila (herní minuty), kvůli délce života ve statistikách.</summary>
    public double BornAt { get; set; }

    /// <summary>Otisk tajného klíče hráče (PlayerKeys). Null = postava zatím nikomu nepatří (správce ji odemkl).</summary>
    public string? KeyHash { get; set; }

    /// <summary>Do kdy postava spí sen bez slov (sekundy serveru): nehladoví a zvířata si jí nevšímají.</summary>
    public double DreamUntil { get; set; } = double.NegativeInfinity;
}

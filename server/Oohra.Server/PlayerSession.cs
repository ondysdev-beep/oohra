using LiteNetLib;
using Oohra.Core.Characters;
using Oohra.Core.Items;

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
}

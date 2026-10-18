namespace Oohra.Core.World;

/// <summary>
/// Druh dlaždice. Čísla 0 až 6 odpovídají prototypu (T v part1.js) a golden datům,
/// další přidal nový generátor světa (návrh 0.8).
/// </summary>
public enum TileType : byte
{
    Sea = 0,
    Sand = 1,
    Grass = 2,
    Forest = 3,
    Rock = 4,
    Bog = 5,

    /// <summary>Sladká voda: jezírko, jezero i řeka. Nedá se přejít, dá se z ní pít.</summary>
    Pond = 6,

    /// <summary>Kamenitá půda skalnaté vrchoviny (mezi skalami, roste tu málo stromů).</summary>
    Highland = 7,

    /// <summary>Brod: mělčina na řece. Dá se přejít a napít se z ní.</summary>
    Ford = 8,

    /// <summary>Vrstvená skála ve vrchovině (později pazourek na nejlepší nářadí).</summary>
    LayeredRock = 9,
}

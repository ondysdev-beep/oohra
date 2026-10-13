using System.Text;
using Oohra.Core.World;

namespace Oohra.TestClient;

/// <summary>Vykreslí ostrov jako text, jeden znak na dlaždici.</summary>
public static class MapRenderer
{
    public const string Legend =
        "Legenda: ~ moře  - pobřeží  . písek  , tráva  ; les  ^ skála  % bažina  o jezírko\n" +
        "         T strom  0-7 rostlina (číslo druhu)  K tábor kmene  R stará rytina";

    public static string Render(Island island)
    {
        var map = new char[island.Height][];
        for (var y = 0; y < island.Height; y++)
        {
            map[y] = new char[island.Width];
            for (var x = 0; x < island.Width; x++)
            {
                var index = island.Index(x, y);
                map[y][x] = TileChar(island.Tiles[index], island.Shore[index]);
            }
        }

        foreach (var tree in island.Trees)
        {
            map[(int)tree.Y][(int)tree.X] = 'T';
        }

        foreach (var plant in island.Plants)
        {
            map[(int)plant.Y][(int)plant.X] = (char)('0' + plant.SpeciesId);
        }

        foreach (var ruin in island.RuinSpots)
        {
            map[(int)ruin.Y][(int)ruin.X] = 'R';
        }

        foreach (var camp in island.Camps)
        {
            map[camp.Y][camp.X] = 'K';
        }

        var text = new StringBuilder();
        foreach (var row in map)
        {
            text.Append(row).Append('\n');
        }

        return text.ToString();
    }

    private static char TileChar(TileType tile, bool shore)
    {
        return tile switch
        {
            TileType.Sea => shore ? '-' : '~',
            TileType.Sand => '.',
            TileType.Grass => ',',
            TileType.Forest => ';',
            TileType.Rock => '^',
            TileType.Bog => '%',
            TileType.Pond => 'o',
            _ => '?',
        };
    }
}

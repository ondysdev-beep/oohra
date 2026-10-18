using System.Text;
using Oohra.Core.World;

namespace Oohra.TestClient;

/// <summary>Vykreslí ostrov jako text, jeden znak na dlaždici.</summary>
public static class MapRenderer
{
    public const string Legend =
        "Legenda: ~ moře  - pobřeží  . písek  , tráva  ; les  ^ skála  % bažina  o jezírko\n" +
        "         : kamenitá půda  # vrstvená skála  = brod\n" +
        "         T strom  0-7 rostlina (číslo druhu)  K tábor kmene  R stará rytina";

    public const string RegionLegend =
        "Legenda: ~ moře  . pláž  L les  , louka  V vrchovina  B bažina  J jezero  R řeka";

    /// <summary>
    /// Mapa oblastí zmenšená, aby se vešla do konzole (nejvýš asi 96 znaků na řádek), a podíly oblastí ze souše.
    /// Řádků je polovina, protože znak v konzoli je asi dvakrát vyšší než širší.
    /// </summary>
    public static string RenderRegions(Island island)
    {
        var step = Math.Max(1, (int)Math.Ceiling(island.Width / 96.0));
        var text = new StringBuilder();
        for (var y = 0; y < island.Height; y += step * 2)
        {
            for (var x = 0; x < island.Width; x += step)
            {
                text.Append(RegionChar(island.RegionAt(x, y)));
            }

            text.Append('\n');
        }

        var land = island.Regions.Count(r => r != RegionKind.Sea);
        foreach (var group in island.Regions.Where(r => r != RegionKind.Sea).GroupBy(r => r).OrderBy(g => g.Key))
        {
            text.Append($"{RegionChar(group.Key)} {group.Key,-9} {100.0 * group.Count() / land,5:0.0} %\n");
        }

        return text.ToString();
    }

    private static char RegionChar(RegionKind region)
    {
        return region switch
        {
            RegionKind.Sea => '~',
            RegionKind.Coast => '.',
            RegionKind.Forest => 'L',
            RegionKind.Meadow => ',',
            RegionKind.Highland => 'V',
            RegionKind.Marsh => 'B',
            RegionKind.Lake => 'J',
            RegionKind.River => 'R',
            _ => '?',
        };
    }

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
            TileType.Highland => ':',
            TileType.Ford => '=',
            TileType.LayeredRock => '#',
            _ => '?',
        };
    }
}

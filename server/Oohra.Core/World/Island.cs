using Oohra.Core.Plants;
using Oohra.Core.Secret;

namespace Oohra.Core.World;

/// <summary>Bod na mřížce dlaždic.</summary>
public readonly record struct GridPoint(int X, int Y);

/// <summary>Místo ve světě (souřadnice v dlaždicích, střed dlaždice je x + 0,5).</summary>
public readonly record struct Position(double X, double Y);

/// <summary>
/// Strom na dlaždici: druh a výška v metrech (ze seedu). Variant (0 až 1) vybere model, když jich má druh víc.
/// </summary>
public readonly record struct Tree(int TileIndex, double X, double Y, double Variant, TreeKind Kind, double Height);

/// <summary>
/// Jedna rostlina ve světě. FruitOffset (-1, 0, 1) posouvá zrání plodů o měsíc,
/// aby všechny rostliny jednoho druhu neplodily najednou.
/// </summary>
public readonly record struct PlantInstance(int Id, int SpeciesId, double X, double Y, TileType Tile, int FruitOffset);

/// <summary>Vygenerovaný ostrov: dlaždice, pobřeží, tábory, ruiny, stromy a rostliny.</summary>
public sealed class Island
{
    private readonly HashSet<int> _treeTiles;

    public Island(
        int seed,
        int width,
        int height,
        TileType[] tiles,
        bool[] shore,
        IReadOnlyList<GridPoint> camps,
        IReadOnlyList<Position> ruinSpots,
        IReadOnlyList<Tree> trees,
        IReadOnlyList<PlantInstance> plants,
        IReadOnlyList<PlantSpecies> species,
        IReadOnlyList<SpeciesSecret> speciesSecrets,
        RegionKind[]? regions = null)
    {
        Seed = seed;
        Width = width;
        Height = height;
        Tiles = tiles;
        Regions = regions ?? tiles.Select(RegionOf).ToArray();
        Shore = shore;
        Camps = camps;
        RuinSpots = ruinSpots;
        Trees = trees;
        Plants = plants;
        Species = species;
        SpeciesSecrets = speciesSecrets;
        _treeTiles = trees.Select(t => t.TileIndex).ToHashSet();
        Rocks = new RockPiles(this);
    }

    public int Seed { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Dlaždice po řádcích: index = y * Width + x.</summary>
    public TileType[] Tiles { get; }

    /// <summary>Oblast každé dlaždice (les, louka, vrchovina…), index jako u Tiles.</summary>
    public RegionKind[] Regions { get; }

    /// <summary>Mořská dlaždice u pevniny (kreslí se světleji).</summary>
    public bool[] Shore { get; }

    /// <summary>Tábory dvou cizích kmenů.</summary>
    public IReadOnlyList<GridPoint> Camps { get; }

    /// <summary>Místa starých rytin (ruin).</summary>
    public IReadOnlyList<Position> RuinSpots { get; }

    public IReadOnlyList<Tree> Trees { get; }

    public IReadOnlyList<PlantInstance> Plants { get; }

    public IReadOnlyList<PlantSpecies> Species { get; }

    /// <summary>SKRYTÁ DATA: složení druhů. Nikdy neposílat klientovi.</summary>
    public IReadOnlyList<SpeciesSecret> SpeciesSecrets { get; }

    /// <summary>Stavby hráčů (zdi). Na rozdíl od zbytku ostrova se mění během hry.</summary>
    public Structures Structures { get; } = new();

    /// <summary>Těla mrtvých hráčů s jejich věcmi (zmizí po 4 dnech).</summary>
    public Corpses Corpses { get; } = new();

    /// <summary>Místa narození, kde prvních 24 hodin nesmí stavět cizí (návrh 4.1).</summary>
    public BirthGuards BirthGuards { get; } = new();

    /// <summary>Hromady kamenů na skalách (berou se z nich kameny, prázdné se za čas obnoví).</summary>
    public RockPiles Rocks { get; }

    /// <summary>Značky a rytiny hráčů a staré rytiny u ruin (ty přidá server při startu, AddAncient).</summary>
    public Signs Signs { get; } = new();

    public int Index(int x, int y)
    {
        return y * Width + x;
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public TileType TileAt(int x, int y)
    {
        return Tiles[Index(x, y)];
    }

    public bool HasTree(int tileIndex)
    {
        return _treeTiles.Contains(tileIndex);
    }

    /// <summary>V okolí (čtverec o poloměru radius) je jezírko.</summary>
    public bool PondNear(int x, int y, int radius)
    {
        for (var yy = -radius; yy <= radius; yy++)
        {
            for (var xx = -radius; xx <= radius; xx++)
            {
                if (InBounds(x + xx, y + yy) && TileAt(x + xx, y + yy) == TileType.Pond)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public RegionKind RegionAt(int x, int y)
    {
        return Regions[y * Width + x];
    }

    /// <summary>Oblast podle dlaždice, pro starý ostrov z prototypu (ten oblasti nemá).</summary>
    private static RegionKind RegionOf(TileType tile)
    {
        return tile switch
        {
            TileType.Sea => RegionKind.Sea,
            TileType.Sand => RegionKind.Coast,
            TileType.Forest => RegionKind.Forest,
            TileType.Rock or TileType.Highland or TileType.LayeredRock => RegionKind.Highland,
            TileType.Bog => RegionKind.Marsh,
            TileType.Pond => RegionKind.Lake,
            TileType.Ford => RegionKind.River,
            _ => RegionKind.Meadow,
        };
    }
}

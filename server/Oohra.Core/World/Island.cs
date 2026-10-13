using Oohra.Core.Plants;
using Oohra.Core.Secret;

namespace Oohra.Core.World;

/// <summary>Bod na mřížce dlaždic.</summary>
public readonly record struct GridPoint(int X, int Y);

/// <summary>Místo ve světě (souřadnice v dlaždicích, střed dlaždice je x + 0,5).</summary>
public readonly record struct Position(double X, double Y);

/// <summary>Strom na dlaždici. Variant (0 až 1) určuje velikost a druh stromu při kreslení.</summary>
public readonly record struct Tree(int TileIndex, double X, double Y, double Variant);

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
        IReadOnlyList<SpeciesSecret> speciesSecrets)
    {
        Seed = seed;
        Width = width;
        Height = height;
        Tiles = tiles;
        Shore = shore;
        Camps = camps;
        RuinSpots = ruinSpots;
        Trees = trees;
        Plants = plants;
        Species = species;
        SpeciesSecrets = speciesSecrets;
        _treeTiles = trees.Select(t => t.TileIndex).ToHashSet();
    }

    public int Seed { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Dlaždice po řádcích: index = y * Width + x.</summary>
    public TileType[] Tiles { get; }

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

    /// <summary>Zdi postavené hráči. Na rozdíl od zbytku ostrova se mění během hry.</summary>
    public Walls Walls { get; } = new();

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
}

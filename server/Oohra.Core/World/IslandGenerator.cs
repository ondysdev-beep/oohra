using Oohra.Core.Data;
using Oohra.Core.Rng;
using Oohra.Core.Secret;

namespace Oohra.Core.World;

/// <summary>
/// Generátor ostrova ze seedu, přesný přepis buildWorld z prototypu (part1.js).
/// Stejný seed a velikost dají vždy stejný ostrov. Čísla níže jsou z prototypu
/// a nesmí se měnit, jinak přestanou platit golden testy.
/// </summary>
public sealed class IslandGenerator
{
    // Šance na strom a na rostlinu podle dlaždice (treeP a plantP v prototypu).
    private static readonly Dictionary<TileType, double> _treeChance = new()
    {
        [TileType.Forest] = 0.42,
        [TileType.Grass] = 0.045,
        [TileType.Bog] = 0.03,
    };

    private static readonly Dictionary<TileType, double> _plantChance = new()
    {
        [TileType.Forest] = 0.09,
        [TileType.Grass] = 0.075,
        [TileType.Bog] = 0.10,
        [TileType.Sand] = 0.03,
        [TileType.Rock] = 0.015,
    };

    private readonly int _seed;
    private readonly int _width;
    private readonly int _height;
    private readonly TileType[] _tiles;

    private IslandGenerator(int seed, int width, int height)
    {
        _seed = seed;
        _width = width;
        _height = height;
        _tiles = new TileType[width * height];
    }

    /// <summary>Ostrov ve velikosti z nastavení (settings.json).</summary>
    public static Island Build(int seed)
    {
        var settings = GameData.Settings;
        return Build(seed, settings.WorldWidth, settings.WorldHeight);
    }

    public static Island Build(int seed, int width, int height)
    {
        return new IslandGenerator(seed, width, height).Generate();
    }

    private Island Generate()
    {
        BuildTerrain();
        AddPonds();
        var shore = MarkShore();
        var camps = MakeCamps();
        var ruinSpots = MakeRuinSpots(camps);
        var species = SpeciesGenerator.Build(_seed, GameData.Archetypes.Count, GameData.Settings.MonthsPerYear);
        var (trees, plants) = PlaceTreesAndPlants(camps, ruinSpots, species.Secrets);

        return new Island(_seed, _width, _height, _tiles, shore, camps, ruinSpots, trees, plants, species.Species, species.Secrets);
    }

    /// <summary>Výška terénu z šumu, dál od středu klesá do moře. Z výšky a dalšího šumu vznikne druh dlaždice.</summary>
    private void BuildTerrain()
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var dx = (x + 0.5 - _width / 2.0) / (_width / 2.0);
                var dy = (y + 0.5 - _height / 2.0) / (_height / 2.0);
                var d2 = dx * dx + dy * dy;
                var elevation = Noise.Fbm(x / 17.0, y / 17.0, _seed) - d2 * 0.82 + 0.34;
                _tiles[y * _width + x] = TileFor(x, y, elevation);
            }
        }
    }

    private TileType TileFor(int x, int y, double elevation)
    {
        if (elevation < 0.30)
        {
            return TileType.Sea;
        }

        if (elevation < 0.345)
        {
            return TileType.Sand;
        }

        var rock = Noise.Fbm(x / 8.0, y / 8.0, unchecked(_seed + 7));
        var forest = Noise.Fbm(x / 9.0, y / 9.0, unchecked(_seed + 13));
        var moisture = Noise.Fbm(x / 11.0, y / 11.0, unchecked(_seed + 29));
        if (rock > 0.66 && elevation > 0.42)
        {
            return TileType.Rock;
        }

        if (moisture > 0.62 && elevation < 0.52)
        {
            return TileType.Bog;
        }

        return forest > 0.54 ? TileType.Forest : TileType.Grass;
    }

    /// <summary>Čtyři pokusy o jezírko (elipsu) na trávě nebo v lese.</summary>
    private void AddPonds()
    {
        var rng = new Mulberry32(_seed ^ 0x70D);
        for (var k = 0; k < 4; k++)
        {
            for (var n = 0; n < 300; n++)
            {
                var cx = rng.NextInt(_width);
                var cy = rng.NextInt(_height);
                var tile = _tiles[cy * _width + cx];
                if (tile != TileType.Grass && tile != TileType.Forest)
                {
                    continue;
                }

                var rx = 2 + rng.NextDouble() * 2;
                var ry = 1.6 + rng.NextDouble() * 2;
                DigPond(cx, cy, rx, ry);
                break;
            }
        }
    }

    private void DigPond(int cx, int cy, double rx, double ry)
    {
        for (var yy = cy - 5; yy <= cy + 5; yy++)
        {
            for (var xx = cx - 5; xx <= cx + 5; xx++)
            {
                if (xx < 1 || yy < 1 || xx >= _width - 1 || yy >= _height - 1)
                {
                    continue;
                }

                var q = ((xx - cx) / rx) * ((xx - cx) / rx) + ((yy - cy) / ry) * ((yy - cy) / ry);
                var current = _tiles[yy * _width + xx];
                if (q < 1 && current != TileType.Sea && current != TileType.Sand && current != TileType.Rock)
                {
                    _tiles[yy * _width + xx] = TileType.Pond;
                }
            }
        }
    }

    /// <summary>Moře, které sousedí s pevninou (ne s jezírkem).</summary>
    private bool[] MarkShore()
    {
        var shore = new bool[_width * _height];
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                if (_tiles[y * _width + x] == TileType.Sea)
                {
                    shore[y * _width + x] = TouchesLand(x, y);
                }
            }
        }

        return shore;
    }

    private bool TouchesLand(int x, int y)
    {
        for (var oy = -1; oy <= 1; oy++)
        {
            for (var ox = -1; ox <= 1; ox++)
            {
                var nx = x + ox;
                var ny = y + oy;
                if (!InBounds(nx, ny))
                {
                    continue;
                }

                var neighbour = _tiles[ny * _width + nx];
                if (neighbour != TileType.Sea && neighbour != TileType.Pond)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Dva tábory na souši, nejlépe u jezírka a daleko od sebe.
    /// Když to nejde, podmínky se postupně uvolní (stejně jako makeCamps v prototypu).
    /// </summary>
    private List<GridPoint> MakeCamps()
    {
        var rng = new Mulberry32(_seed ^ 0x7B1B);
        var camps = new List<GridPoint>();

        for (var pass = 0; pass < 2 && camps.Count == 0; pass++)
        {
            for (var n = 0; n < 5000 && camps.Count == 0; n++)
            {
                var x = 4 + rng.NextInt(_width - 8);
                var y = 4 + rng.NextInt(_height - 8);
                if (AreaIsLand(x, y, 2) && (pass == 1 || PondNear(x, y, 14)))
                {
                    camps.Add(new GridPoint(x, y));
                }
            }
        }

        if (camps.Count == 0)
        {
            camps.Add(new GridPoint(_width >> 1, _height >> 1));
        }

        var first = camps[0];
        for (var pass = 0; pass < 2 && camps.Count < 2; pass++)
        {
            var minDistance = pass == 0 ? 34 : 18;
            for (var n = 0; n < 8000 && camps.Count < 2; n++)
            {
                var x = 4 + rng.NextInt(_width - 8);
                var y = 4 + rng.NextInt(_height - 8);
                if (AreaIsLand(x, y, 2)
                    && Distance(x, y, first.X, first.Y) >= minDistance
                    && (pass == 1 || PondNear(x, y, 14)))
                {
                    camps.Add(new GridPoint(x, y));
                }
            }
        }

        if (camps.Count < 2)
        {
            camps.Add(new GridPoint(first.X + 9, first.Y));
        }

        return camps;
    }

    /// <summary>Celé okolí bodu je souš (ne moře, jezírko ani skála) a daleko od okraje mapy.</summary>
    private bool AreaIsLand(int x, int y, int radius)
    {
        for (var yy = -radius; yy <= radius; yy++)
        {
            for (var xx = -radius; xx <= radius; xx++)
            {
                var a = x + xx;
                var b = y + yy;
                if (a < 2 || b < 2 || a >= _width - 2 || b >= _height - 2)
                {
                    return false;
                }

                var tile = _tiles[b * _width + a];
                if (tile == TileType.Sea || tile == TileType.Pond || tile == TileType.Rock)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool PondNear(int x, int y, int radius)
    {
        for (var yy = -radius; yy <= radius; yy++)
        {
            for (var xx = -radius; xx <= radius; xx++)
            {
                var a = x + xx;
                var b = y + yy;
                if (InBounds(a, b) && _tiles[b * _width + a] == TileType.Pond)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Až šest míst pro staré rytiny, daleko od táborů i od sebe navzájem.</summary>
    private List<Position> MakeRuinSpots(IReadOnlyList<GridPoint> camps)
    {
        var rng = new Mulberry32(_seed ^ 0x2C7D);
        var spots = new List<Position>();
        for (var n = 0; n < 8000 && spots.Count < 6; n++)
        {
            var x = 5 + rng.NextInt(_width - 10);
            var y = 5 + rng.NextInt(_height - 10);
            var tile = _tiles[y * _width + x];
            if (tile == TileType.Sea || tile == TileType.Pond || tile == TileType.Bog)
            {
                continue;
            }

            if (camps.Any(c => Distance(x, y, c.X, c.Y) < 12))
            {
                continue;
            }

            if (spots.Any(s => Distance(x, y, s.X, s.Y) < 14))
            {
                continue;
            }

            spots.Add(new Position(x + 0.5, y + 0.5));
        }

        return spots;
    }

    /// <summary>
    /// Projde dlaždice po řádcích a náhodně na ně dá strom, nebo rostlinu.
    /// Druh rostliny se losuje s vahou 4 pro druhy, kterým dlaždice svědčí, jinak 1.
    /// Kolem táborů a ruin nic neroste, ale náhoda se tam stejně čerpá (jako v prototypu).
    /// </summary>
    private (List<Tree> Trees, List<PlantInstance> Plants) PlaceTreesAndPlants(
        IReadOnlyList<GridPoint> camps,
        IReadOnlyList<Position> ruinSpots,
        IReadOnlyList<SpeciesSecret> secrets)
    {
        var rng = new Mulberry32(_seed ^ 0xA5A5);
        var trees = new List<Tree>();
        var plants = new List<PlantInstance>();

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var index = y * _width + x;
                var tile = _tiles[index];
                var keepClear = IsClearZone(x, y, camps, ruinSpots);

                if (_treeChance.TryGetValue(tile, out var treeChance) && rng.NextDouble() < treeChance)
                {
                    if (!keepClear)
                    {
                        trees.Add(new Tree(index, x + 0.5, y + 0.5, rng.NextDouble()));
                    }

                    continue;
                }

                if (_plantChance.TryGetValue(tile, out var plantChance) && rng.NextDouble() < plantChance)
                {
                    var speciesId = PickSpecies(rng, tile, secrets);
                    var offsetRoll = rng.NextDouble();
                    var px = x + 0.2 + rng.NextDouble() * 0.6;
                    var py = y + 0.2 + rng.NextDouble() * 0.6;
                    if (keepClear)
                    {
                        continue;
                    }

                    var offset = offsetRoll < 0.33 ? -1 : (offsetRoll < 0.66 ? 0 : 1);
                    plants.Add(new PlantInstance(plants.Count, speciesId, px, py, tile, offset));
                }
            }
        }

        return (trees, plants);
    }

    private static int PickSpecies(Mulberry32 rng, TileType tile, IReadOnlyList<SpeciesSecret> secrets)
    {
        var weights = secrets.Select(s => GameData.Archetypes[s.ArchetypeIndex].GrowsOn(tile) ? 4 : 1).ToArray();
        var pick = rng.NextDouble() * weights.Sum();
        for (var i = 0; i < weights.Length; i++)
        {
            pick -= weights[i];
            if (pick <= 0)
            {
                return i;
            }
        }

        return 0;
    }

    private static bool IsClearZone(int x, int y, IReadOnlyList<GridPoint> camps, IReadOnlyList<Position> ruinSpots)
    {
        foreach (var camp in camps)
        {
            if (Math.Abs(x - camp.X) <= 2 && Math.Abs(y - camp.Y) <= 2)
            {
                return true;
            }
        }

        foreach (var ruin in ruinSpots)
        {
            if (Math.Abs(x - Math.Floor(ruin.X)) <= 1 && Math.Abs(y - Math.Floor(ruin.Y)) <= 1)
            {
                return true;
            }
        }

        return false;
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < _width && y < _height;
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

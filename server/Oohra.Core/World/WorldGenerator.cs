using Oohra.Core.Data;
using Oohra.Core.Rng;
using Oohra.Core.Secret;

namespace Oohra.Core.World;

/// <summary>
/// Nový generátor světa (návrh 0.8, kap. 1): jeden velký ostrov s oblastmi (les, louka, skalnatá vrchovina, bažina,
/// jezero a řeka, pláž). Pravidla a čísla jsou v worldgen.json. Stejný seed a velikost dají vždy stejný svět.
///
/// Postup: tvar ostrova (souš je landShare mapy, ostrůvky se zatopí) → pláž podle vzdálenosti od moře →
/// vrchovina (nejvyšší místa) → jezero (nejnižší místo v dálce od moře) → řeka z vrchoviny do jezera a z jezera
/// do moře, s brody → bažina (vlhká nízká místa, nejlépe u vody) → les a louka podle šumu → dlaždice podle oblastí
/// → jezírka → tábory, ruiny, stromy a rostliny.
/// Podíly oblastí se berou pořadím (třeba 15 % nejvyšších míst souše), takže každý seed má stejné podíly.
/// </summary>
public sealed class WorldGenerator
{
    private const int Unreachable = int.MaxValue;

    private readonly int _seed;
    private readonly int _width;
    private readonly int _height;
    private readonly WorldGenRules _rules;
    private readonly TileType[] _tiles;
    private readonly RegionKind[] _regions;
    private readonly bool[] _assigned;
    private readonly bool[] _fords;
    private readonly double[] _elevation;
    private double[]? _detail;
    private int[] _coastDistance = [];
    private int _landCount;

    private WorldGenerator(int seed, int width, int height)
    {
        _seed = seed;
        _width = width;
        _height = height;
        _rules = GameData.WorldGen;
        _tiles = new TileType[width * height];
        _regions = new RegionKind[width * height];
        _assigned = new bool[width * height];
        _fords = new bool[width * height];
        _elevation = new double[width * height];
    }

    /// <summary>Svět ve velikosti z nastavení (settings.json).</summary>
    public static Island Build(int seed)
    {
        var settings = GameData.Settings;
        return Build(seed, settings.WorldWidth, settings.WorldHeight);
    }

    public static Island Build(int seed, int width, int height)
    {
        return new WorldGenerator(seed, width, height).Generate();
    }

    /// <summary>Celý svět pro hru: ostrov a pod ním podzemí s jeskyněmi (Caves.Dig).</summary>
    public static Island BuildWorld(int seed, int width, int height)
    {
        return Caves.Dig(Build(seed, width, height));
    }

    private Island Generate()
    {
        ShapeIsland();
        _coastDistance = DistanceFrom(i => _assigned[i] && _regions[i] == RegionKind.Sea, i => true);
        MarkCoast();
        MarkHighland();
        var lake = MarkLake();
        CarveRiver(lake);
        MarkMarsh();
        MarkForestAndMeadow();
        PaintTiles();
        DigPonds();
        FillUnreachablePockets();

        var shore = MarkShore();
        var camps = MakeCamps();
        var ruins = MakeRuinSpots(camps);
        var species = SpeciesGenerator.Build(_seed, GameData.Archetypes.Count, GameData.Settings.MonthsPerYear);
        var (trees, plants) = PlaceTreesAndPlants(camps, ruins, species.Secrets);
        return new Island(_seed, _width, _height, _tiles, shore, camps, ruins, trees, plants, species.Species, species.Secrets, _regions);
    }

    // ---------- tvar ostrova ----------

    /// <summary>
    /// Výška ze šumu, ke krajům mapy klesá. Souš je landShare nejvyšších míst, okraj mapy je vždy moře.
    /// Zůstane jen největší kus souše (jeden ostrov).
    /// </summary>
    private void ShapeIsland()
    {
        var all = new List<int>(_tiles.Length);
        for (var i = 0; i < _tiles.Length; i++)
        {
            var (x, y) = (i % _width, i / _width);
            var dx = (x + 0.5 - _width / 2.0) / (_width / 2.0);
            var dy = (y + 0.5 - _height / 2.0) / (_height / 2.0);
            _elevation[i] = Noise.Fbm(x / _rules.ShapeScale, y / _rules.ShapeScale, unchecked(_seed + 1)) * _rules.ShapeNoise - (dx * dx + dy * dy);
            if (x >= 4 && y >= 4 && x < _width - 4 && y < _height - 4)
            {
                all.Add(i);
            }
        }

        var land = new bool[_tiles.Length];
        foreach (var i in TakeTop(all, i => _elevation[i], (int)(_tiles.Length * _rules.LandShare)))
        {
            land[i] = true;
        }

        var main = LargestComponent(land);
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (!main[i])
            {
                Assign(i, RegionKind.Sea);
            }
            else
            {
                _landCount++;
            }
        }
    }

    private void MarkCoast()
    {
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (!_assigned[i] && _coastDistance[i] <= _rules.BeachWidth)
            {
                Assign(i, RegionKind.Coast);
            }
        }
    }

    /// <summary>Vrchovina: nejvyšší místa souše (podíl shares.highland), vlny terénu jí dají nepravidelný tvar.</summary>
    private void MarkHighland()
    {
        foreach (var i in TakeTop(Free(), i => _elevation[i] + 0.25 * Detail(i), Share(_rules.Shares.Highland)))
        {
            Assign(i, RegionKind.Highland);
        }
    }

    // ---------- voda ----------

    /// <summary>
    /// Jezero: nejnižší místo terénu (šum detailScale), které je aspoň minCoastDistance od moře. Kolem něj se
    /// přidávají nízká místa, dokud jezero nemá podíl shares.lake (hledá se výška hladiny). Vrací dlaždice jezera.
    /// </summary>
    private List<int> MarkLake()
    {
        var candidates = Free().Where(i => _coastDistance[i] >= _rules.Lake.MinCoastDistance).ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var start = candidates.OrderBy(Detail).ThenBy(i => i).First();
        var allowed = candidates.ToHashSet();
        var target = Share(_rules.Shares.Lake);
        double low = Detail(start), high = 1.5;
        var lake = new List<int> { start };
        for (var step = 0; step < 24; step++)
        {
            var level = (low + high) / 2;
            var grown = Flood(start, i => allowed.Contains(i) && Detail(i) <= level);
            if (grown.Count > target)
            {
                high = level;
            }
            else
            {
                low = level;
                lake = grown;
            }
        }

        foreach (var i in lake)
        {
            Assign(i, RegionKind.Lake);
        }

        return lake;
    }

    /// <summary>
    /// Řeka: pramen ve vrchovině co nejdál od jezera, teče do jezera a z jezera do moře. Cestu hledá krajinou,
    /// kde je šum „údolí“ (wobbleScale, meander) nejnižší, takže se kroutí. U pramene je úzká, k ústí se rozšiřuje. Brod každých fordEvery metrů, na každém
    /// úseku aspoň jeden, takže se řeka dá přejít.
    /// </summary>
    private void CarveRiver(List<int> lake)
    {
        var highland = Enumerable.Range(0, _tiles.Length).Where(i => _regions[i] == RegionKind.Highland && _assigned[i]).ToList();
        if (lake.Count == 0 || highland.Count == 0)
        {
            return;
        }

        var lakeSet = lake.ToHashSet();
        var toLakeField = CostDistance(lakeSet.Contains, IsLand);
        var source = highland.OrderByDescending(i => toLakeField[i]).ThenBy(i => i).First();
        var toLake = Descend(source, toLakeField);
        var toSeaField = CostDistance(i => _assigned[i] && _regions[i] == RegionKind.Sea, i => true);
        var exit = lake.OrderBy(i => toSeaField[i]).ThenBy(i => i).First();
        var toSea = Descend(exit, toSeaField);

        var total = toLake.Count + toSea.Count;
        PaintRiver(toLake, 0, total, lakeSet);
        PaintRiver(toSea, toLake.Count, total, lakeSet);
    }

    private void PaintRiver(List<int> path, int offset, int total, HashSet<int> lake)
    {
        var rules = _rules.River;
        var fords = 0;
        for (var k = 0; k < path.Count; k++)
        {
            var width = rules.MinWidth + (rules.MaxWidth - rules.MinWidth) * (offset + k) / Math.Max(1, total - 1);
            PaintDisk(path[k], width / 2, i => IsLand(i) && !lake.Contains(i), i => Assign(i, RegionKind.River));
            var inner = k >= rules.FordSkipEnds && k < path.Count - rules.FordSkipEnds;
            if (inner && (k - rules.FordSkipEnds) % rules.FordEvery == rules.FordEvery / 2 && !lake.Contains(path[k]))
            {
                fords++;
            }
        }

        // Brody až po namalování celého úseku: když podle fordEvery nevyjde žádný, dá se jeden doprostřed.
        for (var k = 0; k < path.Count; k++)
        {
            var inner = k >= rules.FordSkipEnds && k < path.Count - rules.FordSkipEnds;
            var due = inner && (k - rules.FordSkipEnds) % rules.FordEvery == rules.FordEvery / 2;
            var middle = fords == 0 && k == path.Count / 2;
            if ((due || middle) && !lake.Contains(path[k]))
            {
                var width = rules.MinWidth + (rules.MaxWidth - rules.MinWidth) * (offset + k) / Math.Max(1, total - 1);
                PaintDisk(path[k], width / 2 + 1.5, i => _regions[i] == RegionKind.River, i => _fords[i] = true);
            }
        }
    }

    /// <summary>Cesta po poli vzdáleností dolů k cíli (vzdálenost 0): vždy na souseda, který je k cíli nejblíž.</summary>
    private static List<int> Descend(int start, double[] distance, Func<int, IEnumerable<int>> neighbours)
    {
        var path = new List<int> { start };
        var current = start;
        while (distance[current] > 0 && !double.IsPositiveInfinity(distance[current]))
        {
            var next = -1;
            var best = distance[current];
            foreach (var n in neighbours(current))
            {
                if (distance[n] < best)
                {
                    best = distance[n];
                    next = n;
                }
            }

            if (next < 0)
            {
                break;
            }

            path.Add(next);
            current = next;
        }

        return path;
    }

    /// <summary>Malá jezírka s pitnou vodou v lese a na louce (aspoň 8 m od moře).</summary>
    private void DigPonds()
    {
        var rules = _rules.Ponds;
        var rng = new Mulberry32(_seed ^ 0x70D);
        for (var k = 0; k < rules.Count; k++)
        {
            for (var n = 0; n < 400; n++)
            {
                var i = rng.NextInt(_tiles.Length);
                var radius = rules.MinRadius + rng.NextDouble() * (rules.MaxRadius - rules.MinRadius);
                if (_regions[i] is RegionKind.Meadow or RegionKind.Forest && _coastDistance[i] > 8)
                {
                    PaintDisk(i, radius, j => _regions[j] is RegionKind.Meadow or RegionKind.Forest, j => _tiles[j] = TileType.Pond);
                    break;
                }
            }
        }
    }

    // ---------- bažina, les a louka ----------

    /// <summary>Bažina: vlhká a nízká místa (podíl shares.marsh), přednost mají místa blízko řeky a jezera.</summary>
    private void MarkMarsh()
    {
        var water = DistanceFrom(i => _regions[i] is RegionKind.Lake or RegionKind.River && _assigned[i], IsLand);
        var free = Free();
        var minE = free.Count > 0 ? free.Min(i => _elevation[i]) : 0;
        var maxE = free.Count > 0 ? free.Max(i => _elevation[i]) : 1;
        double Score(int i)
        {
            var x = i % _width;
            var y = i / _width;
            var moisture = Noise.Fbm(x / _rules.MoistureScale, y / _rules.MoistureScale, unchecked(_seed + 29));
            var low = 1 - (_elevation[i] - minE) / Math.Max(1e-9, maxE - minE);
            var nearWater = water[i] <= _rules.MarshWaterDistance ? _rules.MarshWaterBonus : 0;
            return moisture + 0.15 * low + nearWater;
        }

        foreach (var i in TakeTop(free, Score, Share(_rules.Shares.Marsh)))
        {
            Assign(i, RegionKind.Marsh);
        }
    }

    /// <summary>Les tam, kde je šum lesa nejvyšší (podíl shares.forest), zbytek je louka.</summary>
    private void MarkForestAndMeadow()
    {
        var scale = _rules.Forest.RegionScale;
        var free = Free();
        foreach (var i in TakeTop(free, i => Noise.Fbm(i % _width / scale, i / _width / scale, unchecked(_seed + 13)), Share(_rules.Shares.Forest)))
        {
            Assign(i, RegionKind.Forest);
        }

        foreach (var i in Free())
        {
            Assign(i, RegionKind.Meadow);
        }
    }

    /// <summary>
    /// Dlaždice podle oblastí. V lese jsou světliny (tráva), ve vrchovině skály (část z nich vrstvená skála)
    /// a mezi nimi kamenitá půda. Na řece brody.
    /// </summary>
    private void PaintTiles()
    {
        for (var i = 0; i < _tiles.Length; i++)
        {
            _tiles[i] = _regions[i] switch
            {
                RegionKind.Sea => TileType.Sea,
                RegionKind.Coast => TileType.Sand,
                RegionKind.Lake => TileType.Pond,
                RegionKind.River => _fords[i] ? TileType.Ford : TileType.Pond,
                RegionKind.Marsh => TileType.Bog,
                RegionKind.Forest => TileType.Forest,
                RegionKind.Highland => TileType.Highland,
                _ => TileType.Grass,
            };
        }

        var forest = Enumerable.Range(0, _tiles.Length).Where(i => _regions[i] == RegionKind.Forest).ToList();
        var clearing = _rules.Forest.ClearingScale;
        var clearings = (int)(forest.Count * _rules.Forest.ClearingShare);
        foreach (var i in TakeTop(forest, i => -Noise.Fbm(i % _width / clearing, i / _width / clearing, unchecked(_seed + 41)), clearings))
        {
            _tiles[i] = TileType.Grass;
        }

        var rules = _rules.Highland;
        var highland = Enumerable.Range(0, _tiles.Length).Where(i => _regions[i] == RegionKind.Highland).ToList();
        var rocks = TakeTop(highland, i => Noise.Fbm(i % _width / rules.RockScale, i / _width / rules.RockScale, unchecked(_seed + 7)), (int)(highland.Count * rules.RockShare));
        foreach (var i in rocks)
        {
            _tiles[i] = TileType.Rock;
        }

        foreach (var i in TakeTop(rocks, i => Noise.Fbm(i % _width / rules.LayeredScale, i / _width / rules.LayeredScale, unchecked(_seed + 53)), (int)(rocks.Count * rules.LayeredShare)))
        {
            _tiles[i] = TileType.LayeredRock;
        }
    }

    /// <summary>
    /// Souš, na kterou se nedá dojít (ostrůvek uprostřed jezera nebo jezírka), se zatopí. Všude se tak dá dojít pěšky.
    /// </summary>
    private void FillUnreachablePockets()
    {
        var walkable = Enumerable.Range(0, _tiles.Length).Select(i => !Movement.IsWaterTile(_tiles[i])).ToArray();
        var main = LargestComponent(walkable);
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (walkable[i] && !main[i])
            {
                _tiles[i] = TileType.Pond;
                _regions[i] = RegionKind.Lake;
            }
        }
    }

    // ---------- pobřeží, tábory, ruiny, stromy a rostliny ----------

    /// <summary>Moře, které sousedí se souší (kreslí se světleji).</summary>
    private bool[] MarkShore()
    {
        var shore = new bool[_tiles.Length];
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] != TileType.Sea)
            {
                continue;
            }

            var (x, y) = (i % _width, i / _width);
            for (var oy = -1; oy <= 1 && !shore[i]; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    if (InBounds(x + ox, y + oy) && _tiles[(y + oy) * _width + x + ox] is not (TileType.Sea or TileType.Pond))
                    {
                        shore[i] = true;
                        break;
                    }
                }
            }
        }

        return shore;
    }

    /// <summary>
    /// Tábory cizinců na louce nebo v lese, u sladké vody a daleko od sebe, nejlépe každý v jiné oblasti.
    /// Když to nejde, podmínky se postupně uvolní.
    /// </summary>
    private List<GridPoint> MakeCamps()
    {
        var rules = _rules.Camps;
        var water = DistanceFrom(i => _tiles[i] is TileType.Pond or TileType.Ford, i => true);
        var rng = new Mulberry32(_seed ^ 0x7B1B);
        var camps = new List<GridPoint>();
        // Na malé mapě (testy) by se tábory daleko od sebe nevešly: poslední pokus jen čtvrtinu šířky mapy.
        for (var pass = 0; pass < 5 && camps.Count < rules.Count; pass++)
        {
            var minDistance = pass < 3 ? rules.MinDistance : pass == 3 ? rules.MinDistance / 2 : Math.Min(rules.MinDistance / 2, _width / 4);
            for (var n = 0; n < 20000 && camps.Count < rules.Count; n++)
            {
                var x = 4 + rng.NextInt(_width - 8);
                var y = 4 + rng.NextInt(_height - 8);
                var i = y * _width + x;
                var region = _regions[i];
                if ((pass < 4 && region is not (RegionKind.Meadow or RegionKind.Forest))
                    || !AreaIsDryLand(x, y, 2)
                    || (pass < 2 && water[i] > rules.WaterWithin)
                    || (pass == 0 && camps.Any(c => _regions[c.Y * _width + c.X] == region))
                    || camps.Any(c => Distance(x, y, c.X, c.Y) < minDistance))
                {
                    continue;
                }

                camps.Add(new GridPoint(x, y));
            }
        }

        return camps;
    }

    /// <summary>Zříceniny Pradávných: na souši (ne v bažině), daleko od táborů i od sebe navzájem.</summary>
    private List<Position> MakeRuinSpots(IReadOnlyList<GridPoint> camps)
    {
        var rules = _rules.Ruins;
        var rng = new Mulberry32(_seed ^ 0x2C7D);
        var spots = new List<Position>();
        for (var n = 0; n < 20000 && spots.Count < rules.Count; n++)
        {
            var x = 5 + rng.NextInt(_width - 10);
            var y = 5 + rng.NextInt(_height - 10);
            if (!AreaIsDryLand(x, y, 1)
                || _tiles[y * _width + x] == TileType.Bog
                || camps.Any(c => Distance(x, y, c.X, c.Y) < rules.CampDistance)
                || spots.Any(s => Distance(x, y, s.X, s.Y) < rules.MinDistance))
            {
                continue;
            }

            spots.Add(new Position(x + 0.5, y + 0.5));
        }

        return spots;
    }

    /// <summary>
    /// Projde dlaždice po řádcích a náhodně na ně dá strom, nebo rostlinu (šance podle dlaždice ve worldgen.json).
    /// Strom stojí kdekoli v dlaždici (ne jen uprostřed), šanci násobí šum shluků a mýtin (treeClumps) a strom se
    /// nepostaví blíž k sousedovi, než dovolí rozestup obou druhů (spacing v trees.json). Kde se strom nevejde,
    /// může růst rostlina. Druh rostliny se losuje s vahou 4 pro druhy, kterým dlaždice svědčí, jinak 1.
    /// Kolem táborů a ruin nic neroste.
    /// </summary>
    private (List<Tree> Trees, List<PlantInstance> Plants) PlaceTreesAndPlants(
        IReadOnlyList<GridPoint> camps,
        IReadOnlyList<Position> ruins,
        IReadOnlyList<SpeciesSecret> secrets)
    {
        var rng = new Mulberry32(_seed ^ 0xA5A5);
        var freshWater = DistanceFrom(i => _tiles[i] is TileType.Pond or TileType.Ford, i => true);
        var trees = new List<Tree>();
        var plants = new List<PlantInstance>();
        var nearby = new TreeGrid(_width, _height);
        for (var i = 0; i < _tiles.Length; i++)
        {
            var (x, y) = (i % _width, i / _width);
            var tile = _tiles[i];
            var clear = IsClearZone(x, y, camps, ruins);
            if (_rules.TreeChance.TryGetValue(tile, out var treeChance) && rng.NextDouble() < treeChance * Clumps(x, y))
            {
                var (tx, ty) = (x + 0.15 + 0.7 * rng.NextDouble(), y + 0.15 + 0.7 * rng.NextDouble());
                var variant = rng.NextDouble();
                if (clear)
                {
                    continue;
                }

                var (kind, height) = TreeSpecies(i, freshWater);
                if (nearby.HasRoom(tx, ty, GameData.Trees.For(kind).Spacing))
                {
                    var tree = new Tree(i, tx, ty, variant, kind, height);
                    trees.Add(tree);
                    nearby.Add(tree);
                    continue;
                }
            }

            if (_rules.PlantChance.TryGetValue(tile, out var plantChance) && rng.NextDouble() < plantChance)
            {
                var speciesId = PickSpecies(rng, tile, secrets);
                var offsetRoll = rng.NextDouble();
                var px = x + 0.2 + rng.NextDouble() * 0.6;
                var py = y + 0.2 + rng.NextDouble() * 0.6;
                if (!clear)
                {
                    var offset = offsetRoll < 0.33 ? -1 : (offsetRoll < 0.66 ? 0 : 1);
                    plants.Add(new PlantInstance(plants.Count, speciesId, px, py, tile, offset));
                }
            }
        }

        return (trees, plants);
    }

    /// <summary>
    /// Druh a výška stromu ze seedu a čísla dlaždice (vlastní náhoda, aby se nepohnulo rozmístění rostlin):
    /// druh podle vah oblasti v trees.json, u sladké vody často vrba, výška v rozsahu druhu.
    /// </summary>
    private (TreeKind Kind, double Height) TreeSpecies(int i, int[] freshWater)
    {
        var rules = GameData.Trees;
        var salt = unchecked(_seed ^ rules.SeedSalt);
        var (x, y) = (i % _width, i / _width);
        var region = _regions[i];
        var kind = TreeKind.Oak;
        if (freshWater[i] <= rules.WaterDistance && Hash.Hash2(x, y, unchecked(salt + 1)) < rules.WillowNearWater)
        {
            kind = TreeKind.Willow;
        }
        else
        {
            var options = rules.Species.Where(s => s.Weights.ContainsKey(region)).ToList();
            var pick = Hash.Hash2(x, y, salt) * options.Sum(s => s.Weights[region]);
            foreach (var option in options)
            {
                kind = option.Kind;
                pick -= option.Weights[region];
                if (pick <= 0)
                {
                    break;
                }
            }
        }

        var species = rules.For(kind);
        var height = species.MinHeight + (species.MaxHeight - species.MinHeight) * Hash.Hash2(x, y, unchecked(salt + 2));
        return (kind, Math.Round(height, 1));
    }

    /// <summary>Šum shluků stromů a mýtin: násobí šanci na strom (treeClumps ve worldgen.json).</summary>
    private double Clumps(int x, int y)
    {
        var rules = _rules.TreeClumps;
        var noise = Noise.Value(x / rules.Scale, y / rules.Scale, unchecked(_seed ^ 0x7C1));
        return rules.Min + (rules.Max - rules.Min) * noise;
    }

    /// <summary>Stromy podle čtverců 8 × 8 m, aby se rozestup od sousedů hledal jen v okolí.</summary>
    private sealed class TreeGrid
    {
        private const int Cell = 8;
        private readonly List<Tree>[] _cells;
        private readonly int _columns;
        private readonly int _rows;

        public TreeGrid(int width, int height)
        {
            _columns = width / Cell + 1;
            _rows = height / Cell + 1;
            _cells = new List<Tree>[_columns * _rows];
        }

        public void Add(Tree tree)
        {
            var index = (int)(tree.Y / Cell) * _columns + (int)(tree.X / Cell);
            (_cells[index] ??= []).Add(tree);
        }

        /// <summary>Strom s rozestupem spacing se sem vejde (od každého souseda aspoň průměr obou rozestupů).</summary>
        public bool HasRoom(double x, double y, double spacing)
        {
            var (cx, cy) = ((int)(x / Cell), (int)(y / Cell));
            for (var row = Math.Max(0, cy - 1); row <= Math.Min(_rows - 1, cy + 1); row++)
            {
                for (var column = Math.Max(0, cx - 1); column <= Math.Min(_columns - 1, cx + 1); column++)
                {
                    foreach (var other in _cells[row * _columns + column] ?? [])
                    {
                        var need = (spacing + GameData.Trees.For(other.Kind).Spacing) / 2;
                        if ((other.X - x) * (other.X - x) + (other.Y - y) * (other.Y - y) < need * need)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
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

    private static bool IsClearZone(int x, int y, IReadOnlyList<GridPoint> camps, IReadOnlyList<Position> ruins)
    {
        return camps.Any(c => Math.Abs(x - c.X) <= 2 && Math.Abs(y - c.Y) <= 2)
            || ruins.Any(r => Math.Abs(x - Math.Floor(r.X)) <= 1 && Math.Abs(y - Math.Floor(r.Y)) <= 1);
    }

    /// <summary>Celé okolí bodu je suchá souš, na které jde stát a stavět (ne voda, brod, skála ani bažina).</summary>
    private bool AreaIsDryLand(int x, int y, int radius)
    {
        for (var yy = y - radius; yy <= y + radius; yy++)
        {
            for (var xx = x - radius; xx <= x + radius; xx++)
            {
                if (!InBounds(xx, yy) || _tiles[yy * _width + xx] is TileType.Sea or TileType.Pond or TileType.Ford
                    or TileType.Rock or TileType.LayeredRock or TileType.Bog)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // ---------- pomůcky ----------

    private void Assign(int i, RegionKind region)
    {
        _regions[i] = region;
        _assigned[i] = true;
    }

    /// <summary>Souš, která ještě nemá oblast.</summary>
    private List<int> Free()
    {
        var free = new List<int>();
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (!_assigned[i])
            {
                free.Add(i);
            }
        }

        return free;
    }

    private int Share(double share)
    {
        return (int)Math.Round(_landCount * share);
    }

    private bool IsLand(int i)
    {
        return _regions[i] != RegionKind.Sea || !_assigned[i];
    }

    /// <summary>Vlny terénu uvnitř ostrova (šum detailScale), spočítané jednou pro celou mapu.</summary>
    private double Detail(int i)
    {
        if (_detail is null)
        {
            _detail = new double[_tiles.Length];
            for (var j = 0; j < _tiles.Length; j++)
            {
                _detail[j] = Noise.Fbm(j % _width / _rules.DetailScale, j / _width / _rules.DetailScale, unchecked(_seed + 3));
            }
        }

        return _detail[i];
    }

    private List<int> Descend(int start, double[] distance)
    {
        return Descend(start, distance, i => Neighbours8(i).Select(n => n.Index));
    }

    /// <summary>Sousedé na 8 stran s délkou kroku (šikmo 1,41), pro řeku, aby netekla jen rovně a do pravého úhlu.</summary>
    private IEnumerable<(int Index, double Step)> Neighbours8(int i)
    {
        var (x, y) = (i % _width, i / _width);
        for (var oy = -1; oy <= 1; oy++)
        {
            for (var ox = -1; ox <= 1; ox++)
            {
                if ((ox != 0 || oy != 0) && InBounds(x + ox, y + oy))
                {
                    yield return ((y + oy) * _width + x + ox, ox != 0 && oy != 0 ? 1.41421356 : 1);
                }
            }
        }
    }

    /// <summary>Cena kroku řeky: 1 a k tomu šum „údolí“, takže řeka teče tam, kde je šum nízký.</summary>
    private double RiverStepCost(int i)
    {
        var scale = _rules.River.WobbleScale;
        return 1 + _rules.River.Meander * Noise.Fbm(i % _width / scale, i / _width / scale, unchecked(_seed + 77));
    }

    /// <summary>Nejlevnější cesta (Dijkstra) od zdrojových dlaždic, cena kroku RiverStepCost.</summary>
    private double[] CostDistance(Func<int, bool> source, Func<int, bool> passable)
    {
        var distance = Enumerable.Repeat(double.PositiveInfinity, _tiles.Length).ToArray();
        var heap = new MinHeap();
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (source(i))
            {
                distance[i] = 0;
                heap.Push(i, 0);
            }
        }

        while (heap.Count > 0)
        {
            var (current, d) = heap.Pop();
            if (d > distance[current])
            {
                continue;
            }

            foreach (var (n, step) in Neighbours8(current))
            {
                var next = d + step * RiverStepCost(n);
                if (passable(n) && next < distance[n])
                {
                    distance[n] = next;
                    heap.Push(n, next);
                }
            }
        }

        return distance;
    }

    /// <summary>count dlaždic s nejvyšším skóre (při shodě menší index), vždy stejně pro stejný seed.</summary>
    private static List<int> TakeTop(List<int> candidates, Func<int, double> score, int count)
    {
        return candidates
            .Select(i => (Index: i, Score: score(i)))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Index)
            .Take(Math.Max(0, count))
            .Select(c => c.Index)
            .ToList();
    }

    /// <summary>Vzdálenost (kroky na 4 strany) od nejbližší zdrojové dlaždice, jen přes průchozí dlaždice.</summary>
    private int[] DistanceFrom(Func<int, bool> source, Func<int, bool> passable)
    {
        var distance = Enumerable.Repeat(Unreachable, _tiles.Length).ToArray();
        var queue = new Queue<int>();
        for (var i = 0; i < _tiles.Length; i++)
        {
            if (source(i))
            {
                distance[i] = 0;
                queue.Enqueue(i);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var n in Neighbours(current))
            {
                if (distance[n] == Unreachable && passable(n))
                {
                    distance[n] = distance[current] + 1;
                    queue.Enqueue(n);
                }
            }
        }

        return distance;
    }

    /// <summary>Souvislý kus dlaždic od start, kam pustí podmínka.</summary>
    private List<int> Flood(int start, Func<int, bool> allowed)
    {
        var seen = new HashSet<int> { start };
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            foreach (var n in Neighbours(queue.Dequeue()))
            {
                if (allowed(n) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }

        return seen.ToList();
    }

    private bool[] LargestComponent(bool[] member)
    {
        var seen = new bool[member.Length];
        var best = new List<int>();
        for (var i = 0; i < member.Length; i++)
        {
            if (!member[i] || seen[i])
            {
                continue;
            }

            var part = Flood(i, n => member[n]);
            foreach (var p in part)
            {
                seen[p] = true;
            }

            if (part.Count > best.Count)
            {
                best = part;
            }
        }

        var result = new bool[member.Length];
        foreach (var i in best)
        {
            result[i] = true;
        }

        return result;
    }

    private void PaintDisk(int center, double radius, Func<int, bool> allowed, Action<int> paint)
    {
        var (cx, cy) = (center % _width, center / _width);
        var r = (int)Math.Ceiling(radius);
        for (var y = cy - r; y <= cy + r; y++)
        {
            for (var x = cx - r; x <= cx + r; x++)
            {
                var i = y * _width + x;
                if (InBounds(x, y) && Distance(x, y, cx, cy) <= radius && allowed(i))
                {
                    paint(i);
                }
            }
        }
    }

    private IEnumerable<int> Neighbours(int i)
    {
        var (x, y) = (i % _width, i / _width);
        if (x > 0) yield return i - 1;
        if (x < _width - 1) yield return i + 1;
        if (y > 0) yield return i - _width;
        if (y < _height - 1) yield return i + _width;
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

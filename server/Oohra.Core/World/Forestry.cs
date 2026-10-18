using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.Rng;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>
/// Dřevo ležící ve světě: celý kmen (Trunk, blokuje cestu) nebo klad (Piece, 1 m).
/// Poloha je střed, úhel je směr kmene (radiány, jako směr postavy), délka v metrech.
/// </summary>
public sealed class Timber
{
    public Timber(int id, TimberKind kind, TreeKind tree, double x, double y, double angle, int length)
    {
        Id = id;
        Kind = kind;
        Tree = tree;
        X = x;
        Y = y;
        Angle = angle;
        Length = length;
    }

    public int Id { get; }

    public TimberKind Kind { get; set; }

    public TreeKind Tree { get; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Angle { get; }

    public int Length { get; set; }

    /// <summary>Konce kmene (pro kolize a vzdálenost).</summary>
    public (double X1, double Y1, double X2, double Y2) Ends()
    {
        var (dx, dy) = (Math.Cos(Angle) * Length / 2, Math.Sin(Angle) * Length / 2);
        return (X - dx, Y - dy, X + dx, Y + dy);
    }

    public double DistanceTo(double x, double y)
    {
        var (x1, y1, x2, y2) = Ends();
        return Geometry.DistanceToSegment(x, y, x1, y1, x2, y2);
    }
}

/// <summary>
/// Kácení a dřevo (návrh 0.8, kap. 2). Strom se kácí jen sekerou, každé seknutí se počítá. Po posledním strom
/// zapraská (warnSeconds) a padne na stranu od toho, kdo sekal: kdo je v pruhu pádu, je zraněný. Zbude pařez
/// a ležící kmen, který blokuje cestu. Z kmene se sekerou odsekávají klady po 1 m, klad se rozštípe na díly dřeva
/// a díl dřeva na třísky. Klad jde zvednout (jeden) a nést. Za pokácený strom vyroste poblíž mladý strom
/// (z pařezu nic), za pár herních let je z něj normální strom. Ukládá se jen změna proti světu ze seedu.
/// </summary>
public sealed class Forestry
{
    private readonly Dictionary<int, int> _hits = [];
    private readonly Dictionary<int, Falling> _falling = [];
    private readonly Dictionary<int, double> _stumps = [];
    private readonly List<Timber> _timbers = [];
    private readonly Dictionary<int, double> _lastChop = [];
    private readonly Dictionary<int, Sapling> _saplings = [];
    private readonly Dictionary<int, Tree> _grown = [];
    private double _time;
    private int _nextId = 1;

    private sealed class Falling
    {
        public double Angle;
        public double SecondsLeft;
    }

    /// <summary>Mladý strom: druh, výška, až doroste, a kdy (měsíce kalendáře) byl zasazen. Stage = poslaný stupeň růstu.</summary>
    private sealed class Sapling
    {
        public TreeKind Kind;
        public double Height;
        public double PlantedMonths;
        public double Growth;
        public int Stage;
    }

    /// <summary>Roste o 1 při každé změně, kterou vidí hráči (pád, pařez, dřevo na zemi).</summary>
    public int Version { get; private set; }

    public IReadOnlyList<Timber> Timbers => _timbers;

    /// <summary>Strom stojí celý (nepadá a není z něj pařez).</summary>
    public bool IsStanding(int tileIndex)
    {
        return !_stumps.ContainsKey(tileIndex) && !_falling.ContainsKey(tileIndex);
    }

    /// <summary>
    /// Stromy, které nejsou celé ze seedu: padající (úhel pádu), pařezy, mladé (růst 0 až 1 po stupních)
    /// a dorostlé (druh a výška, jsou to normální stromy).
    /// </summary>
    public IEnumerable<(int TileIndex, TreeState State, double Angle, TreeKind Kind, double Height, double Growth)> TreeStates()
    {
        foreach (var falling in _falling)
        {
            yield return (falling.Key, TreeState.Falling, falling.Value.Angle, default, 0, 1);
        }

        foreach (var stump in _stumps.Keys)
        {
            yield return (stump, TreeState.Stump, 0, default, 0, 1);
        }

        var stages = GameData.Trees.Regrowth.Stages;
        foreach (var (tile, sapling) in _saplings)
        {
            yield return (tile, TreeState.Sapling, 0, sapling.Kind, sapling.Height, (double)sapling.Stage / stages);
        }

        foreach (var (tile, tree) in _grown)
        {
            if (IsStanding(tile))
            {
                yield return (tile, TreeState.Grown, 0, tree.Kind, tree.Height, 1);
            }
        }
    }

    /// <summary>Dorostlý mladý strom na dlaždici (je to normální strom), nebo null.</summary>
    public Tree? GrownAt(int tileIndex)
    {
        return _grown.TryGetValue(tileIndex, out var tree) ? tree : null;
    }

    /// <summary>Mladý strom, který už povyrostl (překáží), nebo dorostlý strom.</summary>
    public bool HasYoungTrunk(int tileIndex)
    {
        return _grown.ContainsKey(tileIndex)
            || (_saplings.TryGetValue(tileIndex, out var sapling) && sapling.Growth >= GameData.Trees.Regrowth.BlocksFrom);
    }

    /// <summary>Mladé stromy (dlaždice, druh, růst 0 až 1), pro testy.</summary>
    public IEnumerable<(int TileIndex, TreeKind Kind, double Growth)> Saplings()
    {
        return _saplings.Select(s => (s.Key, s.Value.Kind, s.Value.Growth));
    }

    /// <summary>Kolik seknutí už strom dostal (pro testy).</summary>
    public int HitsOf(int tileIndex)
    {
        return _hits.TryGetValue(tileIndex, out var hits) ? hits : 0;
    }

    public Timber? Find(int id)
    {
        return _timbers.Find(t => t.Id == id);
    }

    /// <summary>Seknutí do stromu na dlaždici sekerou. Po posledním seknutí strom začne padat směrem od hráče.</summary>
    public WoodResult Chop(Island island, int tileX, int tileY, int playerId, double playerX, double playerY, Inventory inventory)
    {
        if (!island.InBounds(tileX, tileY) || !island.HasTree(island.Index(tileX, tileY)))
        {
            return WoodResult.NotFound;
        }

        var tile = island.Index(tileX, tileY);
        var (tx, ty) = (tileX + 0.5, tileY + 0.5);
        var check = CanSwing(playerId, Distance(tx, ty, playerX, playerY), inventory);
        if (check is not null)
        {
            return check.Value;
        }

        var broke = WearAxe(inventory);
        var hits = HitsOf(tile) + 1;
        if (hits < GameData.Trees.For(island.TreeAt(tile)!.Value.Kind).Hits)
        {
            _hits[tile] = hits;
            return broke ? WoodResult.AxeBroke : WoodResult.Chopped;
        }

        _hits.Remove(tile);
        var angle = Math.Atan2(ty - playerY, tx - playerX);
        _falling[tile] = new Falling { Angle = angle, SecondsLeft = GameData.Wood.WarnSeconds };
        Version++;
        return broke ? WoodResult.AxeBroke : WoodResult.TreeFalling;
    }

    /// <summary>
    /// Sekerou do dřeva na zemi: z kmene odsekne klad (od špičky), klad rozštípe na díly dřeva do batohu.
    /// </summary>
    public WoodResult ChopTimber(int timberId, int playerId, double playerX, double playerY, Inventory inventory)
    {
        var timber = Find(timberId);
        if (timber is null)
        {
            return WoodResult.NotFound;
        }

        var check = CanSwing(playerId, timber.DistanceTo(playerX, playerY), inventory);
        if (check is not null)
        {
            return check.Value;
        }

        WoodResult result;
        if (timber.Kind == TimberKind.Trunk)
        {
            CutPiece(timber);
            result = WoodResult.PieceCut;
        }
        else
        {
            var wood = GameData.Trees.For(timber.Tree).WoodPerPiece;
            if (inventory.RoomFor(Item.Wood) < wood)
            {
                return WoodResult.InventoryFull;
            }

            for (var i = 0; i < wood; i++)
            {
                inventory.TryAdd(Item.Wood);
            }

            _timbers.Remove(timber);
            result = WoodResult.PieceSplit;
        }

        Version++;
        return WearAxe(inventory) ? WoodResult.AxeBroke : result;
    }

    /// <summary>Zvedne klad ze země (jen jeden, kmen je moc těžký).</summary>
    public WoodResult PickUp(int timberId, double playerX, double playerY, Inventory inventory)
    {
        var timber = Find(timberId);
        if (timber is null)
        {
            return WoodResult.NotFound;
        }

        if (timber.Kind != TimberKind.Piece)
        {
            return WoodResult.TooHeavy;
        }

        if (timber.DistanceTo(playerX, playerY) > GameData.Wood.Reach)
        {
            return WoodResult.TooFar;
        }

        if (inventory.Count(ItemKind.LogPiece) > 0)
        {
            return WoodResult.AlreadyCarrying;
        }

        if (!inventory.TryAdd(new Item(ItemKind.LogPiece, Tree: timber.Tree)))
        {
            return WoodResult.InventoryFull;
        }

        _timbers.Remove(timber);
        Version++;
        return WoodResult.PickedUp;
    }

    /// <summary>Položí nesený klad kousek před sebe (napříč směru chůze). Do vody ne, pak k nohám.</summary>
    public WoodResult Drop(Island island, Inventory inventory, double playerX, double playerY, double facing)
    {
        var index = inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == ItemKind.LogPiece);
        if (index < 0)
        {
            return WoodResult.NotCarrying;
        }

        var piece = inventory.TakeOne(index)!;
        var (x, y) = (playerX + Math.Cos(facing) * 0.7, playerY + Math.Sin(facing) * 0.7);
        if (!Placement.OnDryLand(island, x, y))
        {
            (x, y) = (playerX, playerY);
        }

        _timbers.Add(new Timber(_nextId++, TimberKind.Piece, piece.Tree, x, y, facing + Math.PI / 2, 1));
        Version++;
        return WoodResult.Dropped;
    }

    /// <summary>Naštípá jeden díl dřeva z batohu na třísky (sekerou).</summary>
    public static WoodResult SplitWood(Inventory inventory, int stackIndex)
    {
        if (stackIndex < 0 || stackIndex >= inventory.Stacks.Count || inventory.Stacks[stackIndex].Item.Kind != ItemKind.Wood)
        {
            return WoodResult.CannotSplit;
        }

        if (inventory.Count(ItemKind.Axe) == 0)
        {
            return WoodResult.NeedAxe;
        }

        var wood = inventory.TakeOne(stackIndex)!;
        var splinters = GameData.Wood.SplintersPerWood;
        var added = 0;
        while (added < splinters && inventory.TryAdd(Item.Splinter))
        {
            added++;
        }

        if (added < splinters)
        {
            inventory.TryRemove(ItemKind.Splinter, added);
            inventory.TryAdd(wood);
            return WoodResult.InventoryFull;
        }

        return WearAxe(inventory) ? WoodResult.AxeBroke : WoodResult.WoodSplit;
    }

    /// <summary>
    /// Čas běží: padající stromy dopadnou. Kdo je v pruhu pádu (hráč i zvíře), je zraněný, zbude pařez a kmen.
    /// </summary>
    public void Update(Island island, Animals animals, IReadOnlyList<AnimalTarget> players, double realSeconds, GameClock clock)
    {
        _time += realSeconds;
        foreach (var tile in _falling.Keys.ToList())
        {
            var falling = _falling[tile];
            falling.SecondsLeft -= realSeconds;
            if (falling.SecondsLeft <= 0)
            {
                Fall(island, animals, players, tile, falling.Angle, clock);
            }
        }

        Grow(island, clock.CalendarMonths);
    }

    /// <summary>
    /// Mladé stromy rostou (podle měsíců kalendáře). Dorostlý je normální strom. U stavby hráče mladý strom uschne.
    /// Hráčům se pošle jen změna stupně růstu (stages za celý růst).
    /// </summary>
    private void Grow(Island island, double months)
    {
        var rules = GameData.Trees.Regrowth;
        foreach (var tile in _saplings.Keys.ToList())
        {
            var sapling = _saplings[tile];
            sapling.Growth = Math.Clamp((months - sapling.PlantedMonths) / (rules.GrowYears * GameData.Settings.MonthsPerYear), 0, 1);
            var stage = (int)Math.Floor(sapling.Growth * rules.Stages);
            if (stage == sapling.Stage)
            {
                continue;
            }

            sapling.Stage = stage;
            Version++;
            var (x, y) = (tile % island.Width + 0.5, tile / island.Width + 0.5);
            if (island.Structures.Near(x, y, rules.StructureClearance).Any())
            {
                _saplings.Remove(tile);
            }
            else if (sapling.Growth >= 1)
            {
                _saplings.Remove(tile);
                _grown[tile] = new Tree(tile, x, y, Hash.Hash2(tile, 3, 77), sapling.Kind, sapling.Height);
            }
        }
    }

    /// <summary>
    /// Za pokácený strom zasadí mladý strom stejného druhu kousek od pařezu: na dlaždici, kde stromy rostou,
    /// volné (bez kmene, pařezu, mladého stromu a dřeva) a daleko od staveb hráčů. Když se místo nenajde, nic.
    /// </summary>
    private void PlantSapling(Island island, Tree felled, double months)
    {
        var rules = GameData.Trees.Regrowth;
        var rng = new Mulberry32(unchecked(island.Seed ^ (felled.TileIndex * 7919) ^ _stumps.Count));
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var angle = rng.NextDouble() * Math.PI * 2;
            var distance = rules.MinDistance + rng.NextDouble() * (rules.MaxDistance - rules.MinDistance);
            var tx = (int)Math.Floor(felled.X + Math.Cos(angle) * distance);
            var ty = (int)Math.Floor(felled.Y + Math.Sin(angle) * distance);
            if (!island.InBounds(tx, ty))
            {
                continue;
            }

            var tile = island.Index(tx, ty);
            var (x, y) = (tx + 0.5, ty + 0.5);
            if (!GameData.WorldGen.TreeChance.ContainsKey(island.Tiles[tile])
                || island.HasTrunk(tile) || _stumps.ContainsKey(tile) || _saplings.ContainsKey(tile)
                || island.Rocks.At(tile) is not null || Blocks(x, y, 0.3)
                || island.Structures.Near(x, y, rules.StructureClearance).Any())
            {
                continue;
            }

            var species = GameData.Trees.For(felled.Kind);
            var height = Math.Round(species.MinHeight + (species.MaxHeight - species.MinHeight) * rng.NextDouble(), 1);
            _saplings[tile] = new Sapling { Kind = felled.Kind, Height = height, PlantedMonths = months };
            return;
        }
    }

    /// <summary>Ležící kmen překáží (válec trunkRadius kolem úsečky kmene). Klad na zemi ne.</summary>
    public bool Blocks(double x, double y, double radius)
    {
        var reach = radius + GameData.Wood.TrunkRadius;
        foreach (var timber in _timbers)
        {
            if (timber.Kind == TimberKind.Trunk && timber.DistanceTo(x, y) < reach)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Úsečka (zeď) by vedla přes ležící kmen.</summary>
    public bool BlocksSegment(double x1, double y1, double x2, double y2, double reach)
    {
        foreach (var timber in _timbers.Where(t => t.Kind == TimberKind.Trunk))
        {
            var (a1, b1, a2, b2) = timber.Ends();
            var r = reach + GameData.Wood.TrunkRadius;
            if (Geometry.DistanceToSegment(a1, b1, x1, y1, x2, y2) < r || Geometry.DistanceToSegment(a2, b2, x1, y1, x2, y2) < r
                || Geometry.DistanceToSegment(x1, y1, a1, b1, a2, b2) < r || Geometry.DistanceToSegment(x2, y2, a1, b1, a2, b2) < r)
            {
                return true;
            }
        }

        return false;
    }

    public ForestrySave Export()
    {
        // Padající strom se uloží jako strom, kterému chybí poslední seknutí.
        var hits = new Dictionary<int, int>(_hits);
        foreach (var tile in _falling.Keys)
        {
            hits[tile] = int.MaxValue / 2;
        }

        return new ForestrySave
        {
            Hits = hits,
            Stumps = new Dictionary<int, double>(_stumps),
            Timbers = _timbers.Select(t => new TimberSave(t.Id, t.Kind, t.Tree, t.X, t.Y, t.Angle, t.Length)).ToList(),
            NextTimberId = _nextId,
            Saplings = _saplings.Select(s => new SaplingSave(s.Key, s.Value.Kind, s.Value.Height, s.Value.PlantedMonths)).ToList(),
            Grown = _grown.Values.Select(t => new SaplingSave(t.TileIndex, t.Kind, t.Height, 0)).ToList(),
        };
    }

    public void Restore(Island island, ForestrySave save)
    {
        foreach (var (tile, hits) in save.Hits)
        {
            if (island.TreeAt(tile) is { } tree)
            {
                _hits[tile] = Math.Min(hits, GameData.Trees.For(tree.Kind).Hits - 1);
            }
        }

        foreach (var (tile, at) in save.Stumps)
        {
            _stumps[tile] = at;
        }

        _timbers.AddRange(save.Timbers.Select(t => new Timber(t.Id, t.Kind, t.Tree, t.X, t.Y, t.Angle, t.Length)));
        foreach (var sapling in save.Saplings)
        {
            _saplings[sapling.TileIndex] = new Sapling { Kind = sapling.Kind, Height = sapling.Height, PlantedMonths = sapling.PlantedMonths, Stage = -1 };
        }

        foreach (var grown in save.Grown)
        {
            var (x, y) = (grown.TileIndex % island.Width + 0.5, grown.TileIndex / island.Width + 0.5);
            _grown[grown.TileIndex] = new Tree(grown.TileIndex, x, y, Hash.Hash2(grown.TileIndex, 3, 77), grown.Kind, grown.Height);
        }

        _nextId = Math.Max(save.NextTimberId, _timbers.Count == 0 ? 1 : _timbers.Max(t => t.Id) + 1);
        Version++;
    }

    /// <summary>Strom dopadl: zranění v pruhu pádu, pařez a ležící kmen (celé metry nad pařezem).</summary>
    private void Fall(Island island, Animals animals, IReadOnlyList<AnimalTarget> players, int tile, double angle, GameClock clock)
    {
        var now = clock.TotalMinutes;
        var rules = GameData.Wood;
        var tree = island.TreeAt(tile)!.Value;
        var (dx, dy) = (Math.Cos(angle), Math.Sin(angle));
        var (x2, y2) = (tree.X + dx * tree.Height, tree.Y + dy * tree.Height);
        var half = rules.FallWidth / 2;
        foreach (var player in players)
        {
            if (!player.Character.IsDead && Geometry.DistanceToSegment(player.X, player.Y, tree.X, tree.Y, x2, y2) <= half)
            {
                player.Character.HitByTree(rules.FallSeverity, now);
            }
        }

        animals.Crush(tree.X, tree.Y, x2, y2, half, rules.FallAnimalDamage, now);
        var length = Math.Max(1, (int)Math.Floor(tree.Height - rules.StumpHeight));
        var start = rules.TrunkRadius + 0.1;
        var center = start + length / 2.0;
        _timbers.Add(new Timber(_nextId++, TimberKind.Trunk, tree.Kind, tree.X + dx * center, tree.Y + dy * center, angle, length));
        _falling.Remove(tile);
        _stumps[tile] = now;
        PlantSapling(island, tree, clock.CalendarMonths);
        Version++;
    }

    /// <summary>Odsekne metr od špičky kmene. Poslední metr kmene je rovnou klad.</summary>
    private void CutPiece(Timber trunk)
    {
        if (trunk.Length <= 1)
        {
            trunk.Kind = TimberKind.Piece;
            return;
        }

        var (dx, dy) = (Math.Cos(trunk.Angle), Math.Sin(trunk.Angle));
        var tip = trunk.Length / 2.0;
        _timbers.Add(new Timber(_nextId++, TimberKind.Piece, trunk.Tree, trunk.X + dx * (tip - 0.5), trunk.Y + dy * (tip - 0.5), trunk.Angle, 1));
        trunk.Length--;
        trunk.X -= dx * 0.5;
        trunk.Y -= dy * 0.5;
    }

    /// <summary>Jde seknout? Vrací důvod, proč ne, nebo null. Zapamatuje si čas seknutí (rytmus sekání).</summary>
    private WoodResult? CanSwing(int playerId, double distance, Inventory inventory)
    {
        var rules = GameData.Wood;
        if (distance > rules.Reach)
        {
            return WoodResult.TooFar;
        }

        if (inventory.Count(ItemKind.Axe) == 0)
        {
            return WoodResult.NeedAxe;
        }

        if (_lastChop.TryGetValue(playerId, out var last) && _time - last < rules.ChopCooldownSeconds)
        {
            return WoodResult.TooSoon;
        }

        _lastChop[playerId] = _time;
        return null;
    }

    /// <summary>Sekera se seknutím opotřebuje. Vrací true, když se rozpadla.</summary>
    private static bool WearAxe(Inventory inventory)
    {
        var index = inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == ItemKind.Axe);
        if (index < 0)
        {
            return false;
        }

        var axe = inventory.Stacks[index].Item;
        if (axe.Uses <= 1)
        {
            inventory.TakeOne(index);
            return true;
        }

        inventory.ReplaceOne(index, axe with { Uses = axe.Uses - 1 });
        return false;
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }
}

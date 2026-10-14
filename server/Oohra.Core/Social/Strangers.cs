using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Secret;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Social;

/// <summary>Člověk z cizího kmene (NPC, prototyp npcs v part5.js).</summary>
public sealed class Stranger
{
    public Stranger(int id, int tribe, double x, double y, double homeX, double homeY, double facing)
    {
        Id = id;
        Tribe = tribe;
        X = x;
        Y = y;
        HomeX = homeX;
        HomeY = homeY;
        Facing = facing;
    }

    public int Id { get; }

    /// <summary>Kmen 0 nebo 1 (každý má jiný jazyk).</summary>
    public int Tribe { get; }

    public double X { get; internal set; }

    public double Y { get; internal set; }

    /// <summary>Oheň tábora: sem se vrací a odsud se nevzdálí.</summary>
    public double HomeX { get; }

    public double HomeY { get; }

    /// <summary>Kam se dívá (radiány, 0 = východ, y roste na jih).</summary>
    public double Facing { get; internal set; }

    public StrangerState State { get; internal set; }

    /// <summary>Právě se posunul (i při toulání kolem tábora), klient ukáže chůzi.</summary>
    public bool Moving { get; internal set; }

    /// <summary>Co klient vidí: toulání a cesta jsou chůze, jinak stav.</summary>
    public StrangerState Shown => State == StrangerState.Idle && Moving ? StrangerState.Walk : State;

    internal StrangerAction Action { get; set; }

    internal double Timer { get; set; }

    internal double TargetX { get; set; }

    internal double TargetY { get; set; }

    internal double Stuck { get; set; }

    internal int PlantId { get; set; } = -1;

    internal double TalkCooldown { get; set; }

    internal double AskCooldown { get; set; }
}

/// <summary>Kam cizinec jde (prototyp n.act).</summary>
internal enum StrangerAction
{
    None,
    Eat,
    Drink,
}

/// <summary>
/// Co cizinec řekl nebo ukázal. Word: pojem, který řekl slovem svého kmene (null = nic neřekl). Gesture: kývnutí,
/// vrtění hlavou, „pozor“, krčení rameny nebo ukázání na (PointX, PointY). At: kdy to hráči uvidí (sekundy cizinců).
/// </summary>
public sealed record StrangerAct(int StrangerId, Concept? Word, Gesture? Gesture, double PointX, double PointY, double At);

/// <summary>
/// Cizí kmeny (návrh: NPC cizinci v testu, prototyp initNPCs, npcThink, updateNPCs, handleNPCWord, askByPoint, giveTo).
/// Dva kmeny v táborech ostrova, každý se třemi lidmi. Jedí bezpečné rostliny, pijí, utíkají před zvířaty
/// a mluví svým jazykem. Na slova a gesta hráčů odpovídají: ukážou vodu, jed, kývnou, zavrtí hlavou,
/// a když je hráč požádá o jídlo, jednou za den mu dají upečenou rostlinu. Druhý kmen má prohozená slova
/// pro jídlo a jed, takže hráč, který se učil jen u jednoho kmene, se u druhého splete.
/// </summary>
public sealed class Strangers
{
    private readonly List<Stranger> _all = [];
    private readonly List<StrangerAct> _acts = [];
    private readonly Dictionary<(int Player, int Tribe), double> _lastGift = [];
    private readonly Mulberry32 _rng;
    private readonly Dictionary<int, PlantPart?> _safeParts = [];
    private Season? _safeSeason;
    private double _seconds;

    public Strangers(int worldSeed, Island island)
    {
        _rng = new Mulberry32(worldSeed ^ GameData.Strangers.SeedSalt);
        Vocabularies = CommonWords.ForStrangers(worldSeed);
        var rules = GameData.Strangers;
        for (var tribe = 0; tribe < Math.Min(2, island.Camps.Count); tribe++)
        {
            var camp = island.Camps[tribe];
            var (hx, hy) = (camp.X + 0.5, camp.Y + 0.5);
            for (var i = 0; i < rules.PerTribe; i++)
            {
                var angle = (double)i / rules.PerTribe * 2 * Math.PI + tribe * 1.3;
                var (x, y) = OpenSpotNear(island, hx + Math.Cos(angle) * rules.RingRadius, hy + Math.Sin(angle) * rules.RingRadius);
                var stranger = new Stranger(_all.Count, tribe, x, y, hx, hy, angle + Math.PI)
                {
                    Timer = _rng.NextDouble() * 3,
                    TalkCooldown = _rng.NextDouble() * rules.TalkCooldownSeconds,
                };
                _all.Add(stranger);
            }
        }
    }

    public IReadOnlyList<Stranger> All => _all;

    /// <summary>Jazyky kmenů: pro každý kmen slovo ke každému pojmu.</summary>
    public IReadOnlyList<IReadOnlyDictionary<Concept, byte[]>> Vocabularies { get; }

    /// <summary>Hodiny cizinců (skutečné sekundy od startu), podle nich se počítají At u StrangerAct.</summary>
    public double Seconds => _seconds;

    public Stranger? Find(int id)
    {
        return id >= 0 && id < _all.Count ? _all[id] : null;
    }

    /// <summary>Slovo kmene pro pojem.</summary>
    public byte[] WordOf(int tribe, Concept concept)
    {
        return Vocabularies[tribe][concept];
    }

    /// <summary>Co slovo znamená v jazyce kmene, nebo null (kmen ho nezná).</summary>
    public Concept? ConceptOf(int tribe, IReadOnlyList<byte> word)
    {
        var key = Words.Key(word);
        foreach (var (concept, tribeWord) in Vocabularies[tribe])
        {
            if (Words.Key(tribeWord) == key)
            {
                return concept;
            }
        }

        return null;
    }

    /// <summary>V jaké situaci posluchač cizince vidí, když říká pojem (prototyp CTXT).</summary>
    public static SpeechContext ContextOf(Concept concept)
    {
        return concept switch
        {
            Concept.Food => SpeechContext.Eating,
            Concept.Water => SpeechContext.AtWater,
            Concept.Danger => SpeechContext.AnimalNear,
            Concept.Fire => SpeechContext.AtFire,
            Concept.Yes => SpeechContext.Nodding,
            Concept.No => SpeechContext.ShakingHead,
            _ => SpeechContext.Discarding,
        };
    }

    /// <summary>Nálada slova (prototyp TONE): nebezpečí, ne a jed jsou poplach.</summary>
    public static Tone ToneOf(Concept concept)
    {
        return concept is Concept.Danger or Concept.No or Concept.Poison ? Tone.Alarm : Tone.Calm;
    }

    /// <summary>Co cizinci od posledního volání řekli a ukázali (jen to, na co už došel čas).</summary>
    public List<StrangerAct> TakeActs()
    {
        var ready = _acts.Where(a => a.At <= _seconds).ToList();
        _acts.RemoveAll(a => a.At <= _seconds);
        return ready;
    }

    /// <summary>Jeden krok cizinců (prototyp updateNPCs). players: kde stojí živí hráči.</summary>
    public void Update(Island island, PlantStates plants, IEnumerable<Animal> animals, GameClock clock, double realSeconds, IReadOnlyList<Position> players)
    {
        _seconds += realSeconds;
        var rules = GameData.Strangers;
        var animalList = animals as IReadOnlyCollection<Animal> ?? animals.ToList();
        foreach (var n in _all)
        {
            n.TalkCooldown -= realSeconds;
            n.AskCooldown -= realSeconds;
            if (n.State == StrangerState.Eat)
            {
                n.Timer -= realSeconds;
                if (n.Timer <= 0)
                {
                    n.State = StrangerState.Idle;
                    n.Timer = 2;
                }

                continue;
            }

            n.Timer -= realSeconds;
            if (n.Timer <= 0 && !(n.State == StrangerState.Walk && n.Action != StrangerAction.None))
            {
                Think(n, island, plants, animalList, clock, players);
            }

            Walk(n, island, clock, realSeconds, players);
        }
    }

    /// <summary>
    /// Hráč řekl slovo (nebo udělal gesto jíst či pít). Odpoví nejbližší cizinec do answerDistance (prototyp handleNPCWord).
    /// Když řekne „jídlo“ a kmen mu dlouho nic nedal, dostane upečenou rostlinu. Vrací výsledek daru, nebo null.
    /// </summary>
    public StrangerResult? Hear(Island island, int playerId, double x, double y, IReadOnlyList<byte> word, Inventory inventory, GameClock clock)
    {
        var n = Nearest(x, y, GameData.Strangers.AnswerDistance);
        return n is null ? null : Answer(n, ConceptOf(n.Tribe, word), island, playerId, x, y, inventory, clock);
    }

    /// <summary>Gesto jíst nebo pít rozumí cizinec jako slovo „jídlo“ nebo „voda“ (prototyp gestureTo). Jiná gesta nechá být.</summary>
    public StrangerResult? SeeGesture(Island island, int playerId, double x, double y, Gesture gesture, Inventory inventory, GameClock clock)
    {
        Concept? concept = gesture switch
        {
            Gesture.Eat => Concept.Food,
            Gesture.Drink => Concept.Water,
            _ => null,
        };
        var n = concept is null ? null : Nearest(x, y, GameData.Strangers.AnswerDistance);
        return n is null ? null : Answer(n, concept, island, playerId, x, y, inventory, clock);
    }

    private StrangerResult? Answer(Stranger n, Concept? concept, Island island, int playerId, double x, double y, Inventory inventory, GameClock clock)
    {
        var rules = GameData.Strangers;
        n.Facing = Math.Atan2(y - n.Y, x - n.X);
        var at = _seconds + rules.AnswerDelaySeconds;
        switch (concept)
        {
            case null:
                Act(n, null, Gesture.Puzzled, at);
                return null;
            case Concept.Food:
                if (GiftDue(playerId, n.Tribe, rules.GiftEveryDays, clock))
                {
                    Act(n, Concept.Yes, Gesture.Yes, at);
                    return GiveBack(island, playerId, n, inventory, clock);
                }

                Act(n, Concept.No, Gesture.No, at);
                return null;
            case Concept.Water:
                var pond = NearestPond(island, n.X, n.Y, rules.PointPondRadius);
                Act(n, Concept.Water, pond is null ? null : Gesture.Point, at, pond?.X ?? 0, pond?.Y ?? 0);
                return null;
            case Concept.Danger:
                Act(n, Concept.Danger, Gesture.Beware, at);
                return null;
            case Concept.Fire:
                Act(n, Concept.Fire, Gesture.Point, at, n.HomeX, n.HomeY);
                return null;
            case Concept.Yes:
                Act(n, null, Gesture.Yes, at);
                return null;
            case Concept.No:
                Act(n, null, Gesture.No, at);
                return null;
            default:
                var bad = NearestHarmfulPlant(island, n.X, n.Y, rules.PointPoisonRadius, clock);
                Act(n, Concept.Poison, bad is null ? Gesture.No : Gesture.Point, at, bad?.X ?? 0, bad?.Y ?? 0);
                return null;
        }
    }

    /// <summary>
    /// Hráč ukázal cizinci na rostlinu (prototyp askByPoint). Nejbližší cizinec do askDistance odpoví „jídlo“
    /// a kývne, nebo „jed“ a zavrtí hlavou. Vrací, jestli někdo odpověděl.
    /// </summary>
    public bool AskAbout(Island island, PlantInstance plant, double x, double y, GameClock clock)
    {
        var rules = GameData.Strangers;
        var n = Nearest(x, y, rules.AskDistance);
        if (n is null || n.AskCooldown > 0)
        {
            return false;
        }

        n.AskCooldown = rules.AskCooldownSeconds;
        n.Facing = Math.Atan2(y - n.Y, x - n.X);
        var safe = SafePart(island, plant.SpeciesId, clock.Season) is not null;
        Act(n, safe ? Concept.Food : Concept.Poison, safe ? Gesture.Yes : Gesture.No, _seconds + rules.AnswerDelaySeconds);
        return true;
    }

    /// <summary>
    /// Hráč dal cizinci jednu věc z batohu (prototyp giveTo). Jídlo, které neškodí, přijme (a za čas dá něco zpátky),
    /// škodlivou rostlinu zahodí a zavrtí hlavou, s ostatním neví, co dělat.
    /// </summary>
    public StrangerResult Give(Island island, int playerId, int strangerId, double x, double y, Inventory inventory, int stackIndex, GameClock clock)
    {
        var rules = GameData.Strangers;
        var n = Find(strangerId);
        if (n is null || stackIndex < 0 || stackIndex >= inventory.Stacks.Count)
        {
            return StrangerResult.NotFound;
        }

        if (Math.Sqrt((n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y)) > rules.GiveDistance)
        {
            return StrangerResult.TooFar;
        }

        n.Facing = Math.Atan2(y - n.Y, x - n.X);
        var at = _seconds + rules.AnswerDelaySeconds;
        var item = inventory.Stacks[stackIndex].Item;
        if (item.Kind != ItemKind.Plant && !item.IsFlesh)
        {
            Act(n, null, Gesture.Puzzled, at);
            return StrangerResult.Puzzled;
        }

        inventory.TakeOne(stackIndex);
        if (item.Plant is not null && Harmful(CompositionCalculator.Compute(item.Plant, island.SpeciesSecrets[item.Plant.SpeciesId])))
        {
            Act(n, Concept.Poison, Gesture.No, at);
            return StrangerResult.Discarded;
        }

        Act(n, Concept.Yes, Gesture.Yes, at);
        return GiftDue(playerId, n.Tribe, rules.GiveBackEveryDays, clock)
            ? GiveBack(island, playerId, n, inventory, clock)
            : StrangerResult.Accepted;
    }

    /// <summary>
    /// Část rostliny, kterou cizinci upečenou jedí (prototyp bestSafePart: plod, kořen, list), nebo null.
    /// Počítá se ze skrytého složení, proto jen na serveru.
    /// </summary>
    public PlantPart? SafePart(Island island, int speciesId, Season season)
    {
        if (_safeSeason != season)
        {
            _safeParts.Clear();
            _safeSeason = season;
        }

        if (_safeParts.TryGetValue(speciesId, out var cached))
        {
            return cached;
        }

        var secret = island.SpeciesSecrets[speciesId];
        var safe = GameData.Strangers.Safe;
        PlantPart? result = null;
        foreach (var part in new[] { PlantPart.Fruit, PlantPart.Root, PlantPart.Leaf })
        {
            if (part == PlantPart.Fruit && !GameData.Archetypes[secret.ArchetypeIndex].HasFruit)
            {
                continue;
            }

            var v = CompositionCalculator.Compute(Baked(speciesId, part, season), secret);
            if (v.Nutrition > safe.MinNutrition && v.Irritant < safe.MaxIrritant && v.Toxin < safe.MaxToxin && v.Neurotoxin < safe.MaxNeurotoxin)
            {
                result = part;
                break;
            }
        }

        _safeParts[speciesId] = result;
        return result;
    }

    /// <summary>Prototyp harmful: cizinec takovou věc nevezme do úst.</summary>
    public static bool Harmful(Composition v)
    {
        var harmful = GameData.Strangers.Harmful;
        return v.Toxin > harmful.Toxin || v.Neurotoxin > harmful.Neurotoxin || v.Irritant > harmful.Irritant;
    }

    /// <summary>
    /// Volné místo co nejblíž (x, y), kde cizinec může stát. Prototyp to nehlídal a cizinec se mohl objevit v kmeni stromu.
    /// </summary>
    private static (double X, double Y) OpenSpotNear(Island island, double x, double y)
    {
        var radius = GameData.Movement.Radius;
        for (var r = 0.0; r <= 3; r += 0.5)
        {
            for (var k = 0; k < 12; k++)
            {
                var (sx, sy) = (x + Math.Cos(k * Math.PI / 6) * r, y + Math.Sin(k * Math.PI / 6) * r);
                if (Movement.IsWalkable(island, sx, sy, radius))
                {
                    return (sx, sy);
                }
            }
        }

        return (x, y);
    }

    private static PlantItem Baked(int speciesId, PlantPart part, Season season)
    {
        return new PlantItem(speciesId, part, Ripeness.Ripe, season, TileType.Grass, Cooking.Baked);
    }

    /// <summary>Prototyp npcThink: utéct před zvířetem, jít se najíst, napít, nebo se toulat kolem tábora.</summary>
    private void Think(Stranger n, Island island, PlantStates plants, IReadOnlyCollection<Animal> animals, GameClock clock, IReadOnlyList<Position> players)
    {
        var rules = GameData.Strangers;
        if (DangerNear(n, animals, players))
        {
            n.State = StrangerState.Flee;
            n.Action = StrangerAction.None;
            n.Timer = rules.FleeSeconds;
            if (n.TalkCooldown <= 0 && PlayerNear(n, players))
            {
                Say(n, Concept.Danger, Gesture.Beware);
            }

            return;
        }

        var r = _rng.NextDouble();
        if (r < rules.EatChance)
        {
            var plant = FindFood(n, island, plants, clock);
            if (plant is { } p)
            {
                GoTo(n, StrangerAction.Eat, p.X, p.Y, rules.GotoEatSeconds);
                n.PlantId = p.Id;
                return;
            }
        }
        else if (r < rules.EatChance + rules.DrinkChance)
        {
            var pond = NearestPond(island, n.HomeX, n.HomeY, rules.PondSearchRadius);
            if (pond is { } water)
            {
                GoTo(n, StrangerAction.Drink, water.X, water.Y, rules.GotoDrinkSeconds);
                return;
            }
        }

        n.State = StrangerState.Idle;
        n.Action = StrangerAction.None;
        n.Timer = rules.IdleMinSeconds + _rng.NextDouble() * (rules.IdleMaxSeconds - rules.IdleMinSeconds);
        var angle = _rng.NextDouble() * 2 * Math.PI;
        var radius = _rng.NextDouble() * rules.WanderRadius;
        n.TargetX = n.HomeX + Math.Cos(angle) * radius;
        n.TargetY = n.HomeY + Math.Sin(angle) * radius;
    }

    private static void GoTo(Stranger n, StrangerAction action, double x, double y, double seconds)
    {
        n.State = StrangerState.Walk;
        n.Action = action;
        n.TargetX = x;
        n.TargetY = y;
        n.Timer = seconds;
    }

    /// <summary>Pohyb k cíli (útěk k táboru). U jídla a vody se zastaví, jí a řekne, co dělá.</summary>
    private void Walk(Stranger n, Island island, GameClock clock, double realSeconds, IReadOnlyList<Position> players)
    {
        var rules = GameData.Strangers;
        var (tx, ty) = n.State == StrangerState.Flee ? (n.HomeX, n.HomeY) : (n.TargetX, n.TargetY);
        var (vx, vy) = (tx - n.X, ty - n.Y);
        var distance = Math.Sqrt(vx * vx + vy * vy);
        if (distance > 0.35)
        {
            n.Facing = Math.Atan2(vy, vx);
            var speed = (n.State == StrangerState.Flee ? rules.FleeSpeed : rules.WalkSpeed) * realSeconds;
            var (nx, ny) = Movement.Step(island, n.X, n.Y, vx, vy, Math.Min(speed, distance));
            var moved = Math.Abs(nx - n.X) + Math.Abs(ny - n.Y) > 1e-6;
            (n.X, n.Y) = (nx, ny);
            n.Stuck = moved ? 0 : n.Stuck + realSeconds;
            n.Moving = moved;
            if (n.Stuck > rules.StuckSeconds || (n.State == StrangerState.Walk && n.Action != StrangerAction.None && n.Timer <= 0))
            {
                n.Stuck = 0;
                n.State = StrangerState.Idle;
                n.Action = StrangerAction.None;
                n.Timer = 1.5;
            }

            return;
        }

        n.Stuck = 0;
        n.Moving = false;
        if (n.Action == StrangerAction.Eat)
        {
            n.State = StrangerState.Eat;
            n.Timer = rules.EatSeconds;
            if (PlayerNear(n, players, rules.TalkDistance) && n.TalkCooldown <= 0)
            {
                Say(n, Concept.Food, Gesture.Point, n.TargetX, n.TargetY);
            }
        }
        else if (n.Action == StrangerAction.Drink)
        {
            n.State = StrangerState.Eat;
            n.Timer = rules.DrinkSeconds;
            if (PlayerNear(n, players, rules.TalkDistance) && n.TalkCooldown <= 0)
            {
                Say(n, Concept.Water, Gesture.Point, n.TargetX, n.TargetY);
            }
        }
        else if (n.State != StrangerState.Idle)
        {
            n.State = StrangerState.Idle;
            n.Timer = 3;
        }

        n.Action = StrangerAction.None;
    }

    /// <summary>Útočící divočák nebo vlk blízko cizince, nebo blízko hráče, který stojí u cizince (prototyp npcThink).</summary>
    private static bool DangerNear(Stranger n, IReadOnlyCollection<Animal> animals, IReadOnlyList<Position> players)
    {
        var rules = GameData.Strangers;
        foreach (var a in animals)
        {
            var attacking = (a.Kind == AnimalKind.Boar && a.State is AnimalState.Alert or AnimalState.Charge)
                || (a.Kind == AnimalKind.Wolf && a.State is AnimalState.Stalk or AnimalState.Alert or AnimalState.Charge);
            if (!attacking)
            {
                continue;
            }

            if (Distance(a.X, a.Y, n.X, n.Y) < rules.DangerDistance)
            {
                return true;
            }

            foreach (var p in players)
            {
                if (Distance(a.X, a.Y, p.X, p.Y) < rules.PlayerDangerDistance && Distance(n.X, n.Y, p.X, p.Y) < rules.PlayerNearDistance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Bezpečná rostlina blízko tábora (prototyp: každá druhá rostlina, s polovičním výběrem).</summary>
    private PlantInstance? FindFood(Stranger n, Island island, PlantStates plants, GameClock clock)
    {
        var rules = GameData.Strangers;
        PlantInstance? best = null;
        var bestDistance = rules.EatSearchRadius + 1;
        for (var i = 0; i < island.Plants.Count; i += 2)
        {
            var plant = island.Plants[i];
            if (Distance(plant.X, plant.Y, n.HomeX, n.HomeY) > rules.EatSearchRadius || plants.IsGone(plant.Id, clock.TotalMinutes)
                || SafePart(island, plant.SpeciesId, clock.Season) is null)
            {
                continue;
            }

            var d = Distance(plant.X, plant.Y, n.X, n.Y);
            if (d < bestDistance && _rng.NextDouble() < 0.5)
            {
                bestDistance = d;
                best = plant;
            }
        }

        return best;
    }

    private PlantInstance? NearestHarmfulPlant(Island island, double x, double y, double radius, GameClock clock)
    {
        PlantInstance? best = null;
        var bestDistance = radius;
        foreach (var plant in island.Plants)
        {
            var d = Distance(plant.X, plant.Y, x, y);
            if (d < bestDistance && SafePart(island, plant.SpeciesId, clock.Season) is null)
            {
                bestDistance = d;
                best = plant;
            }
        }

        return best;
    }

    /// <summary>Střed nejbližší dlaždice jezírka do radius (prototyp nearestPond), nebo null.</summary>
    private static Position? NearestPond(Island island, double x, double y, double radius)
    {
        Position? best = null;
        var bestDistance = radius;
        var r = (int)Math.Ceiling(radius);
        for (var dy = -r; dy <= r; dy++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                var (tx, ty) = ((int)Math.Floor(x) + dx, (int)Math.Floor(y) + dy);
                if (!island.InBounds(tx, ty) || island.TileAt(tx, ty) != TileType.Pond)
                {
                    continue;
                }

                var d = Distance(tx + 0.5, ty + 0.5, x, y);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = new Position(tx + 0.5, ty + 0.5);
                }
            }
        }

        return best;
    }

    /// <summary>Dar od kmene: upečená bezpečná rostlina (prototyp giftBack). Semena hra zatím nemá.</summary>
    private StrangerResult GiveBack(Island island, int playerId, Stranger n, Inventory inventory, GameClock clock)
    {
        var season = clock.Season;
        var safe = Enumerable.Range(0, island.SpeciesSecrets.Count).Where(s => SafePart(island, s, season) is not null).ToList();
        if (safe.Count == 0)
        {
            return StrangerResult.Accepted;
        }

        var species = safe[_rng.NextInt(safe.Count)];
        var gift = Baked(species, SafePart(island, species, season)!.Value, season) with { Day = clock.Day };
        var given = 0;
        for (var i = 0; i < GameData.Strangers.GiftCount; i++)
        {
            given += inventory.TryAdd(gift) ? 1 : 0;
        }

        if (given == 0)
        {
            return StrangerResult.GiftNoRoom;
        }

        _lastGift[(playerId, n.Tribe)] = clock.TotalMinutes;
        return StrangerResult.Gift;
    }

    private bool GiftDue(int playerId, int tribe, double everyDays, GameClock clock)
    {
        return !_lastGift.TryGetValue((playerId, tribe), out var last) || clock.TotalMinutes - last > everyDays * GameClock.MinutesPerDay;
    }

    private void Say(Stranger n, Concept concept, Gesture? gesture, double pointX = 0, double pointY = 0)
    {
        n.TalkCooldown = GameData.Strangers.TalkCooldownSeconds;
        Act(n, concept, gesture, _seconds, pointX, pointY);
    }

    private void Act(Stranger n, Concept? concept, Gesture? gesture, double at, double pointX = 0, double pointY = 0)
    {
        if (concept is not null)
        {
            n.TalkCooldown = GameData.Strangers.TalkCooldownSeconds;
        }

        _acts.Add(new StrangerAct(n.Id, concept, gesture, pointX, pointY, at));
    }

    private Stranger? Nearest(double x, double y, double maxDistance)
    {
        Stranger? best = null;
        var bestDistance = maxDistance;
        foreach (var n in _all)
        {
            var d = Distance(n.X, n.Y, x, y);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = n;
            }
        }

        return best;
    }

    private static bool PlayerNear(Stranger n, IReadOnlyList<Position> players, double? distance = null)
    {
        var max = distance ?? GameData.Strangers.TalkDistance;
        return players.Any(p => Distance(p.X, p.Y, n.X, n.Y) < max);
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }
}

using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Language;
using Oohra.Core.Plants;
using Oohra.Core.Social;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Bots;

/// <summary>
/// Jeden robot: připojí se jako hráč a chová se jako rozumný hráč, aby zátěžový test zkoušel celou hru.
/// Podle potřeb: pije z jezírek (moře je slané), jí rostliny a pamatuje si, po kterých mu bylo zle, peče u ohně,
/// večer nasbírá klacky a kámen a rozdělá oheň, unavený spí. Před rozzuřeným divočákem a vlkem utíká, vyčerpaného
/// divočáka bije, vyrobí si kopí. Mluví a dělá gesta, zve do kmene a přijímá pozvání, chodí za cizinci,
/// kreslí značky, staví koš a ukládá do něj, bere věci z těl a po smrti se narodí u kmene nebo v divočině.
/// Náhoda je tu System.Random s pevným semínkem: roboti nejsou svět, jen hráči.
/// </summary>
public sealed class Bot : IDisposable
{
    private const double Reach = 0.9;
    private const double SearchRadius = 18;

    private readonly Random _random;
    private readonly HashSet<int> _badSpecies = [];
    private readonly HashSet<int> _goodSpecies = [];

    // Obrané rostliny (číslo → do kdy na ně nesahat, sekundy): listy dorůstají, plody zrají.
    private readonly Dictionary<int, double> _emptyPlants = [];
    private int _lastPlant = -1;
    private Snapshot? _lastSnapshot;
    private double _nextDecision;
    private int _lastEatenSpecies = -1;
    private double _lastEatenAt = double.NegativeInfinity;
    private (float X, float Y)? _wander;
    private bool _dreamDone;
    private bool _signatureSet;
    private double _now;

    public Bot(string nickname, int seed)
    {
        Nickname = nickname;
        _random = new Random(seed);
    }

    public string Nickname { get; }

    public GameConnection Connection { get; } = new();

    /// <summary>Kolik snímků s polohami hráčů přišlo (server je posílá 15× za sekundu).</summary>
    public int Snapshots { get; private set; }

    public int Harvests { get; private set; }

    public int Meals { get; private set; }

    public int Drinks { get; private set; }

    public int Deaths { get; private set; }

    public int Fires { get; private set; }

    public int Torches { get; private set; }

    public int Kills { get; private set; }

    public int Words { get; private set; }

    public int Builds { get; private set; }

    public int Gifts { get; private set; }

    public bool Ready => Connection.LoggedIn && Connection.World is not null && Connection.Self is not null;

    /// <summary>Kolik hráčů robot vidí v posledním snímku.</summary>
    public int VisiblePlayers => Connection.Snapshot?.Players.Length ?? 0;

    /// <summary>Snímky počítá vrácením, aby šlo měřit, kolik jich přišlo za úsek.</summary>
    public int TakeSnapshotCount()
    {
        var count = Snapshots;
        Snapshots = 0;
        return count;
    }

    public void Update(double nowSeconds)
    {
        _now = nowSeconds;
        Connection.Poll();
        if (!ReferenceEquals(Connection.Snapshot, _lastSnapshot) && Connection.Snapshot is not null)
        {
            Snapshots++;
            _lastSnapshot = Connection.Snapshot;
        }

        ReadEvents();
        AnswerInvites();
        if (!Ready || nowSeconds < _nextDecision)
        {
            return;
        }

        _nextDecision = nowSeconds + 0.4 + _random.NextDouble() * 0.8;
        Decide();
    }

    // ---------- co se stalo ----------

    private void ReadEvents()
    {
        foreach (var e in Connection.TakeEvents())
        {
            switch (e.Kind)
            {
                case EventKind.Harvest when e.Code == (byte)HarvestResult.Picked:
                    Harvests++;
                    break;
                case EventKind.Harvest when (HarvestResult)e.Code is HarvestResult.NoLeavesLeft or HarvestResult.NoFruit or HarvestResult.PlantGone:
                    if (_lastPlant >= 0)
                    {
                        _emptyPlants[_lastPlant] = _now + 180;
                    }

                    break;
                case EventKind.Drink when (DrinkResult)e.Code is DrinkResult.Clean or DrinkResult.Muddy or DrinkResult.MuddySip:
                    Drinks++;
                    break;
                case EventKind.Fire when e.Code == (byte)FireResult.Lit:
                    Fires++;
                    break;
                case EventKind.Fire when e.Code == (byte)FireResult.TorchLit:
                    Torches++;
                    break;
                case EventKind.Hunt when e.Code is (byte)HuntResult.Killed or (byte)HuntResult.CaughtSmall:
                    Kills++;
                    break;
                case EventKind.Build when e.Code == (byte)BuildResult.Built:
                    Builds++;
                    break;
                case EventKind.Stranger when e.Code == (byte)StrangerResult.Gift:
                    Gifts++;
                    break;
                case EventKind.Body when (BodyEvent)e.Code is BodyEvent.Vomited or BodyEvent.Diarrhea or BodyEvent.Tremor or BodyEvent.Fainted or BodyEvent.HeavyFeeling:
                    // Po čem mu bylo zle, to už nejí (otrava přichází se zpožděním, proto jen nedávné jídlo).
                    if (_lastEatenSpecies >= 0 && _now - _lastEatenAt < 240)
                    {
                        _badSpecies.Add(_lastEatenSpecies);
                        _goodSpecies.Remove(_lastEatenSpecies);
                    }

                    break;
            }
        }
    }

    private void AnswerInvites()
    {
        foreach (var inviter in Connection.TakeInvites())
        {
            Connection.AnswerInvite(inviter, _random.NextDouble() < 0.7);
        }
    }

    // ---------- rozhodování ----------

    private void Decide()
    {
        var me = Connection.Self!;
        if (me.Dead)
        {
            Deaths++;
            Connection.SendInput(0, 0);
            Connection.Reborn(Connection.TribeMembers.Length > 0 && _random.NextDouble() < 0.5 ? BirthPlace.Tribe : BirthPlace.Wild);
            _lastEatenSpecies = -1;
            return;
        }

        if (Connection.NewCharacter && !_dreamDone)
        {
            // Sen bez slov robot „přeskočí“.
            _dreamDone = true;
            Connection.DreamOver();
        }

        if (!_signatureSet)
        {
            _signatureSet = true;
            Connection.SetSignature([(byte)_random.Next(10), (byte)_random.Next(10)], RandomBits(10));
        }

        var self = Connection.SelfSnapshot();
        if (self is null)
        {
            return;
        }

        var (x, y) = (self.Value.X, self.Value.Y);
        if (me.Sleeping)
        {
            if (me.Energy > 92 || Danger(x, y) is not null)
            {
                Connection.ToggleSleep();
            }

            return;
        }

        if (Survive(me, x, y) || Social(x, y))
        {
            return;
        }

        Wander(x, y);
    }

    /// <summary>Potřeby a nebezpečí. Vrací true, když robot něco udělal.</summary>
    private bool Survive(SelfState me, float x, float y)
    {
        if (Danger(x, y) is { } animal)
        {
            return FightOrFlee(animal, x, y);
        }

        if (me.Hydration < 55)
        {
            return DrinkFromPond(x, y);
        }

        if (me.Satiety < 65 && EatSomething(me))
        {
            return true;
        }

        if (me.Satiety < 70 && Forage(x, y))
        {
            return true;
        }

        var night = Hour() >= 19 || Hour() < 6 || me.Warmth < 30;
        if (night && !me.NearFire && MakeFire(x, y))
        {
            return true;
        }

        // U ohně si v noci občas zapálí pochodeň (na cestu tmou, vlci se jí bojí).
        if (night && me.NearFire && Connection.SelfSnapshot() is { Torch: false } && Count(ItemKind.Stick) >= 1 && _random.NextDouble() < 0.2)
        {
            Connection.LightTorch();
            return true;
        }

        if (me.Energy < 25)
        {
            Connection.SendInput(0, 0);
            Connection.ToggleSleep();
            return true;
        }

        if (Connection.Inventory.Stacks.Length >= Connection.Inventory.Capacity && TidyBackpack())
        {
            return true;
        }

        if (!night && Count(ItemKind.Stick) < 6 && _random.NextDouble() < 0.25 && Gather(TreeTile(x, y), x, y, sticks: true))
        {
            return true;
        }

        if (Count(ItemKind.Spear) == 0 && Count(ItemKind.Stick) > 0 && Count(ItemKind.Stone) > 0)
        {
            Connection.MakeSpear();
            return true;
        }

        return false;
    }

    /// <summary>Rozzuřený divočák, plížící se vlk nebo syčící had poblíž, nebo vyčerpaný divočák na ránu.</summary>
    private AnimalView? Danger(float x, float y)
    {
        foreach (var a in Connection.Animals)
        {
            var d = Dist(a.X, a.Y, x, y);
            var angry = (a.Kind == AnimalKind.Boar && a.State is AnimalState.Alert or AnimalState.Charge && d < 7)
                || (a.Kind == AnimalKind.Wolf && a.State is AnimalState.Stalk or AnimalState.Alert or AnimalState.Charge && d < 9)
                || (a.Kind == AnimalKind.Snake && a.State == AnimalState.Alert && d < 3);
            var beatable = a.State == AnimalState.Exhaust && d < 2.5;
            if (angry || beatable)
            {
                return a;
            }
        }

        return null;
    }

    private bool FightOrFlee(AnimalView animal, float x, float y)
    {
        if (animal.State == AnimalState.Exhaust)
        {
            Connection.SendInput(animal.X - x, animal.Y - y);
            Connection.Attack(animal.Id);
            return true;
        }

        // Uskočit stranou od výpadu (kolmo), jinak pryč.
        var (dx, dy) = (x - animal.X, y - animal.Y);
        if (animal.State == AnimalState.Charge)
        {
            (dx, dy) = (-dy, dx);
        }

        Connection.SendInput(dx, dy);
        return true;
    }

    private bool DrinkFromPond(float x, float y)
    {
        var pond = Nearest(TileType.Pond, x, y);
        if (pond is null)
        {
            return false;
        }

        var (tx, ty) = pond.Value;
        if (Dist(tx + 0.5f, ty + 0.5f, x, y) < 1.6)
        {
            Connection.SendInput(0, 0);
            Connection.Drink(tx, ty);
        }
        else
        {
            Connection.SendInput(tx + 0.5f - x, ty + 0.5f - y);
        }

        return true;
    }

    /// <summary>Sní (nebo u ohně upeče) něco, co mu neublížilo. Neznámé jen ochutná nebo sní malou šancí.</summary>
    private bool EatSomething(SelfState me)
    {
        var stacks = Connection.Inventory.Stacks;
        for (var i = 0; i < stacks.Length; i++)
        {
            var s = stacks[i];
            var food = s.Kind is ItemKind.Plant or ItemKind.Meat or ItemKind.Fish;
            if (!food || (s.Kind == ItemKind.Plant && _badSpecies.Contains(s.SpeciesId)))
            {
                continue;
            }

            if (me.NearFire && s.Cooking == Cooking.Raw)
            {
                Connection.Bake(i);
                return true;
            }

            var known = s.Kind != ItemKind.Plant || _goodSpecies.Contains(s.SpeciesId);
            if (!known && _random.NextDouble() < 0.5)
            {
                Connection.Taste(i);
                return true;
            }

            Connection.Eat(i);
            Meals++;
            if (s.Kind == ItemKind.Plant)
            {
                _lastEatenSpecies = s.SpeciesId;
                _lastEatenAt = _now;
                _goodSpecies.Add(s.SpeciesId);
            }

            return true;
        }

        return false;
    }

    /// <summary>Plný batoh: zahodí rostlinu, po které mu bylo zle, jinak hromádku rostlin, které má nejvíc.</summary>
    private bool TidyBackpack()
    {
        var stacks = Connection.Inventory.Stacks;
        var bad = Array.FindIndex(stacks, s => s.Kind == ItemKind.Plant && _badSpecies.Contains(s.SpeciesId));
        var drop = bad >= 0 ? bad : Array.FindIndex(stacks, s => s.Kind == ItemKind.Plant);
        if (drop < 0)
        {
            return false;
        }

        Connection.Drop(drop);
        return true;
    }

    /// <summary>Dojde k rostlině, po které mu nebylo zle, a utrhne ji.</summary>
    private bool Forage(float x, float y)
    {
        var plant = NearestPlant(x, y);
        if (plant < 0)
        {
            return false;
        }

        var target = Connection.World!.Plants[plant];
        if (Dist(target.X, target.Y, x, y) < Reach)
        {
            Connection.SendInput(0, 0);
            _lastPlant = plant;
            Connection.Harvest(plant, PartToPick(plant));
        }
        else
        {
            Connection.SendInput(target.X - x, target.Y - y);
        }

        return true;
    }

    /// <summary>Plod, když na rostlině nějaký je (je vidět), jinak list nebo kořen.</summary>
    private PlantPart PartToPick(int plant)
    {
        var visuals = Connection.PlantVisuals;
        var fruit = plant < visuals.Length && visuals[plant] is PlantVisual.RipeFruit or PlantVisual.UnripeFruit or PlantVisual.OverripeFruit;
        return fruit && _random.NextDouble() < 0.7 ? PlantPart.Fruit : _random.NextDouble() < 0.6 ? PlantPart.Leaf : PlantPart.Root;
    }

    /// <summary>Večer: nasbírá klacky a kámen a rozdělá oheň vedle sebe.</summary>
    private bool MakeFire(float x, float y)
    {
        if (Count(ItemKind.Stick) >= 3 && Count(ItemKind.Stone) >= 1)
        {
            Connection.SendInput(0, 0);
            // Místo kolem sebe pokaždé jinde (když je tam strom nebo voda, příště to zkusí jinam).
            var angle = _random.NextDouble() * Math.PI * 2;
            Connection.Place(BuildKind.Campfire, x + (float)Math.Cos(angle) * 1.2f, y + (float)Math.Sin(angle) * 1.2f, 0);
            return true;
        }

        return Count(ItemKind.Stick) < 3 ? Gather(TreeTile(x, y), x, y, sticks: true) : Gather(RockTile(x, y), x, y, sticks: false);
    }

    private bool Gather((int X, int Y)? tile, float x, float y, bool sticks)
    {
        if (tile is null)
        {
            return false;
        }

        var (tx, ty) = tile.Value;
        if (Dist(tx + 0.5f, ty + 0.5f, x, y) < 1.4)
        {
            Connection.SendInput(0, 0);
            if (sticks)
            {
                Connection.GatherSticks(tx, ty);
            }
            else
            {
                Connection.TakeStone(tx, ty);
            }
        }
        else
        {
            Connection.SendInput(tx + 0.5f - x, ty + 0.5f - y);
        }

        return true;
    }

    // ---------- společnost ----------

    /// <summary>Občas promluví, zve do kmene, chodí za cizinci, kreslí, staví a bere z těl.</summary>
    private bool Social(float x, float y)
    {
        var roll = _random.NextDouble();
        if (roll < 0.10)
        {
            SayWord();
            return false;
        }

        if (roll < 0.14 && OtherPlayerNear(x, y, 3) is { } other && !Connection.TribeMembers.Contains(other))
        {
            Connection.InviteToTribe(other);
            return true;
        }

        if (roll < 0.22 && TalkToStranger(x, y))
        {
            return true;
        }

        if (roll < 0.24)
        {
            Connection.DrawSign(x + 0.8f, y, RandomBits(14));
            return true;
        }

        if (roll < 0.27 && Count(ItemKind.Stick) >= 6 && Connection.Stockpiles.Length == 0)
        {
            Connection.Place(BuildKind.Stockpile, x + 1.5f, y + 0.5f, 0);
            return true;
        }

        if (roll < 0.30 && StoreInStockpile(x, y))
        {
            return true;
        }

        return roll < 0.34 && LootCorpse(x, y);
    }

    private void SayWord()
    {
        var known = Connection.Vocabulary.Entries;
        var word = known.Length > 0 && _random.NextDouble() < 0.6
            ? known[_random.Next(known.Length)].Word
            : Enumerable.Range(0, 1 + _random.Next(3)).Select(_ => (byte)_random.Next(10)).ToArray();
        Connection.Say(word, (Tone)_random.Next(3));
        Words++;
        if (_random.NextDouble() < 0.3)
        {
            Connection.MakeGesture((Gesture)_random.Next(7));
        }
    }

    /// <summary>Dojde k cizinci a řekne mu slovo, které nejčastěji slyšel u jídla (nebo udělá gesto jíst).</summary>
    private bool TalkToStranger(float x, float y)
    {
        var stranger = Connection.Strangers.OrderBy(s => Dist(s.X, s.Y, x, y)).FirstOrDefault();
        if (Connection.Strangers.Length == 0 || Dist(stranger.X, stranger.Y, x, y) > 25)
        {
            return false;
        }

        if (Dist(stranger.X, stranger.Y, x, y) > 4)
        {
            Connection.SendInput(stranger.X - x, stranger.Y - y);
            return true;
        }

        Connection.SendInput(0, 0);
        var food = Connection.Vocabulary.Entries.OrderByDescending(e => e.Contexts[(int)SpeechContext.Eating]).FirstOrDefault();
        if (food is not null && food.Contexts[(int)SpeechContext.Eating] > 0)
        {
            Connection.Say(food.Word);
            Words++;
        }
        else
        {
            Connection.MakeGesture(Gesture.Eat);
        }

        var plant = Array.FindIndex(Connection.Inventory.Stacks, s => s.Kind == ItemKind.Plant);
        if (plant >= 0 && _random.NextDouble() < 0.3)
        {
            Connection.ShowItem(plant);
        }

        return true;
    }

    private bool StoreInStockpile(float x, float y)
    {
        var mine = Connection.Stockpiles.FirstOrDefault();
        if (mine is null || Connection.Inventory.Stacks.Length == 0)
        {
            return false;
        }

        var structure = Connection.Structures.FirstOrDefault(s => s.Id == mine.StructureId);
        if (Dist(structure.X, structure.Y, x, y) > 1.4)
        {
            Connection.SendInput(structure.X - x, structure.Y - y);
            return true;
        }

        Connection.StoreItem(mine.StructureId, _random.Next(Connection.Inventory.Stacks.Length));
        return true;
    }

    private bool LootCorpse(float x, float y)
    {
        var corpse = Connection.Corpses.FirstOrDefault(c => c.Contents.Stacks.Length > 0 && Dist(c.X, c.Y, x, y) < 12);
        if (corpse is null)
        {
            return false;
        }

        if (Dist(corpse.X, corpse.Y, x, y) > 1.2)
        {
            Connection.SendInput(corpse.X - x, corpse.Y - y);
            return true;
        }

        Connection.TakeFromCorpse(corpse.Id, 0);
        return true;
    }

    /// <summary>Bloudí po ostrově k náhodnému cíli (občas postojí).</summary>
    private void Wander(float x, float y)
    {
        if (_wander is null || Dist(_wander.Value.X, _wander.Value.Y, x, y) < 1.5 || _random.NextDouble() < 0.05)
        {
            var world = Connection.World!;
            _wander = (x + (float)(_random.NextDouble() * 30 - 15), y + (float)(_random.NextDouble() * 30 - 15));
            _wander = (Math.Clamp(_wander.Value.X, 2, world.Width - 2), Math.Clamp(_wander.Value.Y, 2, world.Height - 2));
        }

        if (_random.NextDouble() < 0.1)
        {
            Connection.SendInput(0, 0);
            return;
        }

        Connection.SendInput(_wander.Value.X - x, _wander.Value.Y - y);
    }

    // ---------- pomůcky ----------

    private double Hour()
    {
        var minutes = Connection.Snapshot?.TotalMinutes ?? 0;
        return minutes % 1440 / 60;
    }

    private int Count(ItemKind kind)
    {
        return Connection.Inventory.Stacks.Where(s => s.Kind == kind).Sum(s => s.Count);
    }

    private int? OtherPlayerNear(float x, float y, double distance)
    {
        var snapshot = Connection.Snapshot;
        var other = snapshot?.Players.FirstOrDefault(p => p.Id != Connection.PlayerId && !p.Dead && Dist(p.X, p.Y, x, y) < distance);
        return other is { } found && found.Id != 0 ? found.Id : null;
    }

    private int NearestPlant(float x, float y)
    {
        var plants = Connection.World!.Plants;
        var gone = Connection.PlantVisuals;
        var best = -1;
        var bestDistance = SearchRadius;
        for (var i = 0; i < plants.Length; i++)
        {
            if ((i < gone.Length && gone[i] == PlantVisual.Gone) || _badSpecies.Contains(plants[i].SpeciesId)
                || (_emptyPlants.TryGetValue(i, out var until) && until > _now))
            {
                continue;
            }

            var distance = Dist(plants[i].X, plants[i].Y, x, y);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private (int X, int Y)? Nearest(TileType type, float x, float y)
    {
        var world = Connection.World!;
        (int, int)? best = null;
        var bestDistance = double.MaxValue;
        var r = 40;
        for (var ty = Math.Max(0, (int)y - r); ty < Math.Min(world.Height, (int)y + r); ty++)
        {
            for (var tx = Math.Max(0, (int)x - r); tx < Math.Min(world.Width, (int)x + r); tx++)
            {
                if (world.TileAt(tx, ty) != type)
                {
                    continue;
                }

                var d = Dist(tx + 0.5f, ty + 0.5f, x, y);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = (tx, ty);
                }
            }
        }

        return best;
    }

    private (int X, int Y)? TreeTile(float x, float y)
    {
        var world = Connection.World!;
        var best = world.Trees.OrderBy(t => Dist(t.TileIndex % world.Width + 0.5f, t.TileIndex / world.Width + 0.5f, x, y)).FirstOrDefault();
        return world.Trees.Length == 0 ? null : (best.TileIndex % world.Width, best.TileIndex / world.Width);
    }

    private (int X, int Y)? RockTile(float x, float y)
    {
        var world = Connection.World!;
        var piles = Connection.RockPiles.Where(p => p.Stones > 0).ToList();
        if (piles.Count == 0)
        {
            return null;
        }

        var best = piles.OrderBy(p => Dist(p.TileIndex % world.Width + 0.5f, p.TileIndex / world.Width + 0.5f, x, y)).First();
        return (best.TileIndex % world.Width, best.TileIndex / world.Width);
    }

    private byte[] RandomBits(int points)
    {
        var bits = SignBits.Empty();
        for (var i = 0; i < points; i++)
        {
            SignBits.Set(bits, _random.Next(SignBits.Size), _random.Next(SignBits.Size), true);
        }

        return bits;
    }

    private static double Dist(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}

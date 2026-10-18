using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>Zvíře na ostrově. Poloha v metrech, Dir je směr pohledu (radiány, y mapy na jih).</summary>
public sealed class Animal
{
    public Animal(int id, AnimalKind kind, double x, double y, int hp, double stamina)
    {
        Id = id;
        Kind = kind;
        X = x;
        Y = y;
        DenX = x;
        DenY = y;
        TargetX = x;
        TargetY = y;
        Hp = hp;
        Stamina = stamina;
    }

    public int Id { get; }

    public AnimalKind Kind { get; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Dir { get; set; }

    public AnimalState State { get; set; }

    public int Hp { get; set; }

    /// <summary>Kdy zvíře zemřelo (herní minuty).</summary>
    public double DeadAt { get; set; }

    public bool IsDead => State == AnimalState.Dead;

    // Vnitřní stav chování (prototyp: timer, aggro, stam, tx, ty, den, cdx, cdy, ct, graze).
    internal double Timer { get; set; }

    internal double Aggro { get; set; }

    internal double Stamina { get; set; }

    internal double TargetX { get; set; }

    internal double TargetY { get; set; }

    internal double DenX { get; }

    internal double DenY { get; }

    internal double ChargeX { get; set; }

    internal double ChargeY { get; set; }

    internal double ChargeLeft { get; set; }

    internal bool GoingToPlant { get; set; }

    /// <summary>Hráč, na kterého se zvíře zaměřilo (vlk, divočák), nebo -1.</summary>
    internal int Prey { get; set; } = -1;

    /// <summary>Chytilo se do pasti: zmizí ze světa (úlovek je v pasti).</summary>
    internal bool Trapped { get; set; }
}

/// <summary>Hráč, jak ho vidí zvířata: poloha a tělo (spánek, zranění).</summary>
public sealed record AnimalTarget(int Id, double X, double Y, Character Character);

/// <summary>
/// Zvířata na ostrově (návrh 12.5, prototyp ECO, spawnAnimal, updateAnimals a attack v part4.js a part5.js).
/// Králík a jelen utíkají, králík okusuje rostliny a v noci spí, jeleni drží stádo. Divočák se zaměří
/// na blízkého hráče, vyrazí a pak je vyčerpaný. Vlk se v noci plíží k hráči mimo oheň a úkryt.
/// Prototyp měl jednoho hráče, tady zvířata reagují na nejbližšího hráče.
/// Pohyb běží v sekundách reálného času, smrt a doplňování v herních minutách. Náhoda z vlastního generátoru.
/// </summary>
public sealed class Animals
{
    private const double HitDistance = 0.75;

    private readonly List<Animal> _animals = [];
    private readonly Dictionary<int, double> _lastAttack = [];
    private readonly Dictionary<int, double> _lastFish = [];
    private readonly Mulberry32 _rng;
    private int _nextId = 1;
    private double _time;
    private double _nextRespawnAt = double.NaN;

    public Animals(int seed)
    {
        _rng = new Mulberry32(seed ^ 0xA11A);
    }

    public IReadOnlyList<Animal> All => _animals;

    /// <summary>Zvířata se rodí a doplňují (vypnuté např. v testech). Čas (prodlevy, omráčení) běží vždy.</summary>
    public bool Enabled { get; set; } = true;

    public Animal? Find(int id)
    {
        return _animals.Find(a => a.Id == id);
    }

    /// <summary>Kolik zvířat daného druhu na ostrov patří (prototyp ECO, přepočtené podle plochy ostrova).</summary>
    public static int TargetCount(Island island, AnimalKind kind)
    {
        var population = GameData.Animals.Population;
        var count = kind switch
        {
            AnimalKind.Boar => population.Boar,
            AnimalKind.Rabbit => population.Rabbit,
            AnimalKind.Deer => population.Deer,
            _ => population.Wolf,
        };
        return (int)Math.Round(count * island.Width * island.Height / population.ReferenceArea);
    }

    /// <summary>Zabydlí ostrov zvířaty (prototyp initAnimals). Jeleni začínají u jednoho stáda.</summary>
    public void Populate(Island island, IReadOnlyList<AnimalTarget> players)
    {
        foreach (var kind in new[] { AnimalKind.Boar, AnimalKind.Rabbit, AnimalKind.Deer, AnimalKind.Wolf })
        {
            for (var i = 0; i < TargetCount(island, kind); i++)
            {
                Spawn(island, kind, players);
            }
        }
    }

    /// <summary>Jeden krok: zvířata se hýbou a reagují na hráče, mrtvá mizí, chybějící se pomalu doplňují.</summary>
    public void Update(Island island, Fires fires, PlantStates plants, GameClock clock, double realSeconds, IReadOnlyList<AnimalTarget> players)
    {
        _time += realSeconds;
        foreach (var p in players)
        {
            p.Character.StunSeconds = Math.Max(0, p.Character.StunSeconds - realSeconds);
        }

        var night = clock.Daylight < GameData.Animals.NightDaylightBelow;
        foreach (var animal in _animals)
        {
            if (animal.IsDead)
            {
                continue;
            }

            switch (animal.Kind)
            {
                case AnimalKind.Boar:
                    UpdateBoar(island, animal, clock, realSeconds, players);
                    break;
                case AnimalKind.Wolf:
                    UpdateWolf(island, fires, animal, clock, realSeconds, night, players);
                    break;
                default:
                    UpdatePrey(island, plants, animal, clock, realSeconds, night, players);
                    break;
            }
        }

        _animals.RemoveAll(a => a.Trapped);
        RemoveOldCorpses(clock.TotalMinutes);
        if (!Enabled)
        {
            return;
        }

        Respawn(island, clock.TotalMinutes, players);
    }

    /// <summary>
    /// Rána zvířeti (prototyp attack). Králíka chytí hned. Ostatním ubere životy: pěst málo, kopí víc
    /// (zkušený lovec ještě víc), vyčerpané zvíře po výpadu dostane dvojnásobek. Procvičí lov.
    /// </summary>
    public HuntResult Attack(int animalId, AnimalTarget hunter, Inventory inventory, double now)
    {
        var rules = GameData.Animals.Hunt;
        var animal = Find(animalId);
        if (animal is null || animal.IsDead)
        {
            return HuntResult.NotFound;
        }

        if (Distance(animal.X, animal.Y, hunter.X, hunter.Y) > rules.Reach)
        {
            return HuntResult.TooFar;
        }

        if (_lastAttack.TryGetValue(hunter.Id, out var last) && _time - last < rules.CooldownSeconds)
        {
            return HuntResult.TooSoon;
        }

        _lastAttack[hunter.Id] = _time;
        var c = hunter.Character;
        if (animal.Kind == AnimalKind.Rabbit)
        {
            Kill(animal, now);
            c.Practice(SkillKind.Hunt, rules.PracticeSmall);
            return HuntResult.CaughtSmall;
        }

        var spear = inventory.Count(ItemKind.Spear) > 0;
        double damage = spear ? rules.SpearDamage + (c.Skills.Level(SkillKind.Hunt) >= rules.SpearBonusLevel ? 1 : 0) : rules.FistDamage;
        if (animal.State == AnimalState.Exhaust)
        {
            damage *= rules.ExhaustedMultiplier;
        }

        animal.Hp -= (int)Math.Round(damage);
        animal.Aggro = rules.HitAggroSeconds;
        c.Practice(SkillKind.Hunt, rules.PracticeHit);
        if (animal.Hp <= 0)
        {
            Kill(animal, now);
            c.Practice(SkillKind.Hunt, rules.PracticeKill);
            return HuntResult.Killed;
        }

        if (animal.Kind == AnimalKind.Boar && animal.State == AnimalState.Wander)
        {
            animal.State = AnimalState.Alert;
            animal.Timer = 0.5;
            animal.Prey = hunter.Id;
        }

        return spear ? HuntResult.HitSpear : HuntResult.HitFist;
    }

    /// <summary>Padající strom: zvířata v pruhu (do halfWidth od úsečky) dostanou ránu, slabší zvíře zabije.</summary>
    public void Crush(double x1, double y1, double x2, double y2, double halfWidth, int damage, double now)
    {
        foreach (var animal in _animals.Where(a => !a.IsDead).ToList())
        {
            if (Geometry.DistanceToSegment(animal.X, animal.Y, x1, y1, x2, y2) > halfWidth)
            {
                continue;
            }

            animal.Hp -= damage;
            if (animal.Hp <= 0)
            {
                Kill(animal, now);
            }
        }
    }

    /// <summary>Stáhne maso z mrtvého zvířete (prototyp meatQty). Zvíře pak zmizí.</summary>
    public HuntResult Butcher(int animalId, Inventory inventory, double playerX, double playerY, double now)
    {
        var animal = Find(animalId);
        if (animal is null)
        {
            return HuntResult.NotFound;
        }

        if (!animal.IsDead)
        {
            return HuntResult.NotDead;
        }

        if (Distance(animal.X, animal.Y, playerX, playerY) > GameData.Animals.Hunt.ButcherReach)
        {
            return HuntResult.TooFar;
        }

        var added = 0;
        for (var i = 0; i < MeatOf(animal.Kind); i++)
        {
            if (inventory.TryAdd(Item.RawMeat(GameClock.DayOf(now))))
            {
                added++;
            }
        }

        if (added == 0)
        {
            return HuntResult.InventoryFull;
        }

        _animals.Remove(animal);
        return HuntResult.Butchered;
    }

    /// <summary>
    /// Rybaření s kopím u jezírka nebo moře (prototyp fish). Šance roste s dovedností Lov.
    /// Mezi pokusy je krátká prodleva.
    /// </summary>
    public HuntResult Fish(Island island, int tileX, int tileY, AnimalTarget fisher, Inventory inventory, double now)
    {
        var rules = GameData.Animals.Fishing;
        if (inventory.Count(ItemKind.Spear) == 0)
        {
            return HuntResult.NeedSpear;
        }

        if (!island.InBounds(tileX, tileY) || island.TileAt(tileX, tileY) is not (TileType.Pond or TileType.Sea))
        {
            return HuntResult.NotWater;
        }

        if (Distance(tileX + 0.5, tileY + 0.5, fisher.X, fisher.Y) > rules.Reach)
        {
            return HuntResult.TooFar;
        }

        if (_lastFish.TryGetValue(fisher.Id, out var last) && _time - last < rules.CooldownSeconds)
        {
            return HuntResult.TooSoon;
        }

        _lastFish[fisher.Id] = _time;
        var c = fisher.Character;
        if (_rng.NextDouble() >= rules.BaseChance + rules.ChancePerLevel * c.Skills.Level(SkillKind.Hunt))
        {
            c.Practice(SkillKind.Hunt, rules.PracticeMiss);
            return HuntResult.FishEscaped;
        }

        if (!inventory.TryAdd(Item.RawFish(GameClock.DayOf(now))))
        {
            return HuntResult.InventoryFull;
        }

        c.Practice(SkillKind.Hunt, rules.PracticeCatch);
        return HuntResult.FishCaught;
    }

    /// <summary>Kořist u volné pasti se s nějakou šancí chytí (prototyp trapCheck). Zvíře pak zmizí, úlovek je v pasti.</summary>
    private void CheckTraps(Island island, Animal a, double dt)
    {
        var rules = GameData.Animals.Traps;
        foreach (var trap in island.Structures.Near(a.X, a.Y, rules.CatchRange))
        {
            if (trap.Kind != BuildKind.Trap || trap.Caught is not null || Distance(trap.X, trap.Y, a.X, a.Y) >= rules.CatchRange)
            {
                continue;
            }

            var chance = (a.Kind == AnimalKind.Rabbit ? rules.RabbitChance : rules.DeerChance) * dt * rules.RateFactor;
            if (_rng.NextDouble() < chance)
            {
                island.Structures.Catch(trap, a.Kind);
                a.State = AnimalState.Dead;
                a.Trapped = true;
                return;
            }
        }
    }

    /// <summary>Kolik kusů masa zvíře dá (prototyp meatQty).</summary>
    public static int MeatOf(AnimalKind kind)
    {
        var rules = GameData.Animals;
        return kind switch
        {
            AnimalKind.Boar => rules.Boar.Meat,
            AnimalKind.Deer => rules.Deer.Meat,
            AnimalKind.Wolf => rules.Wolf.Meat,
            _ => rules.Rabbit.Meat,
        };
    }

    // ---------- králík a jelen ----------

    private void UpdatePrey(Island island, PlantStates plants, Animal a, GameClock clock, double dt, bool night, IReadOnlyList<AnimalTarget> players)
    {
        var rabbit = a.Kind == AnimalKind.Rabbit;
        var rules = rabbit ? GameData.Animals.Rabbit : GameData.Animals.Deer;
        var asleep = night && rabbit;
        if (a.State == AnimalState.Tired)
        {
            a.Timer -= dt;
            if (a.Timer <= 0)
            {
                a.State = AnimalState.Wander;
                a.Stamina = rules.Stamina;
            }

            return;
        }

        var (nearest, d) = Nearest(players, a.X, a.Y, p => true);
        if (nearest is not null && d < (asleep ? rules.FleeRangeAsleep : rules.FleeRange))
        {
            Flee(island, a, nearest, d, dt, rules);
            CheckTraps(island, a, dt);
            return;
        }

        if (a.State == AnimalState.Flee)
        {
            a.State = AnimalState.Wander;
            a.Stamina = Math.Min(rules.Stamina, a.Stamina + dt);
        }

        if (asleep)
        {
            a.State = AnimalState.Asleep;
            return;
        }

        if (a.State == AnimalState.Asleep)
        {
            a.State = AnimalState.Wander;
        }

        if (a.State == AnimalState.Graze)
        {
            a.Timer -= dt;
            if (a.Timer <= 0)
            {
                a.State = AnimalState.Wander;
            }

            return;
        }

        a.Timer -= dt;
        if (a.Timer <= 0)
        {
            a.Timer = 2 + _rng.NextDouble() * 4;
            ChooseWanderTarget(island, plants, a, clock, rules);
        }

        var dx = a.TargetX - a.X;
        var dy = a.TargetY - a.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > 0.3)
        {
            Move(island, a, dx / distance * rules.WalkSpeed * dt, dy / distance * rules.WalkSpeed * dt, rules.Radius);
            CheckTraps(island, a, dt);
            a.Dir = Math.Atan2(dy, dx);
        }
        else if (a.GoingToPlant)
        {
            a.GoingToPlant = false;
            a.State = AnimalState.Graze;
            a.Timer = rules.GrazeSeconds;
        }
    }

    /// <summary>Útěk od hráče. Jelen se drží stáda. Při útěku se zvíře unaví.</summary>
    private void Flee(Island island, Animal a, AnimalTarget from, double d, double dt, PreyRules rules)
    {
        a.State = AnimalState.Flee;
        a.GoingToPlant = false;
        a.Stamina -= dt;
        if (a.Stamina <= 0)
        {
            a.State = AnimalState.Tired;
            a.Timer = rules.TiredSeconds;
            return;
        }

        d = Math.Max(d, 0.001);
        var nx = (a.X - from.X) / d;
        var ny = (a.Y - from.Y) / d;
        if (a.Kind == AnimalKind.Deer)
        {
            var (hx, hy) = HerdCenter(a);
            var hd = Math.Max(Distance(hx, hy, a.X, a.Y), 1);
            nx = nx * 0.8 + (hx - a.X) / hd * 0.2;
            ny = ny * 0.8 + (hy - a.Y) / hd * 0.2;
        }

        var step = rules.FleeSpeed * dt;
        if (!Move(island, a, nx * step, ny * step, rules.Radius))
        {
            Move(island, a, -ny * step, nx * step, rules.Radius);
        }

        a.Dir = Math.Atan2(ny, nx);
    }

    /// <summary>Kam se zvíře vydá: králík občas k rostlině, kterou žere, jelen ke stádu, jinak kousek opodál.</summary>
    private void ChooseWanderTarget(Island island, PlantStates plants, Animal a, GameClock clock, PreyRules rules)
    {
        if (a.Kind == AnimalKind.Rabbit && _rng.NextDouble() < rules.GrazeChance && NearestEdiblePlant(island, plants, a, clock, rules.PlantSearch) is { } plant)
        {
            a.TargetX = plant.X;
            a.TargetY = plant.Y;
            a.GoingToPlant = true;
            return;
        }

        a.GoingToPlant = false;
        var (cx, cy) = a.Kind == AnimalKind.Deer ? HerdCenter(a) : (a.X, a.Y);
        a.TargetX = cx + (_rng.NextDouble() * 2 - 1) * rules.WanderRange;
        a.TargetY = cy + (_rng.NextDouble() * 2 - 1) * rules.WanderRange;
    }

    /// <summary>Nejbližší rostlina, kterou králíci okusují (archetyp rabbit), každá třetí jako v prototypu.</summary>
    private static PlantInstance? NearestEdiblePlant(Island island, PlantStates plants, Animal a, GameClock clock, double search)
    {
        PlantInstance? best = null;
        var bestDistance = search;
        for (var i = 0; i < island.Plants.Count; i += 3)
        {
            var plant = island.Plants[i];
            var archetype = GameData.Archetypes[island.SpeciesSecrets[plant.SpeciesId].ArchetypeIndex];
            if (!archetype.Rabbit || plants.IsGone(plant.Id, clock.TotalMinutes))
            {
                continue;
            }

            var d = Distance(plant.X, plant.Y, a.X, a.Y);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = plant;
            }
        }

        return best;
    }

    private (double X, double Y) HerdCenter(Animal a)
    {
        var range = GameData.Animals.Deer.HerdRange;
        double sx = 0, sy = 0;
        var n = 0;
        foreach (var b in _animals)
        {
            if (b.Kind == AnimalKind.Deer && !b.IsDead && Distance(a.X, a.Y, b.X, b.Y) < range)
            {
                sx += b.X;
                sy += b.Y;
                n++;
            }
        }

        return n > 0 ? (sx / n, sy / n) : (a.X, a.Y);
    }

    // ---------- vlk ----------

    private void UpdateWolf(Island island, Fires fires, Animal a, GameClock clock, double dt, bool night, IReadOnlyList<AnimalTarget> players)
    {
        var rules = GameData.Animals.Wolf;
        a.Aggro = Math.Max(0, a.Aggro - dt);
        switch (a.State)
        {
            case AnimalState.Stalk:
                Stalk(island, fires, a, dt, players, rules);
                break;
            case AnimalState.Alert:
                Aim(a, players, dt, rules.ChargeSeconds);
                break;
            case AnimalState.Charge:
                Charge(island, a, clock, dt, players, rules.ChargeSpeed, rules.Radius, rules.ExhaustSeconds, rules.ExhaustAfterHitSeconds, rules.WoundMin, rules.WoundRange);
                break;
            case AnimalState.Exhaust:
                a.Timer -= dt;
                if (a.Timer <= 0)
                {
                    a.State = AnimalState.Wander;
                    a.Timer = 1;
                    a.TargetX = a.DenX;
                    a.TargetY = a.DenY;
                }

                break;
            default:
                WolfWander(island, fires, a, clock, dt, night, players, rules);
                break;
        }
    }

    private void WolfWander(Island island, Fires fires, Animal a, GameClock clock, double dt, bool night, IReadOnlyList<AnimalTarget> players, WolfRules rules)
    {
        a.State = AnimalState.Wander;
        a.Timer -= dt;
        if (a.Timer <= 0)
        {
            a.Timer = 3 + _rng.NextDouble() * 4;
            var range = night ? rules.DenRangeNight : rules.DenRangeDay;
            a.TargetX = a.DenX + (_rng.NextDouble() * 2 - 1) * range;
            a.TargetY = a.DenY + (_rng.NextDouble() * 2 - 1) * range;
        }

        WalkToTarget(island, a, rules.WanderSpeed, dt, rules.Radius);
        if (!night)
        {
            return;
        }

        var (prey, d) = Nearest(players, a.X, a.Y, p => Exposed(island, fires, p, rules));
        if (prey is not null && d < rules.StalkRange)
        {
            a.State = AnimalState.Stalk;
            a.Timer = rules.StalkSeconds + _rng.NextDouble() * rules.StalkSecondsRandom;
            a.Prey = prey.Id;
            prey.Character.Report(clock.TotalMinutes, BodyEvent.Stalked);
        }
    }

    /// <summary>Vlk se plíží k hráči. Vzdá to, když je hráč u ohně, v úkrytu, daleko, nebo když vyprší čas.</summary>
    private void Stalk(Island island, Fires fires, Animal a, double dt, IReadOnlyList<AnimalTarget> players, WolfRules rules)
    {
        var prey = Target(players, a.Prey);
        a.Timer -= dt;
        var d = prey is null ? double.MaxValue : Distance(prey.X, prey.Y, a.X, a.Y);
        if (prey is null || !Exposed(island, fires, prey, rules) || a.Timer <= 0 || d > rules.GiveUpRange)
        {
            a.State = AnimalState.Wander;
            a.Timer = 2;
            a.Prey = -1;
            return;
        }

        // Neplíží se přímo, ale v obloucích (prototyp: úhel se vlní).
        var angle = Math.Atan2(prey.Y - a.Y, prey.X - a.X) + Math.Sin(_time / 4 + a.Id) * 0.9;
        var speed = (d > rules.StalkSlowWithin ? rules.StalkFastSpeed : rules.StalkSlowSpeed) * dt;
        Move(island, a, Math.Cos(angle) * speed, Math.Sin(angle) * speed, rules.Radius);
        a.Dir = Math.Atan2(prey.Y - a.Y, prey.X - a.X);
        if (d < rules.AlertRange)
        {
            a.State = AnimalState.Alert;
            a.Timer = rules.AlertSeconds;
        }
    }

    /// <summary>Hráč je pro vlka na ráně: žije, nemá zapálenou pochodeň, není do fireRange od hořícího ohně a není v úkrytu.</summary>
    private static bool Exposed(Island island, Fires fires, AnimalTarget p, WolfRules rules)
    {
        return !p.Character.IsDead && !p.Character.TorchLit && !fires.BurningNear(island, p.X, p.Y, rules.FireRange) && !Shelter.IsSheltered(island, p.X, p.Y);
    }

    // ---------- divočák ----------

    private void UpdateBoar(Island island, Animal a, GameClock clock, double dt, IReadOnlyList<AnimalTarget> players)
    {
        var rules = GameData.Animals.Boar;
        a.Aggro = Math.Max(0, a.Aggro - dt);
        var (near, d) = Nearest(players, a.X, a.Y, p => !p.Character.IsDead && !p.Character.Sleeping);
        if (near is not null && d < rules.AggroRange && a.State != AnimalState.Charge)
        {
            a.Aggro = rules.AggroSeconds;
        }

        if (a.Aggro > 0)
        {
            WakeSleepers(a, clock, players, rules);
        }

        switch (a.State)
        {
            case AnimalState.Alert:
                Aim(a, players, dt, rules.ChargeSeconds);
                break;
            case AnimalState.Charge:
                Charge(island, a, clock, dt, players, rules.ChargeSpeed, rules.Radius, rules.ExhaustSeconds, rules.ExhaustAfterHitSeconds, rules.WoundMin, rules.WoundRange);
                break;
            case AnimalState.Exhaust:
                a.Timer -= dt;
                if (a.Timer <= 0)
                {
                    a.Timer = 0.8;
                    a.State = AnimalState.Wander;
                    if (a.Aggro > 0 && near is not null && d < rules.AlertRange)
                    {
                        StartBoarAlert(a, near, clock, rules);
                    }
                }

                break;
            default:
                a.State = AnimalState.Wander;
                a.Timer -= dt;
                if (a.Timer <= 0)
                {
                    a.Timer = 2 + _rng.NextDouble() * 4;
                    a.TargetX = a.X + (_rng.NextDouble() * 2 - 1) * rules.WanderRange;
                    a.TargetY = a.Y + (_rng.NextDouble() * 2 - 1) * rules.WanderRange;
                }

                WalkToTarget(island, a, rules.WanderSpeed, dt, rules.Radius);
                if (a.Aggro > 0 && near is not null && d < rules.AlertRange)
                {
                    StartBoarAlert(a, near, clock, rules);
                }

                break;
        }
    }

    /// <summary>Divočák se zaměří. Zkušený lovec tu chvíli vidí delší (víc času uhnout).</summary>
    private static void StartBoarAlert(Animal a, AnimalTarget prey, GameClock clock, BoarRules rules)
    {
        a.State = AnimalState.Alert;
        a.Prey = prey.Id;
        a.Timer = rules.AlertSeconds + rules.AlertBonusPerHuntLevel * prey.Character.Skills.Level(SkillKind.Hunt);
        prey.Character.Report(clock.TotalMinutes, BodyEvent.BoarAlert);
    }

    /// <summary>Rozzuřený divočák vzbudí spící hráče poblíž.</summary>
    private static void WakeSleepers(Animal a, GameClock clock, IReadOnlyList<AnimalTarget> players, BoarRules rules)
    {
        foreach (var p in players)
        {
            if (p.Character.Sleeping && Distance(p.X, p.Y, a.X, a.Y) < rules.WakeSleeperRange)
            {
                p.Character.WakeUp(clock.TotalMinutes, BodyEvent.WokeByAnimal);
            }
        }
    }

    // ---------- společné: zaměření, výpad ----------

    /// <summary>Zaměření: zvíře se dívá na hráče. Po čase vyrazí jeho směrem (směr výpadu se už nemění).</summary>
    private static void Aim(Animal a, IReadOnlyList<AnimalTarget> players, double dt, double chargeSeconds)
    {
        var prey = Target(players, a.Prey);
        if (prey is null)
        {
            a.State = AnimalState.Wander;
            return;
        }

        a.Dir = Math.Atan2(prey.Y - a.Y, prey.X - a.X);
        a.Timer -= dt;
        if (a.Timer > 0)
        {
            return;
        }

        var d = Math.Max(Distance(prey.X, prey.Y, a.X, a.Y), 0.001);
        a.State = AnimalState.Charge;
        a.ChargeLeft = chargeSeconds;
        a.ChargeX = (prey.X - a.X) / d;
        a.ChargeY = (prey.Y - a.Y) / d;
    }

    /// <summary>Výpad: zvíře běží rovně. Koho zasáhne (a není zrovna omráčený), toho zraní. Pak je vyčerpané.</summary>
    private void Charge(
        Island island, Animal a, GameClock clock, double dt, IReadOnlyList<AnimalTarget> players,
        double speed, double radius, double exhaustSeconds, double exhaustAfterHitSeconds, double woundMin, double woundRange)
    {
        a.ChargeLeft -= dt;
        var moved = Move(island, a, a.ChargeX * speed * dt, a.ChargeY * speed * dt, radius);
        foreach (var p in players)
        {
            if (!p.Character.IsDead && p.Character.StunSeconds <= 0 && Distance(p.X, p.Y, a.X, a.Y) < HitDistance)
            {
                p.Character.Wound(woundMin + _rng.NextDouble() * woundRange, a.Kind, clock.TotalMinutes);
                a.State = AnimalState.Exhaust;
                a.Timer = exhaustAfterHitSeconds;
                return;
            }
        }

        if (!moved || a.ChargeLeft <= 0)
        {
            a.State = AnimalState.Exhaust;
            a.Timer = exhaustSeconds;
        }
    }

    // ---------- narození, smrt, doplňování ----------

    private void Kill(Animal animal, double now)
    {
        animal.State = AnimalState.Dead;
        animal.DeadAt = now;
    }

    private void RemoveOldCorpses(double now)
    {
        _animals.RemoveAll(a => a.IsDead && now > a.DeadAt + GameData.Animals.CorpseMinutes);
    }

    /// <summary>Když zvířat ubude (lov), jednou za respawnMinutes se doplní jedno chybějící od každého druhu.</summary>
    private void Respawn(Island island, double now, IReadOnlyList<AnimalTarget> players)
    {
        if (double.IsNaN(_nextRespawnAt))
        {
            _nextRespawnAt = now + GameData.Animals.RespawnMinutes;
        }

        if (now < _nextRespawnAt)
        {
            return;
        }

        _nextRespawnAt = now + GameData.Animals.RespawnMinutes;
        foreach (var kind in new[] { AnimalKind.Boar, AnimalKind.Rabbit, AnimalKind.Deer, AnimalKind.Wolf })
        {
            if (_animals.Count(a => a.Kind == kind && !a.IsDead) < TargetCount(island, kind))
            {
                Spawn(island, kind, players);
            }
        }
    }

    /// <summary>
    /// Nové zvíře (prototyp spawnAnimal): divočák a vlk na trávě nebo v lese, kořist i na písku, daleko od hráčů
    /// a táborů. Jelen se rodí u stáda (když nějaké je).
    /// </summary>
    /// <summary>Zvíře na dané místo bez kontrol (pro testy a později pro načtení uloženého světa).</summary>
    public Animal Place(AnimalKind kind, double x, double y)
    {
        var (hp, stamina) = Vitals(kind);
        var animal = new Animal(_nextId++, kind, x, y, hp, stamina);
        _animals.Add(animal);
        return animal;
    }

    private static (int Hp, double Stamina) Vitals(AnimalKind kind)
    {
        var rules = GameData.Animals;
        return kind switch
        {
            AnimalKind.Boar => (rules.Boar.Hp, 0),
            AnimalKind.Wolf => (rules.Wolf.Hp, 0),
            AnimalKind.Deer => (rules.Deer.Hp, rules.Deer.Stamina),
            _ => (rules.Rabbit.Hp, rules.Rabbit.Stamina),
        };
    }

    private Animal? Spawn(Island island, AnimalKind kind, IReadOnlyList<AnimalTarget> players)
    {
        var rules = GameData.Animals;
        var (hp, radius, avoid, stamina) = kind switch
        {
            AnimalKind.Boar => (rules.Boar.Hp, rules.Boar.Radius, rules.Boar.SpawnAvoid, 0.0),
            AnimalKind.Wolf => (rules.Wolf.Hp, rules.Wolf.Radius, rules.Wolf.SpawnAvoid, 0.0),
            AnimalKind.Deer => (rules.Deer.Hp, rules.Deer.Radius, rules.Deer.SpawnAvoid, rules.Deer.Stamina),
            _ => (rules.Rabbit.Hp, rules.Rabbit.Radius, rules.Rabbit.SpawnAvoid, rules.Rabbit.Stamina),
        };
        var herd = kind == AnimalKind.Deer ? _animals.FirstOrDefault(a => a.Kind == AnimalKind.Deer && !a.IsDead) : null;
        for (var n = 0; n < 200; n++)
        {
            double x, y;
            if (herd is not null)
            {
                x = herd.X + (_rng.NextDouble() * 2 - 1) * rules.Deer.HerdSpawnSpread;
                y = herd.Y + (_rng.NextDouble() * 2 - 1) * rules.Deer.HerdSpawnSpread;
            }
            else
            {
                x = _rng.NextInt(island.Width) + 0.5;
                y = _rng.NextInt(island.Height) + 0.5;
            }

            if (!GoodGround(island, kind, x, y)
                || !Movement.IsWalkable(island, x, y, radius)
                || players.Any(p => Distance(p.X, p.Y, x, y) < avoid)
                || island.Camps.Any(c => Distance(c.X, c.Y, x, y) <= rules.CampDistance))
            {
                continue;
            }

            var animal = new Animal(_nextId++, kind, x, y, hp, stamina) { Timer = _rng.NextDouble() * 3, Dir = _rng.NextDouble() * Math.PI * 2 };
            _animals.Add(animal);
            return animal;
        }

        return null;
    }

    private static bool GoodGround(Island island, AnimalKind kind, double x, double y)
    {
        var tx = (int)Math.Floor(x);
        var ty = (int)Math.Floor(y);
        if (!island.InBounds(tx, ty))
        {
            return false;
        }

        var tile = island.TileAt(tx, ty);
        var prey = kind is AnimalKind.Rabbit or AnimalKind.Deer;
        return tile is TileType.Grass or TileType.Forest or TileType.Highland || (prey && tile == TileType.Sand);
    }

    // ---------- pomůcky ----------

    private static void WalkToTarget(Island island, Animal a, double speed, double dt, double radius)
    {
        var dx = a.TargetX - a.X;
        var dy = a.TargetY - a.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d > 0.3)
        {
            a.Dir = Math.Atan2(dy, dx);
            Move(island, a, dx / d * speed * dt, dy / d * speed * dt, radius);
        }
    }

    /// <summary>Posun zvířete (prototyp moveAnimal): po osách, a když něco stojí v cestě, vrátí false.</summary>
    private static bool Move(Island island, Animal a, double dx, double dy, double radius)
    {
        if (!Movement.CanStand(island, a.X + dx, a.Y, radius))
        {
            return false;
        }

        a.X += dx;
        if (!Movement.CanStand(island, a.X, a.Y + dy, radius))
        {
            return false;
        }

        a.Y += dy;
        return true;
    }

    private static (AnimalTarget? Player, double Distance) Nearest(IReadOnlyList<AnimalTarget> players, double x, double y, Func<AnimalTarget, bool> filter)
    {
        AnimalTarget? best = null;
        var bestDistance = double.MaxValue;
        foreach (var p in players)
        {
            if (p.Character.IsDead || !filter(p))
            {
                continue;
            }

            var d = Distance(p.X, p.Y, x, y);
            if (d < bestDistance)
            {
                best = p;
                bestDistance = d;
            }
        }

        return (best, bestDistance);
    }

    private static AnimalTarget? Target(IReadOnlyList<AnimalTarget> players, int id)
    {
        foreach (var p in players)
        {
            if (p.Id == id && !p.Character.IsDead)
            {
                return p;
            }
        }

        return null;
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

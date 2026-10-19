using Oohra.Core.Data;
using Oohra.Core.Language;
using Oohra.Core.Persistence;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Characters;

/// <summary>Druh naplánovaného účinku po jídle.</summary>
public enum EffectType
{
    Vomit,
    Diarrhea,
    Toxin,
    Neurotoxin,
    Crash,

    /// <summary>Nemoc z kalné vody nebo zkaženého jídla: zánět (Infection) a z něj horečka.</summary>
    Sickness,

    /// <summary>Zánět rány po zranění od zvířete.</summary>
    WoundInfection,

    /// <summary>Začátek bahenní zimnice (po kousnutí komára).</summary>
    MarshFever,

    /// <summary>Začátek úplavice (po bahenní vodě).</summary>
    Dysentery,
}

/// <summary>Účinek, který nastane v čase At (herní minuty). Strength je síla účinku.</summary>
public sealed record ScheduledEffect(double At, EffectType Type, double Strength);

/// <summary>
/// Postava a její tělo (stav S z prototypu, jen část o těle).
/// Ukazatele jsou 0 až 100. Toxin, horečku a ostatní skryté hodnoty hráč nevidí, vidí jen příznaky.
/// Čas měří v herních minutách. Náhodu bere z vlastního generátoru, aby šlo výsledky opakovat.
/// </summary>
public sealed class Character
{
    private readonly List<ScheduledEffect> _scheduled = [];
    private readonly List<BodyMessage> _messages = [];
    private readonly Dictionary<DeathCause, double> _blame = [];

    public Character(int generation, Mulberry32 rng)
    {
        Traits = GenerationTraits.For(generation);
        Rng = rng;
        var start = GameData.Body.Start;
        if (Traits.Generation == 0)
        {
            // Pradávní znají základní recepty od narození (kopí, sekera, nůž).
            foreach (var recipe in GameData.Crafting.Recipes.Where(r => r.KnownByAncients))
            {
                _recipes.Add(recipe.Id);
            }
        }

        Satiety = start.Satiety;
        Hydration = start.Hydration;
        Energy = start.Energy;
        Warmth = start.Warmth;
        Health = start.Health;
    }

    public GenerationTraits Traits { get; }

    public Mulberry32 Rng { get; }

    // Viditelné ukazatele (pruhy).
    public double Satiety { get; set; }

    public double Hydration { get; set; }

    public double Energy { get; set; }

    public double Warmth { get; set; }

    public double Health { get; set; }

    /// <summary>Do kdy je nováček v teple od narození (herní minuty): zima mu zatím neubližuje (návrh 4.1).</summary>
    public double WarmUntil { get; set; } = double.NegativeInfinity;

    /// <summary>Do kdy má nováček instinkt na zimu (herní minuty): zebe ho dřív, než zima ubližuje.</summary>
    public double ColdInstinctUntil { get; set; } = double.NegativeInfinity;

    public double LastColdInstinct { get; set; } = double.NegativeInfinity;

    /// <summary>Kolik herních minut ještě hoří pochodeň v ruce (0 = nemá zapálenou).</summary>
    public double TorchMinutes { get; set; }

    public bool TorchLit => TorchMinutes > 0;

    /// <summary>Hráč už dostal upozornění, že pochodeň dohořívá.</summary>
    public bool TorchWarned { get; set; }

    private readonly HashSet<string> _recipes = [];
    private readonly List<Disease> _diseases = [];

    /// <summary>SKRYTÉ: nemoci, které postava má (hráč vidí jen projevy). Smrtí zmizí s postavou.</summary>
    public IReadOnlyList<Disease> Diseases => _diseases;

    public bool HasDisease(DiseaseKind kind)
    {
        return _diseases.Any(d => d.Kind == kind);
    }

    public void SetDisease(Disease disease)
    {
        RemoveDisease(disease.Kind);
        _diseases.Add(disease);
    }

    public void RemoveDisease(DiseaseKind kind)
    {
        _diseases.RemoveAll(d => d.Kind == kind);
    }

    /// <summary>Kolik pijavic na postavě visí.</summary>
    public int Leeches { get; set; }

    /// <summary>Do kdy je natřená bahnem nebo popelem (komáři neštípou), herní minuty.</summary>
    public double SmearedUntil { get; set; } = double.NegativeInfinity;

    public double LastMosquitoWarning { get; set; } = double.NegativeInfinity;

    /// <summary>Herní čas posledního výpočtu těla (pro příznaky, které závisí na čase). Neukládá se.</summary>
    public double LastNow { get; set; }

    /// <summary>Recepty, které postava umí (zkoušením nebo od narození). Smrtí se ztratí.</summary>
    public IReadOnlyCollection<string> Recipes => _recipes;

    /// <summary>Roste o 1, když se postava naučí nový recept (pro poslání hráči).</summary>
    public int RecipesVersion { get; private set; }

    public bool Knows(string recipeId)
    {
        return _recipes.Contains(recipeId);
    }

    public void Learn(string recipeId)
    {
        if (_recipes.Add(recipeId))
        {
            RecipesVersion++;
        }
    }

    /// <summary>Nováček: první hodiny v teple a pár dní instinkt na zimu (první život, návrh 4.1).</summary>
    public void BornAsNewcomer(double now)
    {
        var rules = GameData.Body.Warmth.Newborn;
        WarmUntil = now + rules.WarmHours * 60;
        ColdInstinctUntil = now + rules.InstinctDays * 1440;
    }

    /// <summary>Kdy naposledy jedl nebo ochutnával (herní minuty), pro souvislost „u jídla“ v jazyce.</summary>
    public double LastAte { get; set; } = double.NegativeInfinity;

    // Skryté hodnoty těla.
    public double Toxin { get; set; }

    public double Fever { get; set; }

    public double Infection { get; set; }

    /// <summary>Zbývající minuty třesu, bezvědomí, povzbuzení a průjmu.</summary>
    public double TremorMinutes { get; set; }

    public double FaintMinutes { get; set; }

    public double StimulantMinutes { get; set; }

    public double DiarrheaMinutes { get; set; }

    /// <summary>Ochrana první otravy je zapnutá (viz firstPoisonProtection v body.json).</summary>
    public bool PoisonProtected { get; set; }

    /// <summary>Probíhá otrava (toxin překročil práh). Ochrana se rozhoduje na jejím začátku.</summary>
    public bool InToxinEpisode { get; set; }

    public bool FeverNoticed { get; set; }

    public double BlameMinutes { get; set; }

    public bool IsDead { get; private set; }

    /// <summary>Krvácení: kolik zdraví za herní minutu ubírá. Pomalu ustává.</summary>
    public double Bleed { get; set; }

    /// <summary>Kulhání 0 až 100: zpomalí chůzi. Pomalu se hojí.</summary>
    public double Limp { get; set; }

    /// <summary>Omráčení po zásahu (sekundy reálného času): postava se nehýbe a zvíře ji hned znovu nezasáhne.</summary>
    public double StunSeconds { get; set; }

    /// <summary>
    /// Zranění od zvířete (prototyp wound). Síla se zmenší dovedností Lov a podle generace (Pradávní snesou víc).
    /// Ubere zdraví, rozjede krvácení a kulhání, postavu omráčí a vzbudí. Velká rána se může zanítit.
    /// </summary>
    public void Wound(double severity, AnimalKind from, double now)
    {
        var rules = GameData.Animals.Wounds;
        severity *= 1 - rules.HuntReductionPerLevel * Skills.Level(SkillKind.Hunt);
        ApplyWound(severity, now, Characters.DeathCause.Wound);
        Report(now, from == AnimalKind.Wolf ? BodyEvent.WoundedByWolf : BodyEvent.WoundedByBoar);
    }

    /// <summary>Zasáhl ho padající strom: zranění jako od zvířete (krvácení, kulhání, zánět), jiná příčina smrti.</summary>
    public void HitByTree(double severity, double now)
    {
        ApplyWound(severity, now, Characters.DeathCause.FallingTree);
        Report(now, BodyEvent.HitByTree);
    }

    private void ApplyWound(double severity, double now, DeathCause cause)
    {
        var rules = GameData.Animals.Wounds;
        severity *= Traits.WoundMultiplier;
        TakeDamage(severity * rules.HealthFactor, cause);
        Bleed = Math.Min(rules.MaxBleed, Bleed + severity * rules.BleedPerSeverity);
        Limp = Math.Min(rules.MaxLimp, Limp + severity * rules.LimpPerSeverity);
        StunSeconds = rules.StunSeconds;
        WakeUp(now, null);
        if (severity > rules.InfectionAbove && Rng.NextDouble() < rules.InfectionChance)
        {
            Schedule(now + rules.InfectionDelayMin + Rng.NextDouble() * rules.InfectionDelayRange, EffectType.WoundInfection, severity * rules.InfectionFactor);
        }
    }

    /// <summary>Dovednosti postavy. Smrtí se ztratí (nová postava začíná od nuly).</summary>
    public Skills Skills { get; } = new();

    /// <summary>Zápisník slov. Smrtí se ztratí (nová postava začíná s prázdným, Pradávný se Starým hlasem).</summary>
    public Vocabulary Vocabulary { get; } = new();

    /// <summary>Postava spí (nehýbe se, síla se vrací, dovednosti se po probuzení utvrdí).</summary>
    public bool Sleeping { get; private set; }

    /// <summary>Kolik herních minut už spí.</summary>
    public double SleepMinutes { get; set; }

    /// <summary>Procvičí dovednost (Noví se učí rychleji).</summary>
    public void Practice(SkillKind skill, double amount)
    {
        Skills.Practice(skill, amount, Traits.LearnMultiplier);
    }

    /// <summary>Lehne si ke spánku (prototyp toggleSleep). Jen unavený a v teple. Vrací, zda usnul.</summary>
    public bool TryFallAsleep(double now)
    {
        var rules = GameData.Body.Sleep;
        if (IsDead || Sleeping || FaintMinutes > 0)
        {
            return false;
        }

        if (Energy > rules.MaxEnergyToSleep)
        {
            Report(now, BodyEvent.NotTired);
            return false;
        }

        if (Warmth < rules.MinWarmthToSleep)
        {
            Report(now, BodyEvent.TooColdToSleep);
            return false;
        }

        Sleeping = true;
        SleepMinutes = 0;
        Report(now, BodyEvent.FellAsleep);
        return true;
    }

    /// <summary>
    /// Probudí se (prototyp endSleep). Po dost dlouhém spánku se utvrdí nácvik dovedností.
    /// reason: proč (odpočatý, sám, zimou). Null = bez hlášky (třeba při omdlení).
    /// </summary>
    public void WakeUp(double now, BodyEvent? reason)
    {
        if (!Sleeping)
        {
            return;
        }

        Sleeping = false;
        if (reason is { } bodyEvent)
        {
            Report(now, bodyEvent);
        }

        if (SleepMinutes >= GameData.Skills.MinSleepMinutes)
        {
            var levelUps = Skills.Consolidate(out var learned);
            foreach (var (skill, level) in levelUps)
            {
                _messages.Add(new BodyMessage(now, BodyEvent.SkillLevelUp, null, skill, level));
            }

            if (learned && levelUps.Count == 0)
            {
                Report(now, BodyEvent.LearnedInSleep);
            }
        }

        SleepMinutes = 0;
    }

    public DeathCause? DeathCause { get; private set; }

    public IReadOnlyList<ScheduledEffect> Scheduled => _scheduled;

    public IReadOnlyDictionary<DeathCause, double> Blame => _blame;

    public void Schedule(double at, EffectType type, double strength)
    {
        _scheduled.Add(new ScheduledEffect(at, type, strength));
    }

    /// <summary>Vybere účinky, jejichž čas nastal, a odebere je z plánu.</summary>
    public List<ScheduledEffect> TakeDueEffects(double now)
    {
        var due = _scheduled.Where(e => now >= e.At).ToList();
        _scheduled.RemoveAll(e => now >= e.At);
        return due;
    }

    public void Report(double at, BodyEvent bodyEvent, IReadOnlyList<TasteNote>? notes = null)
    {
        _messages.Add(new BodyMessage(at, bodyEvent, notes));
    }

    /// <summary>Vrátí nové zprávy o těle a smaže je (klient si je vyzvedne).</summary>
    public List<BodyMessage> TakeMessages()
    {
        var messages = _messages.ToList();
        _messages.Clear();
        return messages;
    }

    /// <summary>
    /// Ubere zdraví a zapamatuje si příčinu. Při ochraně první otravy jed nesrazí zdraví pod práh.
    /// </summary>
    public void TakeDamage(double amount, DeathCause cause)
    {
        if (amount <= 0)
        {
            return;
        }

        var protection = GameData.Body.FirstPoisonProtection;
        if (IsPoisonCause(cause) && PoisonProtected && Health - amount < protection.HealthFloor)
        {
            amount = Math.Max(0, Health - protection.HealthFloor);
        }

        Health -= amount;
        _blame[cause] = _blame.GetValueOrDefault(cause) + amount;
    }

    /// <summary>Každý den se polovina „viny“ zapomene, aby příčina smrti odpovídala nedávným událostem.</summary>
    public void HalveBlame()
    {
        foreach (var cause in _blame.Keys.ToList())
        {
            _blame[cause] *= 0.5;
        }
    }

    public void Die(double now)
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        Sleeping = false;
        TorchMinutes = 0;
        DeathCause = _blame.Count == 0
            ? Characters.DeathCause.Hunger
            : _blame.OrderByDescending(b => b.Value).First().Key;
        Report(now, BodyEvent.Died);
    }

    /// <summary>Příznaky, které hráč vidí ve stavu těla.</summary>
    public List<Symptom> Symptoms()
    {
        var rules = GameData.Body.Symptoms;
        var list = new List<Symptom>();
        if (Toxin > rules.HeavyBodyToxinAbove)
        {
            list.Add(Symptom.HeavyBody);
        }

        if (Fever > rules.FeverAbove)
        {
            list.Add(Symptom.Fever);
        }

        if (TremorMinutes > 0)
        {
            list.Add(Symptom.Tremor);
        }

        if (StimulantMinutes > 0)
        {
            list.Add(Symptom.Agitated);
        }

        if (DiarrheaMinutes > 0)
        {
            list.Add(Symptom.Diarrhea);
        }

        if (Infection > rules.InflammationAbove)
        {
            list.Add(Symptom.Inflammation);
        }

        if (FaintMinutes > 0)
        {
            list.Add(Symptom.Unconscious);
        }

        if (Bleed > 0)
        {
            list.Add(Symptom.Bleeding);
        }

        if (Limp > GameData.Animals.Wounds.LimpSymptomAbove)
        {
            list.Add(Symptom.Limping);
        }

        if (HasDisease(DiseaseKind.MarshFever))
        {
            list.Add(Symptom.Chills);
        }

        if (HasDisease(DiseaseKind.Dysentery))
        {
            list.Add(Symptom.BellyAche);
        }

        if (Leeches > 0)
        {
            list.Add(Symptom.Leeches);
        }

        if (SmearedUntil > LastNow)
        {
            list.Add(Symptom.Smeared);
        }

        return list;
    }

    /// <summary>Uloží celou postavu (uložení světa). Zprávy pro klienta se neukládají.</summary>
    public CharacterSave ToSave()
    {
        return new CharacterSave
        {
            Generation = Traits.Generation,
            RngState = Rng.State,
            Satiety = Satiety,
            Hydration = Hydration,
            Energy = Energy,
            Warmth = Warmth,
            Health = Health,
            LastAte = LastAte,
            WarmUntil = WarmUntil,
            ColdInstinctUntil = ColdInstinctUntil,
            LastColdInstinct = LastColdInstinct,
            TorchMinutes = TorchMinutes,
            TorchWarned = TorchWarned,
            Recipes = [.. _recipes],
            Diseases = [.. _diseases],
            Leeches = Leeches,
            SmearedUntil = SmearedUntil,
            LastMosquitoWarning = LastMosquitoWarning,
            Toxin = Toxin,
            Fever = Fever,
            Infection = Infection,
            TremorMinutes = TremorMinutes,
            FaintMinutes = FaintMinutes,
            StimulantMinutes = StimulantMinutes,
            DiarrheaMinutes = DiarrheaMinutes,
            PoisonProtected = PoisonProtected,
            InToxinEpisode = InToxinEpisode,
            FeverNoticed = FeverNoticed,
            BlameMinutes = BlameMinutes,
            IsDead = IsDead,
            DeathCause = DeathCause,
            Bleed = Bleed,
            Limp = Limp,
            StunSeconds = StunSeconds,
            Sleeping = Sleeping,
            SleepMinutes = SleepMinutes,
            Scheduled = [.. _scheduled],
            Blame = new Dictionary<DeathCause, double>(_blame),
            SkillPoints = Skills.ExportPoints(),
            SkillPending = Skills.ExportPending(),
            Words = Vocabulary.Export(),
            LastHeard = Vocabulary.LastHeard,
        };
    }

    /// <summary>Postava z uloženého stavu (po restartu serveru pokračuje, jako by se nic nestalo).</summary>
    public static Character FromSave(CharacterSave save)
    {
        var c = new Character(save.Generation, new Mulberry32(save.RngState))
        {
            Satiety = save.Satiety,
            Hydration = save.Hydration,
            Energy = save.Energy,
            Warmth = save.Warmth,
            Health = save.Health,
            LastAte = save.LastAte,
            WarmUntil = save.WarmUntil,
            ColdInstinctUntil = save.ColdInstinctUntil,
            LastColdInstinct = save.LastColdInstinct,
            TorchMinutes = save.TorchMinutes,
            TorchWarned = save.TorchWarned,
            Toxin = save.Toxin,
            Fever = save.Fever,
            Infection = save.Infection,
            TremorMinutes = save.TremorMinutes,
            FaintMinutes = save.FaintMinutes,
            StimulantMinutes = save.StimulantMinutes,
            DiarrheaMinutes = save.DiarrheaMinutes,
            PoisonProtected = save.PoisonProtected,
            InToxinEpisode = save.InToxinEpisode,
            FeverNoticed = save.FeverNoticed,
            BlameMinutes = save.BlameMinutes,
            IsDead = save.IsDead,
            DeathCause = save.DeathCause,
            Bleed = save.Bleed,
            Limp = save.Limp,
            StunSeconds = save.StunSeconds,
            Sleeping = save.Sleeping,
            SleepMinutes = save.SleepMinutes,
        };
        c._scheduled.AddRange(save.Scheduled);
        foreach (var (cause, amount) in save.Blame)
        {
            c._blame[cause] = amount;
        }

        c._diseases.AddRange(save.Diseases);
        c.Leeches = save.Leeches;
        c.SmearedUntil = save.SmearedUntil;
        c.LastMosquitoWarning = save.LastMosquitoWarning;
        c._recipes.Clear();
        foreach (var recipe in save.Recipes)
        {
            c._recipes.Add(recipe);
        }

        c.Skills.Import(save.SkillPoints, save.SkillPending);
        c.Vocabulary.Import(save.Words, save.LastHeard);
        return c;
    }

    private static bool IsPoisonCause(DeathCause cause)
    {
        return cause is Characters.DeathCause.Toxin or Characters.DeathCause.Neurotoxin or Characters.DeathCause.Fever;
    }
}

using Oohra.Core.Data;
using Oohra.Core.Rng;

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

        return list;
    }

    private static bool IsPoisonCause(DeathCause cause)
    {
        return cause is Characters.DeathCause.Toxin or Characters.DeathCause.Neurotoxin or Characters.DeathCause.Fever;
    }
}

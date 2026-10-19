using Oohra.Core.Data;

namespace Oohra.Core.Characters;

/// <summary>Nemoc (SKRYTÁ: jméno ani druh hráč nevidí, jen projevy).</summary>
public enum DiseaseKind
{
    /// <summary>Bahenní zimnice od komárů: horečka ve vlnách, únava.</summary>
    MarshFever = 0,

    /// <summary>Úplavice z bahenní vody: průjem a žízeň.</summary>
    Dysentery = 1,

    /// <summary>Klíšťová horečka: stálá horečka a bolest kloubů.</summary>
    TickFever = 2,

    /// <summary>Hadí jed: jed v těle a kulhání, krátce.</summary>
    SnakeVenom = 3,
}

/// <summary>Nemoc, kterou postava má: síla 0 až 100 a od kdy (herní minuty).</summary>
public sealed record Disease(DiseaseKind Kind, double Severity, double Since);

/// <summary>
/// Nemoci se zpožděním (návrh 0.8, kap. 5, hazards.json): nákaza naplánuje začátek (EffectType.MarshFever,
/// EffectType.Dysentery), pak nemoc působí, sama pomalu odeznívá a léčí ji jen správná rostlina (Cure).
/// </summary>
public static class Diseases
{
    /// <summary>Nákaza: začátek nemoci za inkubační dobu (když ji postava nemá a ani se jí neblíží).</summary>
    public static void Infect(Character c, DiseaseKind kind, double now)
    {
        var type = EffectOf(kind);
        if (c.HasDisease(kind) || c.Scheduled.Any(e => e.Type == type))
        {
            return;
        }

        var rules = GameData.Hazards;
        var (min, range, strength, strengthRange) = kind switch
        {
            DiseaseKind.MarshFever => (rules.MarshFever.IncubationMin, rules.MarshFever.IncubationRange, rules.MarshFever.StrengthMin, rules.MarshFever.StrengthRange),
            DiseaseKind.TickFever => (rules.TickFever.IncubationMin, rules.TickFever.IncubationRange, rules.TickFever.StrengthMin, rules.TickFever.StrengthRange),
            DiseaseKind.SnakeVenom => (rules.SnakeVenom.IncubationMin, rules.SnakeVenom.IncubationRange, rules.SnakeVenom.StrengthMin, rules.SnakeVenom.StrengthRange),
            _ => (rules.Dysentery.IncubationMin, rules.Dysentery.IncubationRange, rules.Dysentery.StrengthMin, rules.Dysentery.StrengthRange),
        };
        c.Schedule(now + min + c.Rng.NextDouble() * range, type, strength + c.Rng.NextDouble() * strengthRange);
    }

    /// <summary>Naplánovaný účinek, kterým nemoc začne.</summary>
    public static EffectType EffectOf(DiseaseKind kind)
    {
        return kind switch
        {
            DiseaseKind.MarshFever => EffectType.MarshFever,
            DiseaseKind.TickFever => EffectType.TickFever,
            DiseaseKind.SnakeVenom => EffectType.SnakeVenom,
            _ => EffectType.Dysentery,
        };
    }

    /// <summary>Nemoc začala (naplánovaný účinek): projev, ale bez jména.</summary>
    public static void Start(Character c, DiseaseKind kind, double strength, double now)
    {
        c.SetDisease(new Disease(kind, Math.Min(100, strength), now));
        c.Report(now, kind switch
        {
            DiseaseKind.MarshFever => BodyEvent.Chills,
            DiseaseKind.TickFever => BodyEvent.AchingJoints,
            DiseaseKind.SnakeVenom => BodyEvent.VenomSpreads,
            _ => BodyEvent.BellyAche,
        });
    }

    /// <summary>Nemoci působí a samy pomalu odeznívají.</summary>
    public static void Update(Character c, double now, double minutes)
    {
        var rules = GameData.Hazards;
        foreach (var disease in c.Diseases.ToList())
        {
            double healPerDay;
            if (disease.Kind == DiseaseKind.MarshFever)
            {
                var r = rules.MarshFever;
                var phase = (now - disease.Since) % r.CycleMinutes / r.CycleMinutes;
                if (phase < r.FeverShare)
                {
                    c.Fever = Math.Max(c.Fever, disease.Severity * r.FeverFactor);
                }

                c.Energy = Math.Max(0, c.Energy - r.EnergyDrainPerMinute * minutes);
                healPerDay = r.HealPerDay;
            }
            else if (disease.Kind == DiseaseKind.TickFever)
            {
                var r = rules.TickFever;
                c.Fever = Math.Max(c.Fever, disease.Severity * r.FeverFactor);
                c.Limp = Math.Max(c.Limp, r.Limp);
                healPerDay = r.HealPerDay;
            }
            else if (disease.Kind == DiseaseKind.SnakeVenom)
            {
                var r = rules.SnakeVenom;
                c.Toxin += r.ToxinPerMinute * disease.Severity / 50 * minutes;
                c.Limp = Math.Max(c.Limp, r.Limp);
                healPerDay = r.HealPerDay;
            }
            else
            {
                var r = rules.Dysentery;
                if (disease.Severity > r.DiarrheaAbove)
                {
                    c.DiarrheaMinutes = Math.Max(c.DiarrheaMinutes, minutes + 1);
                }

                c.Hydration = Math.Max(0, c.Hydration - r.HydrationPerMinute * minutes);
                healPerDay = r.HealPerDay;
            }

            Weaken(c, disease.Kind, healPerDay * minutes / 1440, now, relief: false);
        }
    }

    /// <summary>
    /// Lék: snědená část rostliny s lékem na nemoc (skryté složení) nemoc zeslabí. Upečená léčí slaběji,
    /// ochutnání podle menší dávky. Na jinou nemoc nepomůže.
    /// </summary>
    public static void Cure(Character c, IReadOnlyDictionary<DiseaseKind, double> cures, double dose, bool baked, double now)
    {
        var rules = GameData.Hazards;
        foreach (var (kind, value) in cures)
        {
            var amount = value * dose * rules.CureFactor * (baked ? rules.BakedCureMultiplier : 1);
            Weaken(c, kind, amount, now, relief: true);
        }
    }

    private static void Weaken(Character c, DiseaseKind kind, double amount, double now, bool relief)
    {
        var disease = c.Diseases.FirstOrDefault(d => d.Kind == kind);
        if (disease is null || amount <= 0)
        {
            return;
        }

        var severity = disease.Severity - amount;
        if (severity <= 0)
        {
            c.RemoveDisease(kind);
            c.Report(now, BodyEvent.Recovered);
            return;
        }

        c.SetDisease(disease with { Severity = severity });
        if (relief)
        {
            c.Report(now, BodyEvent.Relief);
        }
    }
}

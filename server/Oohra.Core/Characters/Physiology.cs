using Oohra.Core.Data;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Core.Characters;

/// <summary>
/// Co se děje s tělem v čase (prototyp physiology a runEvents v part4.js):
/// teplo, hlad, žízeň, únava, horečka, uzdravování, poškození a naplánované účinky jídla.
/// Oheň hřeje (nearFire), úkryt taky (sheltered), déšť bez úkrytu studí (weather). Spánek vrací sílu a zpomaluje hlad a žízeň. Zranění zatím nejsou.
/// </summary>
public static class Physiology
{
    /// <summary>
    /// Posune tělo o daný počet herních minut. Čas na hodinách už musí být posunutý.
    /// nearFire: postava je u hořícího ohně (Fires.WarmsAt), sheltered: je v úkrytu (Shelter.IsSheltered).
    /// Teplo těla se pak blíží vyšší hodnotě.
    /// </summary>
    public static void Update(Character c, GameClock clock, double minutes, bool nearFire = false, bool sheltered = false, WeatherState weather = default)
    {
        if (c.IsDead)
        {
            return;
        }

        var rules = GameData.Body;
        c.LastNow = clock.TotalMinutes;
        RunDueEffects(c, clock.TotalMinutes);
        Diseases.Update(c, clock.TotalMinutes, minutes);
        UpdateWarmth(c, clock, minutes, nearFire, sheltered, weather, rules.Warmth);
        UpdateNeeds(c, minutes, rules.Needs);
        UpdateRecoveryAndFever(c, clock.TotalMinutes, minutes, rules);
        UpdatePoisonProtection(c, rules.FirstPoisonProtection);
        CountDownConditions(c, clock.TotalMinutes, minutes);
        UpdateSleep(c, clock, rules.Sleep);
        ApplyDamage(c, minutes, rules.Damage);
        Heal(c, minutes, rules.Recovery);
        ForgetOldBlame(c, minutes, rules.Damage);

        if (c.Health <= 0)
        {
            c.Die(clock.TotalMinutes);
        }
    }

    /// <summary>
    /// Nechá uběhnout daný počet herních minut po krocích (výchozí krok 1 minuta).
    /// Pro zkoušení v konzoli a v testech. Server bude místo toho volat Update každý tick.
    /// </summary>
    public static void Run(Character c, GameClock clock, double minutes, double stepMinutes = 1, bool nearFire = false, bool sheltered = false, WeatherState weather = default)
    {
        var remaining = minutes;
        while (remaining > 0 && !c.IsDead)
        {
            var step = Math.Min(stepMinutes, remaining);
            clock.Advance(clock.RealSecondsFor(step));
            Update(c, clock, step, nearFire, sheltered, weather);
            remaining -= step;
        }
    }

    /// <summary>Teplota okolí podle ročního období, ve dne o něco vyšší.</summary>
    public static double AmbientTemperature(GameClock clock, WarmthRules rules)
    {
        return rules.SeasonAmbient[(int)clock.Season] + rules.DaylightSwing * (clock.Daylight - 0.5);
    }

    /// <summary>
    /// Nováček (návrh 4.1): dokud je v teple od narození, teplo těla neklesne pod hranici, kdy zima škodí.
    /// Dokud má instinkt na zimu, zebe ho dřív, než zima začne ubližovat (hláška ColdInstinct).
    /// </summary>
    private static void NewbornWarmth(Character c, double now, NewbornWarmthRules rules)
    {
        if (now < c.WarmUntil)
        {
            c.Warmth = Math.Max(c.Warmth, GameData.Body.Damage.ColdBelow + 1);
        }

        if (now < c.ColdInstinctUntil && c.Warmth < rules.InstinctBelow && now - c.LastColdInstinct >= rules.InstinctEveryMinutes)
        {
            c.LastColdInstinct = now;
            c.Report(now, BodyEvent.ColdInstinct);
        }
    }

    private static void UpdateWarmth(Character c, GameClock clock, double minutes, bool nearFire, bool sheltered, WeatherState weather, WarmthRules rules)
    {
        var ambient = AmbientTemperature(clock, rules);
        var fire = nearFire ? GameData.Fire.Fire.WarmthBonus : 0;
        var shelter = sheltered ? GameData.Building.Shelter.WarmthBonus : 0;
        var sleeping = c.Sleeping ? GameData.Body.Sleep.WarmthBonus : 0;
        var rain = sheltered ? 0 : Weather.WarmthPenalty(weather);
        var target = Math.Clamp(rules.Base + c.Traits.ColdBonus + (ambient - rules.ComfortAmbient) * rules.AmbientFactor + fire + shelter + sleeping - rain, 0, 100);
        c.Warmth += (target - c.Warmth) * Math.Min(1, rules.ApproachPerMinute * minutes);
        NewbornWarmth(c, clock.TotalMinutes, rules.Newborn);
    }

    /// <summary>
    /// Kdy se spící postava probudí (prototyp physiology): v bezvědomí, zimou, nebo odpočatá
    /// (síla plná, nebo skoro plná a je den).
    /// </summary>
    private static void UpdateSleep(Character c, GameClock clock, SleepRules rules)
    {
        if (!c.Sleeping)
        {
            return;
        }

        var now = clock.TotalMinutes;
        if (c.FaintMinutes > 0)
        {
            c.WakeUp(now, null);
        }
        else if (c.Warmth < rules.WakeWarmthBelow)
        {
            c.WakeUp(now, BodyEvent.WokeCold);
        }
        else if (c.Energy >= 100 || (c.Energy > rules.WakeEnergyInDaylight && clock.Daylight > rules.WakeDaylightAbove))
        {
            c.WakeUp(now, BodyEvent.WokeRested);
        }
    }

    private static void UpdateNeeds(Character c, double minutes, NeedsRules rules)
    {
        var thirst = 100 / rules.HydrationEmptyMinutes;
        if (c.Warmth > rules.HotWarmthAbove)
        {
            thirst *= rules.HotThirstMultiplier;
        }

        if (c.Fever > rules.FeverThirstAbove)
        {
            thirst *= rules.FeverThirstMultiplier;
        }

        if (c.DiarrheaMinutes > 0)
        {
            thirst *= rules.DiarrheaThirstMultiplier;
            c.DiarrheaMinutes = Math.Max(0, c.DiarrheaMinutes - minutes);
        }

        var sleep = GameData.Body.Sleep;
        c.Hydration = Math.Max(0, c.Hydration - thirst * minutes * (c.Sleeping ? sleep.HydrationMultiplier : 1));
        c.Satiety = Math.Max(0, c.Satiety - 100 / rules.SatietyEmptyMinutes * minutes * (c.Sleeping ? sleep.SatietyMultiplier : 1));
        if (c.Sleeping)
        {
            c.Energy = Math.Min(100, c.Energy + sleep.EnergyPerMinute * minutes);
            c.SleepMinutes += minutes;
            return;
        }

        var drain = c.StimulantMinutes > 0 ? rules.StimulantEnergyDrainMultiplier : 1;
        c.Energy = Math.Max(0, c.Energy - 100 / rules.EnergyEmptyMinutes * minutes * c.Traits.EnergyDrainMultiplier * drain);
    }

    /// <summary>Toxin a zánět pomalu mizí. Horečka se blíží hodnotě danou toxinem a zánětem.</summary>
    private static void UpdateRecoveryAndFever(Character c, double now, double minutes, BodyRules rules)
    {
        c.Toxin = Math.Max(0, c.Toxin - rules.Recovery.ToxinPerMinute * minutes);
        c.Infection = Math.Max(0, c.Infection - rules.Recovery.InfectionPerMinute * minutes * c.Traits.HealMultiplier);

        // Zranění od zvířat: kulhání se pomalu hojí, krvácení pomalu ustává (a ubírá zdraví, viz ApplyDamage).
        var wounds = GameData.Animals.Wounds;
        c.Limp = Math.Max(0, c.Limp - wounds.LimpDecayPerMinute * minutes * c.Traits.HealMultiplier);

        var fever = rules.Fever;
        var target = Math.Max(0, (c.Toxin - fever.ToxinAbove) * fever.ToxinFactor) + c.Infection * fever.InfectionFactor;
        c.Fever += (target - c.Fever) * Math.Min(1, fever.ApproachPerMinute * minutes);

        if (c.Fever > fever.NoticeAbove && !c.FeverNoticed)
        {
            c.FeverNoticed = true;
            c.Report(now, BodyEvent.FeverStarted);
        }
        else if (c.Fever < fever.ResetBelow)
        {
            c.FeverNoticed = false;
        }
    }

    /// <summary>
    /// Ochrana první otravy: když otrava začne u zdravé postavy, jed ji nezabije.
    /// Ochrana skončí, až toxin i horečka opadnou.
    /// </summary>
    private static void UpdatePoisonProtection(Character c, ProtectionRules rules)
    {
        if (c.Toxin >= rules.EpisodeToxinAbove && !c.InToxinEpisode)
        {
            c.InToxinEpisode = true;
            c.PoisonProtected = c.Health >= rules.MinHealthToProtect;
        }

        if (c.Toxin < rules.EpisodeEndToxinBelow && c.Fever < rules.EpisodeEndFeverBelow)
        {
            c.InToxinEpisode = false;
            if (c.TremorMinutes <= 0)
            {
                c.PoisonProtected = false;
            }
        }
    }

    private static void CountDownConditions(Character c, double now, double minutes)
    {
        c.TremorMinutes = Math.Max(0, c.TremorMinutes - minutes);
        c.StimulantMinutes = Math.Max(0, c.StimulantMinutes - minutes);
        if (c.FaintMinutes > 0)
        {
            c.FaintMinutes -= minutes;
            if (c.FaintMinutes <= 0)
            {
                c.FaintMinutes = 0;
                c.Report(now, BodyEvent.CameTo);
            }
        }
    }

    private static void ApplyDamage(Character c, double minutes, DamageRules rules)
    {
        if (c.Hydration <= 0)
        {
            c.TakeDamage(rules.ThirstPerMinute * minutes, DeathCause.Thirst);
        }

        if (c.Satiety <= 0)
        {
            c.TakeDamage(rules.HungerPerMinute * minutes, DeathCause.Hunger);
        }

        if (c.Warmth < rules.ColdBelow)
        {
            c.TakeDamage(rules.ColdPerMinute * minutes * (1 + (rules.ColdBelow - c.Warmth) / rules.ColdScale), DeathCause.Cold);
        }

        if (c.Fever > rules.FeverAbove)
        {
            var cause = c.Infection > c.Toxin * 0.5 ? DeathCause.Infection : DeathCause.Fever;
            c.TakeDamage(rules.FeverPerMinute * minutes, cause);
        }

        if (c.Toxin > rules.ToxinAbove)
        {
            c.TakeDamage((c.Toxin - rules.ToxinAbove) * rules.ToxinFactor * minutes, DeathCause.Toxin);
        }

        if (c.Bleed > 0)
        {
            c.TakeDamage(c.Bleed * minutes, DeathCause.Bleeding);
            c.Bleed = Math.Max(0, c.Bleed - GameData.Animals.Wounds.BleedDecayPerMinute * minutes);
        }
    }

    private static void Heal(Character c, double minutes, RecoveryRules rules)
    {
        var healthy = c.Satiety > rules.HealMinSatiety
            && c.Hydration > rules.HealMinHydration
            && c.Fever < rules.HealMaxFever
            && c.Toxin < rules.HealMaxToxin
            && c.Warmth > rules.HealMinWarmth
            && c.Bleed <= 0;
        if (healthy)
        {
            var sleepHeal = c.Sleeping ? GameData.Body.Sleep.HealMultiplier : 1;
            c.Health = Math.Min(100, c.Health + rules.HealthPerMinute * minutes * c.Traits.HealMultiplier * sleepHeal);
        }
    }

    private static void ForgetOldBlame(Character c, double minutes, DamageRules rules)
    {
        c.BlameMinutes += minutes;
        if (c.BlameMinutes > rules.BlameHalfLifeMinutes)
        {
            c.BlameMinutes = 0;
            c.HalveBlame();
        }
    }

    /// <summary>Provede účinky jídla, jejichž čas nastal (prototyp runEvents).</summary>
    private static void RunDueEffects(Character c, double now)
    {
        var rules = GameData.Body.Outcomes;
        foreach (var effect in c.TakeDueEffects(now))
        {
            switch (effect.Type)
            {
                case EffectType.Vomit:
                    c.Satiety = Math.Max(0, c.Satiety - rules.VomitSatietyFactor * effect.Strength - rules.VomitSatietyBase);
                    c.Hydration = Math.Max(0, c.Hydration - rules.VomitHydration);
                    c.Report(now, BodyEvent.Vomited);
                    break;
                case EffectType.Diarrhea:
                    c.DiarrheaMinutes = Math.Max(c.DiarrheaMinutes, rules.DiarrheaMinutesFactor * effect.Strength);
                    c.Report(now, BodyEvent.Diarrhea);
                    break;
                case EffectType.Toxin:
                    c.Toxin += effect.Strength;
                    if (effect.Strength > rules.HeavyFeelingAbove)
                    {
                        c.Report(now, BodyEvent.HeavyFeeling);
                    }

                    break;
                case EffectType.Neurotoxin:
                    ApplyNeurotoxin(c, now, effect.Strength, rules);
                    break;
                case EffectType.Crash:
                    c.Energy = Math.Max(0, c.Energy - rules.CrashEnergyFactor * effect.Strength - rules.CrashEnergyBase);
                    c.Report(now, BodyEvent.Crash);
                    break;
                case EffectType.Sickness:
                    c.Infection = Math.Max(c.Infection, effect.Strength);
                    c.Report(now, BodyEvent.FeltSick);
                    break;
                case EffectType.WoundInfection:
                    c.Infection = Math.Max(c.Infection, effect.Strength);
                    c.Report(now, BodyEvent.WoundInflamed);
                    break;
                case EffectType.MarshFever:
                    Diseases.Start(c, DiseaseKind.MarshFever, effect.Strength, now);
                    break;
                case EffectType.Dysentery:
                    Diseases.Start(c, DiseaseKind.Dysentery, effect.Strength, now);
                    break;
            }
        }
    }

    /// <summary>Nervový jed: třes, při silné dávce bezvědomí, při velmi silné vážné poškození.</summary>
    private static void ApplyNeurotoxin(Character c, double now, double strength, OutcomeRules rules)
    {
        if (c.Health >= GameData.Body.FirstPoisonProtection.MinHealthToProtect)
        {
            c.PoisonProtected = true;
        }

        c.TremorMinutes = Math.Max(c.TremorMinutes, strength * rules.TremorMinutesFactor);
        c.Report(now, BodyEvent.Tremor);

        if (strength > rules.FaintAbove)
        {
            c.FaintMinutes = Math.Max(c.FaintMinutes, rules.FaintMinutesFactor * strength);
            c.TakeDamage(rules.FaintDamageFactor * strength, DeathCause.Neurotoxin);
            c.Report(now, BodyEvent.Fainted);
        }

        if (strength > rules.SevereNeuroAbove)
        {
            c.TakeDamage(rules.SevereNeuroDamage, DeathCause.Neurotoxin);
        }
    }
}

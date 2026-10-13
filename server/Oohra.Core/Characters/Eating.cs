using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Secret;
using Oohra.Core.Time;

namespace Oohra.Core.Characters;

/// <summary>
/// Jedení a ochutnávání rostlin (prototyp consume a tasteText v part4.js).
/// Výživa a povzbuzení působí hned, zvracení, průjem a jedy se naplánují se zpožděním.
/// </summary>
public static class Eating
{
    /// <summary>Sní celou část rostliny.</summary>
    public static void Eat(Character c, PlantItem item, SpeciesSecret species, GameClock clock)
    {
        Consume(c, item, species, clock, GameData.Body.Eating.EatDose, taste: false);
    }

    /// <summary>Ochutná malý kousek: dostane nápovědu chuti a slabší účinky.</summary>
    public static void Taste(Character c, PlantItem item, SpeciesSecret species, GameClock clock)
    {
        Consume(c, item, species, clock, GameData.Body.Eating.TasteDose, taste: true);
    }

    private static void Consume(Character c, PlantItem item, SpeciesSecret species, GameClock clock, double dose, bool taste)
    {
        if (c.IsDead)
        {
            return;
        }

        var rules = GameData.Body;
        var now = clock.TotalMinutes;
        var v = CompositionCalculator.Compute(item, species);

        if (!taste && InstinctStops(c, v, rules.Instinct))
        {
            dose *= rules.Instinct.DoseKept;
            c.Report(now, BodyEvent.BitternessStopped);
        }

        c.Satiety = Math.Min(100, c.Satiety + v.Nutrition * rules.Eating.NutritionToSatiety * dose);
        ScheduleIrritation(c, now, v.Irritant * dose, rules.Effects);
        SchedulePoisons(c, now, v, dose, rules.Effects);
        ApplyStimulant(c, now, v.Stimulant * dose, rules.Effects.Stimulant);
        ApplyMedicine(c, v.Medicine * dose, rules.Effects.Medicine);

        if (taste)
        {
            c.Report(now, BodyEvent.Tasted, TasteNotes(c, v, rules.Taste));
        }
        else
        {
            c.Report(now, BodyEvent.Ate);
        }
    }

    /// <summary>Instinkt Nových: hořká nebo jedovatá věc je zastaví (jen někdy).</summary>
    private static bool InstinctStops(Character c, Composition v, InstinctRules rules)
    {
        var worry = v.Toxin + v.Neurotoxin * rules.NeurotoxinWeight
            + (v.Irritant > rules.IrritantAbove ? v.Irritant * rules.IrritantWeight : 0);
        var generation = c.Traits.Generation;
        return generation >= rules.FromGeneration
            && worry > rules.WorryAbove
            && c.Rng.NextDouble() < Math.Min(rules.MaxChance, rules.BaseChance + rules.ChancePerGeneration * generation);
    }

    private static void ScheduleIrritation(Character c, double now, double irritant, EffectRules rules)
    {
        if (irritant < rules.Vomit.MinIrritant)
        {
            return;
        }

        var vomit = rules.Vomit;
        if (c.Rng.NextDouble() < Math.Clamp(irritant * vomit.ChanceFactor, 0, 1))
        {
            var delay = vomit.DelayMin + c.Rng.NextDouble() * vomit.DelayRange;
            c.Schedule(now + delay, EffectType.Vomit, Math.Clamp(irritant, vomit.MinStrength, vomit.MaxStrength));
        }

        var diarrhea = rules.Diarrhea;
        if (irritant > diarrhea.MinIrritant && c.Rng.NextDouble() < diarrhea.Chance)
        {
            var delay = diarrhea.DelayMin + c.Rng.NextDouble() * diarrhea.DelayRange;
            c.Schedule(now + delay, EffectType.Diarrhea, Math.Clamp(irritant, diarrhea.MinStrength, diarrhea.MaxStrength));
        }
    }

    /// <summary>Toxin a nervový jed působí až po čase. Pradávný snese víc (ToxinMultiplier).</summary>
    private static void SchedulePoisons(Character c, double now, Composition v, double dose, EffectRules rules)
    {
        var multiplier = c.Traits.ToxinMultiplier;
        if (v.Toxin * dose > rules.Toxin.MinDose)
        {
            var delay = rules.Toxin.DelayMin + c.Rng.NextDouble() * rules.Toxin.DelayRange;
            c.Schedule(now + delay, EffectType.Toxin, v.Toxin * dose * rules.Toxin.Factor * multiplier);
        }

        if (v.Neurotoxin * dose > rules.Neurotoxin.MinDose)
        {
            var delay = rules.Neurotoxin.DelayMin + c.Rng.NextDouble() * rules.Neurotoxin.DelayRange;
            c.Schedule(now + delay, EffectType.Neurotoxin, v.Neurotoxin * dose * multiplier);
        }
    }

    /// <summary>Povzbuzení přidá sílu hned, po čase přijde útlum.</summary>
    private static void ApplyStimulant(Character c, double now, double amount, StimulantEffect rules)
    {
        if (amount <= rules.MinDose)
        {
            return;
        }

        c.StimulantMinutes = Math.Max(c.StimulantMinutes, rules.DurationFactor * amount);
        c.Energy = Math.Min(100, c.Energy + rules.EnergyFactor * amount);
        c.Schedule(now + rules.CrashDelayMin + c.Rng.NextDouble() * rules.CrashDelayRange, EffectType.Crash, amount);
    }

    /// <summary>Léčivo hned sníží horečku, toxin a zánět.</summary>
    private static void ApplyMedicine(Character c, double amount, MedicineEffect rules)
    {
        if (amount <= rules.MinDose)
        {
            return;
        }

        c.Fever = Math.Max(0, c.Fever - rules.FeverFactor * amount);
        c.Toxin = Math.Max(0, c.Toxin - rules.ToxinFactor * amount);
        c.Infection = Math.Max(0, c.Infection - rules.InfectionFactor * amount);
    }

    /// <summary>
    /// Nápovědy chuti. Něco napoví spolehlivě (sladké, pálí), hořkost a „mýdlovost“ jen s nějakou šancí.
    /// Noví generace cítí hořkost lépe.
    /// </summary>
    private static List<TasteNote> TasteNotes(Character c, Composition v, TasteRules rules)
    {
        var notes = new List<TasteNote>();
        var generation = c.Traits.Generation;
        var r = c.Rng;

        notes.Add(v.Nutrition > rules.SweetAbove ? TasteNote.Sweet
            : v.Nutrition > rules.BlandAbove ? TasteNote.Bland
            : TasteNote.Tasteless);

        var sharpness = rules.SharpnessBase + rules.SharpnessPerGeneration * generation;
        if (v.Irritant > rules.BurnsAbove)
        {
            notes.Add(TasteNote.Burns);
        }
        else if (v.Irritant > rules.IrritatesAbove && r.NextDouble() < Math.Min(rules.SharpnessMaxChance, sharpness + rules.IrritatesBonus))
        {
            notes.Add(TasteNote.Irritates);
        }

        var veryBitterChance = Math.Min(rules.VeryBitterMaxChance, rules.VeryBitterChance + rules.VeryBitterChancePerGeneration * generation);
        if (v.Toxin > rules.VeryBitterAbove && r.NextDouble() < veryBitterChance)
        {
            notes.Add(TasteNote.VeryBitter);
        }
        else if (v.Toxin > rules.BitterAbove && r.NextDouble() < Math.Min(rules.SharpnessMaxChance, sharpness))
        {
            notes.Add(TasteNote.Bitter);
        }

        if (v.Neurotoxin > rules.SoapyAbove && r.NextDouble() < rules.SoapyChance)
        {
            notes.Add(TasteNote.Soapy);
        }

        if (v.Stimulant > rules.FreshAbove)
        {
            notes.Add(TasteNote.Fresh);
        }

        if (v.Medicine > rules.HerbalAbove)
        {
            notes.Add(TasteNote.Herbal);
        }

        return notes;
    }
}

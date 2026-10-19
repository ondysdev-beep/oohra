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
    /// <summary>Sní celou část rostliny. spoiled: plod je zkažený (Spoilage).</summary>
    public static void Eat(Character c, PlantItem item, SpeciesSecret species, GameClock clock, bool spoiled = false)
    {
        Consume(c, CompositionCalculator.Compute(item, species), clock, GameData.Body.Eating.EatDose, taste: false, spoiled);
        ApplyCures(c, item, species, GameData.Body.Eating.EatDose, clock);
    }

    /// <summary>Lék na nemoc ve snědené části (skryté složení): nemoc zeslabí, jinou nemoc neléčí.</summary>
    private static void ApplyCures(Character c, PlantItem item, SpeciesSecret species, double dose, GameClock clock)
    {
        if (c.IsDead)
        {
            return;
        }

        var cures = GameData.Archetypes[species.ArchetypeIndex].CuresFor(item.Part);
        Diseases.Cure(c, cures, dose, item.Cooking != Cooking.Raw, clock.TotalMinutes);
    }

    /// <summary>Ochutná malý kousek: dostane nápovědu chuti a slabší účinky.</summary>
    public static void Taste(Character c, PlantItem item, SpeciesSecret species, GameClock clock, bool spoiled = false)
    {
        Consume(c, CompositionCalculator.Compute(item, species), clock, GameData.Body.Eating.TasteDose, taste: true, spoiled);
        ApplyCures(c, item, species, GameData.Body.Eating.TasteDose, clock);
    }

    /// <summary>Sní (nebo ochutná) maso či rybu. Syrové trochu dráždí, upečené je výživné (prototyp comp pro meat).</summary>
    public static void EatMeat(Character c, Cooking cooking, GameClock clock, bool taste = false, bool spoiled = false)
    {
        var meat = GameData.Animals.Meat;
        var v = Composition.FromArray(cooking switch
        {
            Cooking.Raw => meat.Raw,
            Cooking.Smoked => meat.Smoked,
            Cooking.Dried => meat.Dried,
            _ => meat.Baked,
        });
        var rules = GameData.Body.Eating;
        Consume(c, v, clock, taste ? rules.TasteDose : rules.EatDose, taste, spoiled);
    }

    private static void Consume(Character c, Composition v, GameClock clock, double dose, bool taste, bool spoiled)
    {
        if (c.IsDead)
        {
            return;
        }

        var rules = GameData.Body;
        var now = clock.TotalMinutes;
        c.LastAte = now;

        if (!taste && InstinctStops(c, v, rules.Instinct))
        {
            dose *= rules.Instinct.DoseKept;
            c.Report(now, BodyEvent.BitternessStopped);
        }

        var nutrition = v.Nutrition * (spoiled ? rules.Spoilage.NutritionFactor : 1);
        c.Satiety = Math.Min(100, c.Satiety + nutrition * rules.Eating.NutritionToSatiety * dose);
        ScheduleIrritation(c, now, v.Irritant * dose, rules.Effects);
        SchedulePoisons(c, now, v, dose, rules.Effects);
        ApplyStimulant(c, now, v.Stimulant * dose, rules.Effects.Stimulant);
        ApplyMedicine(c, v.Medicine * dose, rules.Effects.Medicine);
        if (spoiled)
        {
            ScheduleSpoiledSickness(c, now, taste, rules.Spoilage);
        }

        if (taste)
        {
            var notes = TasteNotes(c, v, rules.Taste);
            if (spoiled)
            {
                notes.Add(TasteNote.Rotten);
            }

            c.Report(now, BodyEvent.Tasted, notes);
        }
        else
        {
            c.Report(now, BodyEvent.Ate);
        }
    }

    /// <summary>
    /// Zkažené jídlo (prototyp consume, bad): s šancí přijde nemoc (zánět a horečka) a někdy zvracení.
    /// Pradávní mají železný žaludek (spoiledMultiplier), u ochutnání je to slabší.
    /// </summary>
    private static void ScheduleSpoiledSickness(Character c, double now, bool taste, SpoilageRules rules)
    {
        var bad = c.Traits.SpoiledMultiplier;
        if (c.Rng.NextDouble() >= (taste ? rules.SicknessChanceTaste : rules.SicknessChance) * bad)
        {
            return;
        }

        if (c.Traits.IsAncient)
        {
            c.Report(now, BodyEvent.IronStomach);
        }

        var strength = (rules.SicknessMin + c.Rng.NextDouble() * rules.SicknessRange) * (taste ? rules.TasteFactor : 1) * bad;
        c.Schedule(now + rules.SicknessDelayMin + c.Rng.NextDouble() * rules.SicknessDelayRange, EffectType.Sickness, strength);
        if (c.Rng.NextDouble() < rules.VomitChance * bad)
        {
            c.Schedule(now + rules.VomitDelayMin + c.Rng.NextDouble() * rules.VomitDelayRange, EffectType.Vomit, rules.VomitStrength);
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

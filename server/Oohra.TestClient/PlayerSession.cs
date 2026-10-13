using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.TestClient;

/// <summary>Jedna postava na ostrově v konzoli: jí, ochutnává, čeká a ukazuje stav těla.</summary>
public sealed class PlayerSession
{
    private readonly Island _island;
    private readonly GameClock _clock = GameClock.FromSettings();
    private Character _character;

    public PlayerSession(Island island)
    {
        _island = island;
        _character = NewCharacter(0);
    }

    public string SetGeneration(int generation)
    {
        _character = NewCharacter(generation);
        return _character.Traits.IsAncient
            ? "Nová postava: Pradávný."
            : $"Nová postava: Nový, generace {generation}.";
    }

    /// <summary>Sní nebo ochutná část první rostliny daného druhu na ostrově.</summary>
    public string Consume(int speciesId, PlantPart part, Cooking cooking, bool taste)
    {
        if (_character.IsDead)
        {
            return "Jsi mrtvý. Napiš „generace 1“ pro novou postavu.";
        }

        if (speciesId < 0 || speciesId >= _island.Species.Count)
        {
            return $"Druh {speciesId} neexistuje (0 až {_island.Species.Count - 1}).";
        }

        var plant = _island.Plants.FirstOrDefault(p => p.SpeciesId == speciesId);
        if (plant == default)
        {
            return "Tento druh na ostrově neroste.";
        }

        var ripeness = Ripeness.Ripe;
        if (part == PlantPart.Fruit)
        {
            var stage = FruitStage(speciesId, plant.FruitOffset);
            if (stage is null)
            {
                return "Žádné plody tu nejsou.";
            }

            ripeness = stage.Value;
        }

        var item = new PlantItem(speciesId, part, ripeness, _clock.Season, plant.Tile, cooking);
        var secret = _island.SpeciesSecrets[speciesId];
        if (taste)
        {
            Eating.Taste(_character, item, secret, _clock);
        }
        else
        {
            Eating.Eat(_character, item, secret, _clock);
        }

        return string.Join("\n", _character.TakeMessages().Select(Texts.For));
    }

    /// <summary>Nechá běžet čas a vypisuje, co se s tělem děje.</summary>
    public IEnumerable<string> Wait(double minutes)
    {
        for (var done = 0.0; done < minutes && !_character.IsDead; done++)
        {
            Physiology.Run(_character, _clock, Math.Min(1, minutes - done));
            foreach (var message in _character.TakeMessages())
            {
                yield return $"[{TimeText()}] {Texts.For(message)}";
            }
        }

        if (_character.IsDead && _character.DeathCause is { } cause)
        {
            yield return Texts.For(cause);
        }
    }

    public string Status()
    {
        var c = _character;
        var symptoms = c.Symptoms();
        var lines = new List<string>
        {
            $"{TimeText()} · {Texts.SeasonName(_clock.Season)}, {_clock.MonthOfYear + 1}. měsíc · "
                + (c.Traits.IsAncient ? "Pradávný" : $"Nový, generace {c.Traits.Generation}"),
            $"Sytost {Bar(c.Satiety)}  Voda {Bar(c.Hydration)}  Síla {Bar(c.Energy)}",
            $"Teplo  {Bar(c.Warmth)}  Zdraví {Bar(c.Health)}",
            "Příznaky: " + (symptoms.Count == 0 ? "žádné" : string.Join(" · ", symptoms.Select(Texts.For))),
        };
        if (c.IsDead)
        {
            lines.Add("Jsi mrtvý.");
        }

        return string.Join("\n", lines);
    }

    /// <summary>Které druhy právě plodí (pro příkaz „plody“).</summary>
    public string FruitingSpecies()
    {
        var lines = new List<string>();
        foreach (var species in _island.Species)
        {
            var plant = _island.Plants.FirstOrDefault(p => p.SpeciesId == species.Id);
            var stage = plant == default ? null : FruitStage(species.Id, plant.FruitOffset);
            if (stage is not null)
            {
                lines.Add($"Druh {species.Id}: plody {StageName(stage.Value)}");
            }
        }

        return lines.Count == 0 ? "Teď nic neplodí." : string.Join("\n", lines);
    }

    private Ripeness? FruitStage(int speciesId, int fruitOffset)
    {
        var archetype = GameData.Archetypes[_island.SpeciesSecrets[speciesId].ArchetypeIndex];
        if (!archetype.HasFruit)
        {
            return null;
        }

        return FruitCalendar.Stage(_island.Species[speciesId], fruitOffset, _clock.CalendarMonths, GameData.Settings.MonthsPerYear);
    }

    private Character NewCharacter(int generation)
    {
        return new Character(generation, new Mulberry32(_island.Seed ^ 0x0B0D ^ generation));
    }

    private string TimeText()
    {
        var hour = (int)_clock.Hour;
        var minute = (int)((_clock.Hour - hour) * 60);
        return $"den {_clock.Day + 1}, {hour:00}:{minute:00}";
    }

    private static string StageName(Ripeness ripeness)
    {
        return ripeness switch
        {
            Ripeness.Unripe => "zelené",
            Ripeness.Ripe => "zralé",
            _ => "scvrklé, tmavé",
        };
    }

    private static string Bar(double value)
    {
        var filled = (int)Math.Round(Math.Clamp(value, 0, 100) / 10);
        return "[" + new string('#', filled) + new string('.', 10 - filled) + "]";
    }
}

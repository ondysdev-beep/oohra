using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>
/// Had (návrh 0.8, kap. 5, animals.json snake): leze pomalu kolem svého místa na skalách, kamenité půdě a loukách.
/// V zimě a v noci leží stočený (Asleep) a nekouše. Kdo přijde blíž než warnRange, had se vztyčí a syčí (Alert),
/// kdo blíž než biteRange, toho kousne: jed začne působit se zpožděním (DiseaseKind.SnakeVenom). V chladnějším
/// období kouše méně ochotně (activeSeasons jako šance). Jednou ranou je mrtvý.
/// </summary>
public sealed partial class Animals
{
    /// <summary>Je had v tuhle chvíli aktivní (teplé období a den)?</summary>
    public static bool SnakeActive(GameClock clock)
    {
        var rules = GameData.Animals.Snake;
        return rules.ActiveSeasons[(int)clock.Season] > 0 && clock.Daylight >= rules.ActiveMinDaylight;
    }

    private void UpdateSnake(Island island, Animal a, GameClock clock, double dt, IReadOnlyList<AnimalTarget> players)
    {
        var rules = GameData.Animals.Snake;
        a.Aggro = Math.Max(0, a.Aggro - dt);
        if (!SnakeActive(clock))
        {
            a.State = AnimalState.Asleep;
            a.Prey = -1;
            return;
        }

        var (near, d) = Nearest(players, a.X, a.Y, _ => true);
        if (near is null || d > rules.WarnRange)
        {
            a.Prey = -1;
            Wander(island, a, dt, rules);
            return;
        }

        var now = clock.TotalMinutes;
        a.State = AnimalState.Alert;
        a.Dir = Math.Atan2(near.Y - a.Y, near.X - a.X);
        if (a.Prey != near.Id)
        {
            // Varování jednou za přiblížení.
            a.Prey = near.Id;
            near.Character.Report(now, BodyEvent.SnakeWarning);
        }

        if (d > rules.BiteRange || a.Aggro > 0 || !Sight.Clear(island, a.X, a.Y, near.X, near.Y))
        {
            return;
        }

        a.Aggro = rules.BiteCooldownSeconds;
        if (_rng.NextDouble() < rules.ActiveSeasons[(int)clock.Season])
        {
            near.Character.Report(now, BodyEvent.SnakeBite);
            Diseases.Infect(near.Character, DiseaseKind.SnakeVenom, now);
        }
    }

    /// <summary>Had se pomalu plazí po okolí svého místa, nevzdálí se od něj víc než wanderRange.</summary>
    private void Wander(Island island, Animal a, double dt, SnakeRules rules)
    {
        a.State = AnimalState.Wander;
        a.Timer -= dt;
        if (a.Timer <= 0)
        {
            a.Timer = 4 + _rng.NextDouble() * 6;
            a.TargetX = a.DenX + (_rng.NextDouble() * 2 - 1) * rules.WanderRange;
            a.TargetY = a.DenY + (_rng.NextDouble() * 2 - 1) * rules.WanderRange;
        }

        WalkToTarget(island, a, rules.WanderSpeed, dt, rules.Radius);
    }
}

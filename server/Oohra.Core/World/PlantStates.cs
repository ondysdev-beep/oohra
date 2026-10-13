namespace Oohra.Core.World;

/// <summary>Co se s rostlinou ve světě stalo: kdy jí dorostou listy a plody, do kdy je pryč (vykopaná).</summary>
public sealed class PlantState
{
    public double LeavesAt { get; set; }

    public double FruitAt { get; set; }

    public double GoneUntil { get; set; }
}

/// <summary>Stav všech rostlin, které už někdo sklízel (prototyp S.pc). Ostatní jsou netknuté.</summary>
public sealed class PlantStates
{
    private readonly Dictionary<int, PlantState> _states = [];

    public PlantState? Find(int plantId)
    {
        return _states.TryGetValue(plantId, out var state) ? state : null;
    }

    public PlantState For(int plantId)
    {
        if (!_states.TryGetValue(plantId, out var state))
        {
            state = new PlantState();
            _states[plantId] = state;
        }

        return state;
    }

    public bool IsGone(int plantId, double now)
    {
        var state = Find(plantId);
        return state is not null && state.GoneUntil > now;
    }
}

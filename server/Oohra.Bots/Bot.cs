using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Bots;

/// <summary>
/// Jeden robot: připojí se jako hráč a chová se zhruba jako člověk. Chodí po ostrově,
/// míří k rostlinám, trhá je, jí, pije u jezírka a po smrti se narodí znovu.
/// Náhoda je tu System.Random s pevným semínkem: roboti nejsou svět, jen hráči.
/// </summary>
public sealed class Bot : IDisposable
{
    private const double PlantReach = 0.9;
    private const double PlantSearchRadius = 8;
    private static readonly PlantPart[] Parts = [PlantPart.Leaf, PlantPart.Fruit, PlantPart.Root];

    private readonly Random _random;
    private Snapshot? _lastSnapshot;
    private double _nextDecision;

    public Bot(string nickname, int seed)
    {
        Nickname = nickname;
        _random = new Random(seed);
    }

    public string Nickname { get; }

    public GameConnection Connection { get; } = new();

    /// <summary>Kolik snímků s polohami hráčů přišlo (server je posílá 15× za sekundu).</summary>
    public int Snapshots { get; private set; }

    public int Harvests { get; private set; }

    public int Meals { get; private set; }

    public int Drinks { get; private set; }

    public int Deaths { get; private set; }

    public bool Ready => Connection.LoggedIn && Connection.World is not null && Connection.Self is not null;

    /// <summary>Kolik hráčů robot vidí v posledním snímku.</summary>
    public int VisiblePlayers => Connection.Snapshot?.Players.Length ?? 0;

    /// <summary>Snímky počítá vrácením, aby šlo měřit, kolik jich přišlo za úsek.</summary>
    public int TakeSnapshotCount()
    {
        var count = Snapshots;
        Snapshots = 0;
        return count;
    }

    public void Update(double nowSeconds)
    {
        Connection.Poll();
        if (!ReferenceEquals(Connection.Snapshot, _lastSnapshot) && Connection.Snapshot is not null)
        {
            Snapshots++;
            _lastSnapshot = Connection.Snapshot;
        }

        CountEvents();
        if (!Ready || nowSeconds < _nextDecision)
        {
            return;
        }

        _nextDecision = nowSeconds + 0.5 + _random.NextDouble() * 2.5;
        Decide();
    }

    private void CountEvents()
    {
        foreach (var gameEvent in Connection.TakeEvents())
        {
            if (gameEvent.Kind == EventKind.Harvest && gameEvent.Code == (byte)HarvestResult.Picked)
            {
                Harvests++;
            }
            else if (gameEvent.Kind == EventKind.Drink)
            {
                Drinks++;
            }
        }
    }

    private void Decide()
    {
        if (Connection.Self!.Dead)
        {
            Deaths++;
            Connection.SendInput(0, 0);
            Connection.Reborn();
            return;
        }

        var self = Connection.SelfSnapshot();
        if (self is null)
        {
            return;
        }

        var x = self.Value.X;
        var y = self.Value.Y;
        if (Connection.Self.Hydration < 60 && TryDrink(x, y))
        {
            return;
        }

        if (Connection.Inventory.Stacks.Length > 0 && _random.NextDouble() < 0.3)
        {
            Connection.Eat(_random.Next(Connection.Inventory.Stacks.Length));
            Meals++;
            return;
        }

        var plant = NearestPlant(x, y);
        if (plant >= 0 && Distance(Connection.World!.Plants[plant], x, y) < PlantReach)
        {
            Connection.SendInput(0, 0);
            Connection.Harvest(plant, Parts[_random.Next(Parts.Length)]);
            return;
        }

        Walk(plant, x, y);
    }

    /// <summary>Napije se z jezírka nebo moře na sousední dlaždici, pokud tam je.</summary>
    private bool TryDrink(float x, float y)
    {
        var world = Connection.World!;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var tx = (int)Math.Floor(x) + dx;
                var ty = (int)Math.Floor(y) + dy;
                if (tx < 0 || ty < 0 || tx >= world.Width || ty >= world.Height)
                {
                    continue;
                }

                var tile = world.Tiles[ty * world.Width + tx];
                if (tile == TileType.Pond || tile == TileType.Sea)
                {
                    Connection.SendInput(0, 0);
                    Connection.Drink(tx, ty);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Většinou jde k nejbližší rostlině, někdy jen náhodně bloudí (nebo stojí).</summary>
    private void Walk(int plant, float x, float y)
    {
        if (plant >= 0 && _random.NextDouble() < 0.6)
        {
            var target = Connection.World!.Plants[plant];
            Connection.SendInput(target.X - x, target.Y - y);
        }
        else if (_random.NextDouble() < 0.15)
        {
            Connection.SendInput(0, 0);
        }
        else
        {
            var angle = _random.NextDouble() * Math.PI * 2;
            Connection.SendInput((float)Math.Cos(angle), (float)Math.Sin(angle));
        }
    }

    private int NearestPlant(float x, float y)
    {
        var plants = Connection.World!.Plants;
        var gone = Connection.PlantVisuals;
        var best = -1;
        var bestDistance = PlantSearchRadius;
        for (var i = 0; i < plants.Length; i++)
        {
            if (i < gone.Length && gone[i] == PlantVisual.Gone)
            {
                continue;
            }

            var distance = Distance(plants[i], x, y);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static double Distance(PlantView plant, float x, float y)
    {
        var dx = plant.X - x;
        var dy = plant.Y - y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}

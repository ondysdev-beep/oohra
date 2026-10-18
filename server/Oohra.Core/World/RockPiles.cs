using Oohra.Core.Data;
using Oohra.Core.Persistence;
using Oohra.Core.Rng;

namespace Oohra.Core.World;

/// <summary>Hromada kamenů uprostřed skalní dlaždice.</summary>
public sealed class RockPile
{
    public RockPile(int tileIndex, double x, double y, int maxStones)
    {
        TileIndex = tileIndex;
        X = x;
        Y = y;
        MaxStones = maxStones;
        Stones = maxStones;
    }

    public int TileIndex { get; }

    public double X { get; }

    public double Y { get; }

    public int MaxStones { get; }

    /// <summary>Kolik kamenů v hromadě zbývá (0 = hromada zmizela).</summary>
    public int Stones { get; internal set; }

    /// <summary>Kdy se vybraná hromada obnoví (herní minuty).</summary>
    public double RegrowAt { get; internal set; }
}

/// <summary>
/// Hromady kamenů na skalách (rocks.json). Leží jen na části skalních dlaždic (podle seedu) a ne pod stromem.
/// Kámen se bere po jednom, prázdná hromada zmizí a po regrowDays dnech se obnoví.
/// Dokud v hromadě něco je, nedá se přes ni projít.
/// </summary>
public sealed class RockPiles
{
    private const double MinutesPerDay = 1440;

    private readonly Dictionary<int, RockPile> _piles = [];

    public RockPiles(Island island)
    {
        var rules = GameData.Rocks;
        var seed = island.Seed ^ rules.SeedSalt;
        for (var i = 0; i < island.Tiles.Length; i++)
        {
            var (x, y) = (i % island.Width, i / island.Width);
            if (island.Tiles[i] is not (TileType.Rock or TileType.LayeredRock) || island.HasTree(i) || Hash.Hash2(x, y, seed) >= rules.PileChance)
            {
                continue;
            }

            var stones = rules.MinStones + (int)(Hash.Hash2(x, y, seed + 1) * (rules.MaxStones - rules.MinStones + 1));
            _piles[i] = new RockPile(i, x + 0.5, y + 0.5, Math.Min(stones, rules.MaxStones));
        }
    }

    /// <summary>Všechny hromady (i vybrané) v pořadí dlaždic.</summary>
    public IEnumerable<RockPile> All => _piles.Values.OrderBy(p => p.TileIndex);

    /// <summary>Roste s každou změnou (server podle toho pošle hromady znovu).</summary>
    public int Version { get; private set; }

    /// <summary>Hromady, ze kterých někdo bral (ostatní jsou plné jako na začátku).</summary>
    public List<RockPileSave> Export()
    {
        return _piles.Values.Where(p => p.Stones != p.MaxStones).OrderBy(p => p.TileIndex)
            .Select(p => new RockPileSave(p.TileIndex, p.Stones, p.RegrowAt)).ToList();
    }

    public void Restore(IEnumerable<RockPileSave> saved)
    {
        foreach (var s in saved)
        {
            if (_piles.TryGetValue(s.TileIndex, out var pile))
            {
                pile.Stones = Math.Min(s.Stones, pile.MaxStones);
                pile.RegrowAt = s.RegrowAt;
            }
        }

        Version++;
    }

    public RockPile? At(int tileIndex)
    {
        return _piles.GetValueOrDefault(tileIndex);
    }

    /// <summary>Vezme z hromady jeden kámen. Vrací false, když tu hromada není nebo je prázdná.</summary>
    public bool TakeOne(int tileIndex, double now)
    {
        var pile = At(tileIndex);
        if (pile is null || pile.Stones <= 0)
        {
            return false;
        }

        pile.Stones--;
        if (pile.Stones == 0)
        {
            pile.RegrowAt = now + GameData.Rocks.RegrowDays * MinutesPerDay;
        }

        Version++;
        return true;
    }

    /// <summary>Prázdné hromady, kterým uplynula doba, se obnoví.</summary>
    public void Regrow(double now)
    {
        foreach (var pile in _piles.Values)
        {
            if (pile.Stones == 0 && now >= pile.RegrowAt)
            {
                pile.Stones = pile.MaxStones;
                Version++;
            }
        }
    }

    /// <summary>Kruh (postava, stavba) o poloměru radius se překrývá s hromadou, ve které něco je.</summary>
    public bool Blocks(Island island, double x, double y, double radius)
    {
        var reach = radius + GameData.Rocks.Radius;
        var (cx, cy) = ((int)Math.Floor(x), (int)Math.Floor(y));
        for (var ty = cy - 1; ty <= cy + 1; ty++)
        {
            for (var tx = cx - 1; tx <= cx + 1; tx++)
            {
                if (!island.InBounds(tx, ty) || !_piles.TryGetValue(island.Index(tx, ty), out var pile) || pile.Stones <= 0)
                {
                    continue;
                }

                if ((pile.X - x) * (pile.X - x) + (pile.Y - y) * (pile.Y - y) < reach * reach)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

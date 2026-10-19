using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Persistence;
using Oohra.Core.Rng;

namespace Oohra.Core.World;

/// <summary>Vosí hnízdo ve stromě. Id je číslo dlaždice stromu.</summary>
public sealed class WaspNest
{
    public WaspNest(int tileIndex, double x, double y)
    {
        TileIndex = tileIndex;
        X = x;
        Y = y;
    }

    public int TileIndex { get; }

    public double X { get; }

    public double Y { get; }

    /// <summary>Do kdy jsou vosy rozzuřené (reálné sekundy od startu).</summary>
    internal double AngryUntil { get; set; }

    /// <summary>Vykouřené: do kdy hnízdo není (herní minuty), jinak 0.</summary>
    public double GoneUntil { get; internal set; }

    public bool Angry(double time)
    {
        return time < AngryUntil;
    }
}

/// <summary>
/// Vosí hnízda (návrh 0.8, kap. 5, hazards.json wasps): ve stromech v lese a na loukách, vybraná ze seedu (stejný
/// svět = stejná hnízda). Kdo přijde blízko, vosy se rozzuří a chvíli štípou každého v okolí (jed a zranění).
/// Kouř z hořící pochodně je odežene a s pochodní jde hnízdo vykouřit, pak na pár dní zmizí. Hnízdo na pokáceném
/// stromě není.
/// </summary>
public sealed class WaspNests
{
    private const int SeedSalt = 0x5A5B;
    private const double ReportEverySeconds = 5;

    private readonly Island _island;
    private readonly Dictionary<int, WaspNest> _nests = [];
    private readonly Dictionary<int, double> _lastSting = [];
    private readonly Dictionary<int, double> _lastReport = [];
    private double _time;

    public WaspNests(Island island)
    {
        _island = island;
        var chance = GameData.Hazards.Wasps.ChancePerTree;
        foreach (var tree in island.Trees)
        {
            var (tx, ty) = (tree.TileIndex % island.Width, tree.TileIndex / island.Width);
            if (island.Regions[tree.TileIndex] is RegionKind.Forest or RegionKind.Meadow
                && Hash.Hash2(tx, ty, island.Seed ^ SeedSalt) < chance)
            {
                _nests[tree.TileIndex] = new WaspNest(tree.TileIndex, tree.X, tree.Y);
            }
        }
    }

    /// <summary>Zvedne se při každé změně, kterou vidí klient (hnízdo zmizí, vrátí se, rozzuří se, uklidní).</summary>
    public int Version { get; private set; }

    /// <summary>Hnízda, která teď jsou (nevykouřená a strom stojí).</summary>
    public IEnumerable<WaspNest> Active(double now)
    {
        return _nests.Values.Where(n => Present(n, now)).OrderBy(n => n.TileIndex);
    }

    public WaspNest? Find(int tileIndex)
    {
        return _nests.GetValueOrDefault(tileIndex);
    }

    public bool IsAngry(WaspNest nest)
    {
        return nest.Angry(_time);
    }

    /// <summary>Krok: vosy se rozzuří na blízkého hráče bez pochodně a štípou v okolí. Čas v reálných sekundách.</summary>
    public void Update(double realSeconds, double now, IReadOnlyList<AnimalTarget> players)
    {
        _time += realSeconds;
        var rules = GameData.Hazards.Wasps;
        foreach (var nest in _nests.Values)
        {
            if (nest.GoneUntil > 0 && now >= nest.GoneUntil)
            {
                nest.GoneUntil = 0;
                Version++;
            }

            if (!Present(nest, now))
            {
                continue;
            }

            var wasAngry = nest.Angry(_time);
            if (players.Any(p => Unprotected(p) && Distance(p, nest) < rules.AngerRange))
            {
                nest.AngryUntil = _time + rules.AngrySeconds;
            }

            if (nest.Angry(_time) != wasAngry)
            {
                Version++;
            }

            if (nest.Angry(_time))
            {
                Sting(nest, now, players, rules);
            }
        }
    }

    /// <summary>Vykouřit hnízdo hořící pochodní. Hnízdo zmizí na regrowDays dní.</summary>
    public WaspResult SmokeOut(int tileIndex, Character c, double x, double y, double now)
    {
        var rules = GameData.Hazards.Wasps;
        var nest = Find(tileIndex);
        if (nest is null || !Present(nest, now))
        {
            return WaspResult.NotFound;
        }

        if (!c.TorchLit)
        {
            return WaspResult.NeedTorch;
        }

        if (Math.Sqrt((nest.X - x) * (nest.X - x) + (nest.Y - y) * (nest.Y - y)) > rules.SmokeReach)
        {
            return WaspResult.TooFar;
        }

        nest.GoneUntil = now + rules.RegrowDays * 1440;
        nest.AngryUntil = 0;
        Version++;
        c.Report(now, BodyEvent.NestSmokedOut);
        return WaspResult.SmokedOut;
    }

    /// <summary>Vykouřená hnízda (ostatní vzniknou znovu ze seedu).</summary>
    public List<WaspNestSave> Export()
    {
        return _nests.Values.Where(n => n.GoneUntil > 0).OrderBy(n => n.TileIndex).Select(n => new WaspNestSave(n.TileIndex, n.GoneUntil)).ToList();
    }

    public void Restore(IEnumerable<WaspNestSave> saved)
    {
        foreach (var s in saved)
        {
            if (_nests.TryGetValue(s.TileIndex, out var nest))
            {
                nest.GoneUntil = s.GoneUntil;
            }
        }

        Version++;
    }

    private void Sting(WaspNest nest, double now, IReadOnlyList<AnimalTarget> players, WaspRules rules)
    {
        foreach (var p in players)
        {
            if (!Unprotected(p) || Distance(p, nest) > rules.ChaseRange
                || (_lastSting.TryGetValue(p.Id, out var last) && _time - last < rules.StingSeconds))
            {
                continue;
            }

            _lastSting[p.Id] = _time;
            p.Character.Toxin += rules.StingToxin;
            p.Character.TakeDamage(rules.StingDamage, DeathCause.Toxin);
            if (!_lastReport.TryGetValue(p.Id, out var reported) || _time - reported >= ReportEverySeconds)
            {
                _lastReport[p.Id] = _time;
                p.Character.Report(now, BodyEvent.WaspSting);
            }
        }
    }

    private bool Present(WaspNest nest, double now)
    {
        return (nest.GoneUntil <= 0 || now >= nest.GoneUntil) && _island.HasTree(nest.TileIndex);
    }

    /// <summary>Živý hráč bez hořící pochodně (kouř vosy odežene).</summary>
    private static bool Unprotected(AnimalTarget p)
    {
        return !p.Character.IsDead && !p.Character.TorchLit;
    }

    private static double Distance(AnimalTarget p, WaspNest nest)
    {
        return Math.Sqrt((p.X - nest.X) * (p.X - nest.X) + (p.Y - nest.Y) * (p.Y - nest.Y));
    }
}

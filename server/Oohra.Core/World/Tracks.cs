using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>Stopa v bahně: kde, kterým směrem (radiány), kdo ji udělal a jak je stará (herní minuty).</summary>
public sealed class Track
{
    public Track(double x, double y, double dir, TrackMaker maker)
    {
        X = x;
        Y = y;
        Dir = dir;
        Maker = maker;
    }

    public double X { get; }

    public double Y { get; }

    public double Dir { get; }

    public TrackMaker Maker { get; }

    /// <summary>Jak je stopa opotřebovaná (herní minuty, déšť počítá víckrát).</summary>
    public double Worn { get; internal set; }
}

/// <summary>
/// Stopy v bahně (návrh 0.8, kap. 5, seasons.json tracks): hráči a zvířata, kteří jdou bažinou nebo zatopenou
/// souší, nechávají stopy. Stopa časem zmizí, v dešti rychle. Neukládají se (jsou krátkodobé), klient dostane
/// jen stopy ve svém okolí.
/// </summary>
public sealed class Tracks
{
    private readonly List<Track> _tracks = [];

    /// <summary>Kde kdo naposledy nechal stopu (jen dokud je v bahně).</summary>
    private readonly Dictionary<(bool Animal, int Id), (double X, double Y)> _last = [];

    public IReadOnlyList<Track> All => _tracks;

    /// <summary>Zvedne se, když stopa přibude nebo zmizí.</summary>
    public int Version { get; private set; }

    /// <summary>Kdo se kde pohybuje: nové stopy v bahně, staré stárnou (minutes herních minut).</summary>
    public void Update(Island island, IReadOnlyList<AnimalTarget> players, IReadOnlyList<Animal> animals, double minutes, WeatherKind weather)
    {
        var rules = GameData.Seasons.Tracks;
        var seen = new HashSet<(bool, int)>();
        foreach (var p in players)
        {
            if (!p.Character.IsDead)
            {
                Step(island, (false, p.Id), p.X, p.Y, TrackMaker.Human, rules, seen);
            }
        }

        foreach (var a in animals)
        {
            if (!a.IsDead)
            {
                Step(island, (true, a.Id), a.X, a.Y, MakerOf(a.Kind), rules, seen);
            }
        }

        // Kdo zmizel ze světa, začne příště novou řadu stop.
        foreach (var key in _last.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _last.Remove(key);
        }

        Age(minutes * (weather is WeatherKind.Rain or WeatherKind.Storm ? rules.RainFactor : 1), rules);
    }

    /// <summary>Stopy do distance metrů od (x, y).</summary>
    public IEnumerable<Track> Near(double x, double y, double distance)
    {
        return _tracks.Where(t => (t.X - x) * (t.X - x) + (t.Y - y) * (t.Y - y) <= distance * distance);
    }

    /// <summary>Bahno: bažina, nebo souš zatopená na jaře.</summary>
    public static bool Muddy(Island island, double x, double y)
    {
        var (tx, ty) = ((int)Math.Floor(x), (int)Math.Floor(y));
        return island.InBounds(tx, ty) && (island.TileAt(tx, ty) == TileType.Bog || island.Flood.IsFlooded(tx, ty));
    }

    public static TrackMaker MakerOf(AnimalKind kind)
    {
        return (TrackMaker)((int)kind + 1);
    }

    private void Step(Island island, (bool, int) key, double x, double y, TrackMaker maker, TrackRules rules, HashSet<(bool, int)> seen)
    {
        if (!Muddy(island, x, y))
        {
            return;
        }

        seen.Add(key);
        if (!_last.TryGetValue(key, out var last))
        {
            _last[key] = (x, y);
            return;
        }

        var (dx, dy) = (x - last.X, y - last.Y);
        if (dx * dx + dy * dy < rules.StepMeters * rules.StepMeters)
        {
            return;
        }

        _last[key] = (x, y);
        _tracks.Add(new Track(x, y, Math.Atan2(dy, dx), maker));
        if (_tracks.Count > rules.MaxTracks)
        {
            _tracks.RemoveAt(0);
        }

        Version++;
    }

    private void Age(double worn, TrackRules rules)
    {
        foreach (var track in _tracks)
        {
            track.Worn += worn;
        }

        if (_tracks.RemoveAll(t => t.Worn >= rules.LifeMinutes) > 0)
        {
            Version++;
        }
    }
}

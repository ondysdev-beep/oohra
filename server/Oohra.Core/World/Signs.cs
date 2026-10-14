using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Social;

namespace Oohra.Core.World;

/// <summary>Záznam na staré rytině: rostlina (kresba jejího tvaru), jméno ze slabik a poznámky. Může se mýlit.</summary>
public sealed record CarvingRecord(int SpeciesId, byte[] Name, IReadOnlyList<CarvingTag> Tags);

/// <summary>
/// Značka ve světě: kresba 12 × 12 bodů. Na skále je to rytina a vydrží, v hlíně a písku zvětrá.
/// Staré rytiny u ruin (Ancient) jsou tu od počátku světa a nesmaže je nikdo.
/// </summary>
public sealed class Sign
{
    public Sign(int id, double x, double y, byte[] bits, int authorId, double createdAt, bool carved, bool ancient = false, CarvingRecord? record = null)
    {
        Id = id;
        X = x;
        Y = y;
        Bits = bits;
        AuthorId = authorId;
        CreatedAt = createdAt;
        Carved = carved;
        Ancient = ancient;
        Record = record;
    }

    public int Id { get; }

    public double X { get; }

    public double Y { get; }

    public byte[] Bits { get; }

    /// <summary>Číslo hráče, který značku nakreslil (-1 u starých rytin).</summary>
    public int AuthorId { get; }

    /// <summary>Kdy vznikla (herní minuty).</summary>
    public double CreatedAt { get; }

    /// <summary>Vyrytá do skály, nezvětrá.</summary>
    public bool Carved { get; }

    public bool Ancient { get; }

    public CarvingRecord? Record { get; }
}

/// <summary>Všechny značky ve světě (prototyp S.signs a world.ruins).</summary>
public sealed class Signs
{
    private const double MinutesPerDay = 1440;

    private readonly List<Sign> _signs = [];
    private int _nextId = 1;

    public IReadOnlyList<Sign> All => _signs;

    /// <summary>Roste s každou změnou (server podle toho pošle značky znovu).</summary>
    public int Version { get; private set; }

    public Sign? Find(int signId)
    {
        return _signs.Find(s => s.Id == signId);
    }

    /// <summary>
    /// Hráč nakreslí značku na místo (x, y) kousek od sebe. Na skále vyryje rytinu, jinak kreslí do hlíny.
    /// </summary>
    public SignResult Draw(Island island, int authorId, double playerX, double playerY, double x, double y, byte[] bits, double now)
    {
        var rules = GameData.Signs;
        if (bits.Length != SignBits.ByteCount || SignBits.Count(bits) == 0)
        {
            return SignResult.Empty;
        }

        if (Distance(playerX, playerY, x, y) > rules.DrawReach)
        {
            return SignResult.TooFar;
        }

        var (tx, ty) = ((int)Math.Floor(x), (int)Math.Floor(y));
        if (!island.InBounds(tx, ty) || Drinking.IsWater(island.TileAt(tx, ty))
            || _signs.Any(s => Distance(s.X, s.Y, x, y) < rules.MinSpacing))
        {
            return SignResult.CannotDrawHere;
        }

        if (_signs.Count(s => s.AuthorId == authorId) >= rules.MaxPerAuthor)
        {
            return SignResult.TooMany;
        }

        var carved = island.TileAt(tx, ty) == TileType.Rock;
        Add(new Sign(_nextId++, x, y, (byte[])bits.Clone(), authorId, now, carved));
        return SignResult.Drawn;
    }

    /// <summary>
    /// Smazat jde vlastní značku nebo značku člena svého kmene (návrh 14.2 a 14.3). Staré rytiny nesmaže nikdo.
    /// </summary>
    public SignResult Erase(int signId, int authorId, double playerX, double playerY, Tribes? tribes = null)
    {
        var sign = Find(signId);
        if (sign is null)
        {
            return SignResult.NotFound;
        }

        if (sign.Ancient || (sign.AuthorId != authorId && tribes?.SameTribe(sign.AuthorId, authorId) != true))
        {
            return SignResult.NotYours;
        }

        if (Distance(playerX, playerY, sign.X, sign.Y) > GameData.Signs.EraseReach)
        {
            return SignResult.TooFar;
        }

        _signs.Remove(sign);
        Version++;
        return SignResult.Erased;
    }

    /// <summary>Kresby v hlíně a písku po weatherDays dnech zvětrají. Rytiny zůstanou.</summary>
    public void Weather(double now)
    {
        if (_signs.RemoveAll(s => WeathersAt(s) <= now) > 0)
        {
            Version++;
        }
    }

    /// <summary>Kdy kresba zvětrá (herní minuty), nebo null u rytiny.</summary>
    public static double? WeathersAt(Sign sign)
    {
        return sign.Carved ? null : sign.CreatedAt + GameData.Signs.WeatherDays * MinutesPerDay;
    }

    /// <summary>Staré rytiny u ruin (volá se jednou při startu světa).</summary>
    public void AddAncient(Island island)
    {
        foreach (var carving in RuinCarvings.For(island))
        {
            Add(new Sign(_nextId++, carving.X, carving.Y, carving.Bits, -1, 0, carved: true, ancient: true, carving.Record));
        }
    }

    private void Add(Sign sign)
    {
        _signs.Add(sign);
        Version++;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    }
}

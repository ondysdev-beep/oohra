using Oohra.Core.Data;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>
/// Rozvodněná bažina na jaře (návrh 0.8, kap. 5, seasons.json flood): voda se rozlije z bažiny do okolní souše
/// kolem bažiny (tráva, les, písek). Jak daleko, záleží na měsíci roku, takže se nic neukládá (vyplývá z kalendáře). Zatopená
/// souš zpomalí (jako bažina), chytají se tam pijavice a zůstávají stopy.
/// </summary>
public sealed class Flooding
{
    private const byte Never = byte.MaxValue;

    private readonly Island _island;

    /// <summary>Kolik dlaždic je souš od bažinné vody (0 = sama voda, Never = moc daleko nebo se nezatopí).</summary>
    private readonly byte[] _distance;

    public Flooding(Island island)
    {
        _island = island;
        _distance = DistancesFromMarshWater(island);
    }

    /// <summary>Do kolika dlaždic od vody je teď zatopeno (0 = nic).</summary>
    public int Ring { get; private set; }

    /// <summary>Zvedne se, když se zatopená plocha změní.</summary>
    public int Version { get; private set; }

    /// <summary>Úroveň vody podle kalendáře.</summary>
    public void Update(GameClock clock)
    {
        var ring = RingFor(clock.MonthOfYear);
        if (ring != Ring)
        {
            Ring = ring;
            Version++;
        }
    }

    /// <summary>Do kolika dlaždic od vody je v daném měsíci roku zatopeno.</summary>
    public static int RingFor(int monthOfYear)
    {
        var rules = GameData.Seasons.Flood;
        var level = rules.MonthLevels[monthOfYear % rules.MonthLevels.Length];
        return (int)Math.Floor(level * rules.MaxRange + 1e-9);
    }

    public bool IsFlooded(int tx, int ty)
    {
        if (Ring == 0 || !_island.InBounds(tx, ty))
        {
            return false;
        }

        var d = _distance[_island.Index(tx, ty)];
        return d > 0 && d != Never && d <= Ring;
    }

    /// <summary>Zatopené dlaždice (čísla), pro klienta.</summary>
    public IEnumerable<int> FloodedTiles()
    {
        for (var i = 0; i < _distance.Length; i++)
        {
            if (Ring > 0 && _distance[i] > 0 && _distance[i] != Never && _distance[i] <= Ring)
            {
                yield return i;
            }
        }
    }

    /// <summary>Vzdálenost (v krocích po dlaždicích i úhlopříčně) souše od bažinné vody, nejvýš maxRange.</summary>
    private static byte[] DistancesFromMarshWater(Island island)
    {
        var distance = new byte[island.Tiles.Length];
        var queue = new Queue<int>();
        for (var i = 0; i < distance.Length; i++)
        {
            var water = island.Tiles[i] is TileType.Bog or TileType.Mire || (island.Tiles[i] == TileType.Pond && island.Regions[i] == RegionKind.Marsh);
            distance[i] = water ? (byte)0 : Never;
            if (water)
            {
                queue.Enqueue(i);
            }
        }

        var maxRange = GameData.Seasons.Flood.MaxRange;
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            var (x, y) = (i % island.Width, i / island.Width);
            var next = distance[i] + 1;
            if (next > maxRange)
            {
                continue;
            }

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (nx, ny) = (x + dx, y + dy);
                    if (!island.InBounds(nx, ny))
                    {
                        continue;
                    }

                    var n = island.Index(nx, ny);
                    if (distance[n] == Never && Floodable(island, n))
                    {
                        distance[n] = (byte)next;
                        queue.Enqueue(n);
                    }
                }
            }
        }

        return distance;
    }

    /// <summary>Zatopit jde nízká souš: tráva, les a písek (ne kamenitá vrchovina, skála ani jiná voda).</summary>
    private static bool Floodable(Island island, int i)
    {
        return island.Tiles[i] is TileType.Grass or TileType.Forest or TileType.Sand;
    }
}

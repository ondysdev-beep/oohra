using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>
/// Nebezpečné okolí, první vlna (návrh 0.8, kap. 5, hazards.json): komáři v bažině a u vody (kousnutí,
/// zimnice), pijavice při chůzi bažinou. Obrana: kouř z ohně, natřít se bahnem nebo popelem, pijavice sundat
/// u ohně nebo popelem. Bahenní vodu (úplavice) řeší pití (Drinking). Druhá vlna: klíšťata v trávě a lese
/// (přisají se potichu, prohlédnout se). Hady počítá Animals, vosí hnízda WaspNests.
/// </summary>
public static class Hazards
{
    /// <summary>Jedna minuta (nebo její část) okolí postavy na místě (x, y).</summary>
    public static void Update(Character c, Island island, Fires fires, double x, double y, GameClock clock, double minutes, Mulberry32 rng)
    {
        if (c.IsDead || minutes <= 0)
        {
            return;
        }

        var now = clock.TotalMinutes;
        Mosquitoes(c, island, fires, x, y, clock, minutes, rng);
        Leeches(c, island, x, y, now, minutes, rng);
        Ticks(c, island, x, y, clock, minutes, rng);
    }

    /// <summary>Prohlédnout se: najde a vytáhne všechna přisátá klíšťata. Vrací jejich počet.</summary>
    public static int Inspect(Character c, double now)
    {
        var count = c.RemoveTicks();
        c.Report(now, count > 0 ? BodyEvent.TicksFound : BodyEvent.NoTicks);
        return count;
    }

    /// <summary>Jak moc tu teď štípou komáři (0 až 1), bez ohledu na ochranu.</summary>
    public static double MosquitoStrength(Island island, double x, double y, GameClock clock)
    {
        var rules = GameData.Hazards.Mosquitoes;
        var season = rules.Seasons[(int)clock.Season];
        if (season <= 0 || !NearMarshWater(island, x, y, rules.NearWater))
        {
            return 0;
        }

        var hour = clock.Hour;
        var twilight = (hour >= rules.Dawn[0] && hour < rules.Dawn[1]) || (hour >= rules.Dusk[0] && hour < rules.Dusk[1]);
        return season * (twilight ? 1 : rules.DayFactor);
    }

    /// <summary>Natřít se bahnem (u bažiny nebo vody) nebo popelem z batohu. Komáři pak chvíli neštípou.</summary>
    public static bool Smear(Character c, Island island, Inventory inventory, double x, double y, bool ash, double now)
    {
        if (ash ? !inventory.TryRemove(ItemKind.Ash, 1) : !NearMarshWater(island, x, y, GameData.Gathering.Reach.Water))
        {
            return false;
        }

        c.SmearedUntil = now + GameData.Hazards.SmearMinutes;
        c.Report(now, BodyEvent.Smeared);
        return true;
    }

    /// <summary>Sundat pijavice: u ohně, nebo popelem z batohu (spotřebuje se).</summary>
    public static bool RemoveLeeches(Character c, Inventory inventory, bool nearFire, double now)
    {
        if (c.Leeches == 0 || (!nearFire && !inventory.TryRemove(ItemKind.Ash, 1)))
        {
            return false;
        }

        c.Leeches = 0;
        c.Report(now, BodyEvent.LeechesRemoved);
        return true;
    }

    private static void Mosquitoes(Character c, Island island, Fires fires, double x, double y, GameClock clock, double minutes, Mulberry32 rng)
    {
        var rules = GameData.Hazards.Mosquitoes;
        var now = clock.TotalMinutes;
        if (now < c.SmearedUntil || fires.BurningNear(island, x, y, rules.SmokeRange))
        {
            return;
        }

        var strength = MosquitoStrength(island, x, y, clock);
        if (strength <= 0 || rng.NextDouble() >= strength * minutes / rules.BiteEveryMinutes)
        {
            return;
        }

        if (now - c.LastMosquitoWarning >= rules.WarnEveryMinutes)
        {
            c.LastMosquitoWarning = now;
            c.Report(now, BodyEvent.MosquitoBite);
        }

        if (rng.NextDouble() < rules.InfectChance)
        {
            Diseases.Infect(c, DiseaseKind.MarshFever, now);
        }
    }

    private static void Leeches(Character c, Island island, double x, double y, double now, double minutes, Mulberry32 rng)
    {
        var rules = GameData.Hazards.Leeches;
        if (c.Leeches > 0)
        {
            c.TakeDamage(c.Leeches * rules.HealthPerLeechPerMinute * minutes, DeathCause.Bleeding);
        }

        var (tx, ty) = ((int)Math.Floor(x), (int)Math.Floor(y));
        if (!island.InBounds(tx, ty) || island.TileAt(tx, ty) != TileType.Bog || c.Leeches >= rules.Max)
        {
            return;
        }

        if (rng.NextDouble() < rules.ChancePerMinute * minutes)
        {
            c.Leeches++;
            c.Report(now, BodyEvent.Leech);
        }
    }

    private static void Ticks(Character c, Island island, double x, double y, GameClock clock, double minutes, Mulberry32 rng)
    {
        var rules = GameData.Hazards.Ticks;
        var now = clock.TotalMinutes;
        for (var i = 0; i < c.Ticks.Count; i++)
        {
            if (now - c.Ticks[i] < rules.TransmitMinutes)
            {
                continue;
            }

            c.MarkTickChecked(i);
            if (rng.NextDouble() < rules.InfectChance)
            {
                Diseases.Infect(c, DiseaseKind.TickFever, now);
            }
        }

        var (tx, ty) = ((int)Math.Floor(x), (int)Math.Floor(y));
        var season = rules.Seasons[(int)clock.Season];
        if (season <= 0 || c.Ticks.Count >= rules.Max || !island.InBounds(tx, ty)
            || island.TileAt(tx, ty) is not (TileType.Grass or TileType.Forest))
        {
            return;
        }

        // Potichu: žádná hláška, hráč na klíště přijde, až když se prohlédne.
        if (rng.NextDouble() < season * rules.ChancePerMinute * minutes)
        {
            c.AddTick(now);
        }
    }

    /// <summary>Bažina, nebo sladká či bahenní voda do distance metrů.</summary>
    private static bool NearMarshWater(Island island, double x, double y, double distance)
    {
        var (cx, cy) = ((int)Math.Floor(x), (int)Math.Floor(y));
        if (island.InBounds(cx, cy) && island.RegionAt(cx, cy) == RegionKind.Marsh)
        {
            return true;
        }

        var r = (int)Math.Ceiling(distance);
        for (var ty = cy - r; ty <= cy + r; ty++)
        {
            for (var tx = cx - r; tx <= cx + r; tx++)
            {
                if (island.InBounds(tx, ty) && island.TileAt(tx, ty) is TileType.Pond or TileType.Bog or TileType.Ford
                    && Math.Sqrt((tx + 0.5 - x) * (tx + 0.5 - x) + (ty + 0.5 - y) * (ty + 0.5 - y)) <= distance)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

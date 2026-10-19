using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Rng;

namespace Oohra.Core.World;

/// <summary>
/// Oheň ve stavbě (návrh 0.8, kap. 6, building.json buildingFire): hořící ohniště blízko dřevěné stavby (nebo pod
/// střechou či podlahou patra) ji může zapálit, v dešti méně. Hořící stavba zapaluje sousední a za burnMinutes
/// shoří (co na ní stálo, pak spadne kvůli statice). Kdo stojí u hořící stavby, pálí se. Ohniště uvnitř kruhu
/// z kamenů (rozhodnuto 19. 10.) nic nezapálí.
/// </summary>
public static class BuildingFire
{
    /// <summary>Krok ohně ve stavbách (minutes herních minut). Vrací stavby, které právě shořely.</summary>
    public static List<Structure> Update(Island island, Fires fires, double minutes, WeatherKind weather, Mulberry32 rng, double now)
    {
        var rules = GameData.Building.BuildingFire;
        var structures = island.Structures;
        var wet = weather is WeatherKind.Rain or WeatherKind.Storm ? rules.RainFactor : 1;
        foreach (var fire in fires.All.Where(f => f.Burning && !InRing(structures, f.X, f.Y)))
        {
            foreach (var s in structures.Near(fire.X, fire.Y, rules.IgniteRange).Where(s => s.BurningSince is null && Wooden(s) && Reaches(s, fire.X, fire.Y, rules.IgniteRange)).ToList())
            {
                if (rng.NextDouble() < rules.IgniteChancePerMinute * wet * minutes)
                {
                    structures.Ignite(s, now);
                }
            }
        }

        var burning = structures.All.Where(s => s.BurningSince is not null).ToList();
        foreach (var source in burning)
        {
            foreach (var s in structures.Near(source.X, source.Y, source.Length + rules.SpreadRange).Where(s => s.BurningSince is null && Wooden(s) && Near(source, s, rules.SpreadRange)).ToList())
            {
                if (rng.NextDouble() < rules.SpreadChancePerMinute * wet * minutes)
                {
                    structures.Ignite(s, now);
                }
            }
        }

        var burnt = burning.Where(s => now - s.BurningSince >= rules.BurnMinutes).ToList();
        foreach (var s in burnt)
        {
            structures.Burn(s);
        }

        return burnt;
    }

    /// <summary>Kdo stojí u hořící stavby (stejné podlaží), pálí se: ztratí zdraví a dostane varování.</summary>
    public static void Heat(Structures structures, Character c, double x, double y, int level, double minutes, double now)
    {
        var rules = GameData.Building.BuildingFire;
        if (c.IsDead || !structures.Near(x, y, rules.HeatRange).Any(s => s.BurningSince is not null && s.Level == level && Reaches(s, x, y, rules.HeatRange)))
        {
            return;
        }

        c.TakeDamage(rules.HeatDamagePerMinute * minutes, DeathCause.Burned);
        if (now - c.LastFireWarning >= rules.WarnEveryMinutes)
        {
            c.LastFireWarning = now;
            c.Report(now, BodyEvent.BuildingOnFire);
        }
    }

    /// <summary>Ohniště stojí uvnitř kruhu z kamenů (ringDecoration): nic nezapálí.</summary>
    public static bool InRing(Structures structures, double x, double y)
    {
        var ring = GameData.Building.BuildingFire.RingDecoration;
        return structures.Near(x, y, 1).Any(s => s.Kind == BuildKind.Decoration && s.DecorationId == ring
            && Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y)) <= (GameData.Decoration(ring)?.Radius ?? 0));
    }

    /// <summary>Co hoří: všechno ze dřeva a klacků, jen kruh z kamenů ne.</summary>
    public static bool Wooden(Structure s)
    {
        return !(s.Kind == BuildKind.Decoration && s.DecorationId == GameData.Building.BuildingFire.RingDecoration);
    }

    /// <summary>Oheň nebo člověk na (x, y) je u stavby: u zdi podél celé délky, u desky pod ní, jinak od středu.</summary>
    private static bool Reaches(Structure s, double x, double y, double range)
    {
        if (s.Kind is BuildKind.Roof or BuildKind.Floor)
        {
            return Roofs.Covers(s, x, y, range * 0.5);
        }

        var (x1, y1, x2, y2) = s.Ends;
        return Geometry.DistanceToSegment(x, y, x1, y1, x2, y2) <= range;
    }

    /// <summary>Dvě stavby jsou si blízko (oheň přeskočí): body jedné do range od druhé.</summary>
    private static bool Near(Structure a, Structure b, double range)
    {
        return Roofs.Footprint(b).Append(new Position(b.X, b.Y)).Any(p => Reaches(a, p.X, p.Y, range))
            || Roofs.Footprint(a).Any(p => Reaches(b, p.X, p.Y, range));
    }
}

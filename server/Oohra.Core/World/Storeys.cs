using Oohra.Core.Characters;
using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>
/// Podlaží (návrh 0.8, kap. 6): postava je v přízemí (0) nebo v patře (1). Nahoru a dolů vede žebřík u okraje
/// podlahy patra, dolů se dá i seskočit (malé zranění). Kdo stojí na podlaze patra, která spadne, spadne s ní.
/// </summary>
public static class Storeys
{
    /// <summary>
    /// Žebřík: z přízemí nahoru na podlahu, u které žebřík stojí, z patra dolů k patě žebříku. Vrací výsledek,
    /// nové podlaží a místo.
    /// </summary>
    public static (ClimbResult Result, int Level, Position To) Climb(Island island, int ladderId, int level, double x, double y)
    {
        var rules = GameData.Building.Ladder;
        var ladder = island.Structures.Find(ladderId);
        if (ladder is null || ladder.Kind != BuildKind.Ladder)
        {
            return (ClimbResult.NotFound, level, new Position(x, y));
        }

        if (Math.Sqrt((ladder.X - x) * (ladder.X - x) + (ladder.Y - y) * (ladder.Y - y)) > rules.Reach)
        {
            return (ClimbResult.TooFar, level, new Position(x, y));
        }

        if (level > 0)
        {
            return (ClimbResult.Down, 0, new Position(ladder.X, ladder.Y));
        }

        var floor = FloorAt(island.Structures, ladder.X, ladder.Y, rules.FloorMargin);
        if (floor is null)
        {
            return (ClimbResult.NotFound, level, new Position(x, y));
        }

        var top = Inside(floor, ladder.X, ladder.Y, GameData.Movement.Radius + 0.15);
        if (!Movement.CanStand(island, top.X, top.Y, GameData.Movement.Radius, 1))
        {
            top = new Position(floor.X, floor.Y);
        }

        return (ClimbResult.Up, 1, top);
    }

    /// <summary>Seskočit z patra: postava je dole, trochu se zraní.</summary>
    public static void JumpDown(Character c, double now)
    {
        c.TakeDamage(GameData.Building.Storey.JumpDamage, DeathCause.Fall);
        c.Report(now, BodyEvent.JumpedDown);
    }

    /// <summary>Spadl s podlahou patra.</summary>
    public static void Fall(Character c, double now)
    {
        c.TakeDamage(GameData.Building.Storey.FallDamage, DeathCause.Fall);
        c.Report(now, BodyEvent.Fell);
    }

    /// <summary>Podlaha patra, u které (nebo nad kterou) je místo (x, y), do margin metrů za okrajem.</summary>
    public static Structure? FloorAt(Structures structures, double x, double y, double margin)
    {
        return structures.All.FirstOrDefault(s => s.Kind == BuildKind.Floor && s.Level > 0 && Roofs.Covers(s, x, y, margin));
    }

    /// <summary>Nejbližší místo uvnitř čtverce, inset metrů od okraje.</summary>
    private static Position Inside(Structure square, double x, double y, double inset)
    {
        var half = square.Length / 2 - inset;
        var (dx, dy) = (x - square.X, y - square.Y);
        var (c, s) = (Math.Cos(square.Angle), Math.Sin(square.Angle));
        var along = Math.Clamp(dx * c + dy * s, -half, half);
        var across = Math.Clamp(-dx * s + dy * c, -half, half);
        return new Position(square.X + along * c - across * s, square.Y + along * s + across * c);
    }
}

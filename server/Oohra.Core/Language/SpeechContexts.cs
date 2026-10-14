using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Language;

/// <summary>
/// V jaké situaci je mluvčí, když něco řekne (prototyp CTXT). Posluchač z toho může odhadnout význam slova.
/// Výsledek jsou bity SpeechContext.
/// </summary>
public static class SpeechContexts
{
    /// <param name="recentGesture">Gesto, které mluvčí udělal před chvílí (kývnutí, vrtění hlavou), nebo null.</param>
    public static int Of(Island island, Fires fires, Animals animals, Character speaker, double x, double y, double now, Gesture? recentGesture)
    {
        var rules = GameData.Language.Context;
        var bits = 0;
        if (now - speaker.LastAte <= rules.EatingMinutes)
        {
            bits |= Bit(SpeechContext.Eating);
        }

        if (WaterNear(island, x, y, rules.WaterDistance))
        {
            bits |= Bit(SpeechContext.AtWater);
        }

        if (animals.All.Any(a => !a.IsDead && Math.Sqrt((a.X - x) * (a.X - x) + (a.Y - y) * (a.Y - y)) <= rules.AnimalDistance))
        {
            bits |= Bit(SpeechContext.AnimalNear);
        }

        if (fires.BurningNear(island, x, y, rules.FireDistance))
        {
            bits |= Bit(SpeechContext.AtFire);
        }

        // Gesto těsně před slovem dá slovu souvislost (návrh 9.3: řekne slovo a ukáže předmět nebo udělá akci).
        // Ukázaná věc (ShowItem) je tu jen, když to bylo jídlo, server to tak předá.
        bits |= recentGesture switch
        {
            Gesture.Yes => Bit(SpeechContext.Nodding),
            Gesture.No => Bit(SpeechContext.ShakingHead),
            Gesture.Eat or Gesture.ShowItem => Bit(SpeechContext.Eating),
            Gesture.Drink => Bit(SpeechContext.AtWater),
            Gesture.Beware => Bit(SpeechContext.AnimalNear),
            _ => 0,
        };

        return bits;
    }

    public static int Bit(SpeechContext context)
    {
        return 1 << (int)context;
    }

    /// <summary>Je do vzdálenosti distance (od středu dlaždice) voda: jezírko, bažina nebo moře?</summary>
    private static bool WaterNear(Island island, double x, double y, double distance)
    {
        var reach = (int)Math.Ceiling(distance);
        var (cx, cy) = ((int)Math.Floor(x), (int)Math.Floor(y));
        for (var ty = cy - reach; ty <= cy + reach; ty++)
        {
            for (var tx = cx - reach; tx <= cx + reach; tx++)
            {
                var (dx, dy) = (tx + 0.5 - x, ty + 0.5 - y);
                if (island.InBounds(tx, ty) && Drinking.IsWater(island.TileAt(tx, ty)) && dx * dx + dy * dy <= distance * distance)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

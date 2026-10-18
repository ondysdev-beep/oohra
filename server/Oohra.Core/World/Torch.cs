using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;

namespace Oohra.Core.World;

/// <summary>
/// Pochodeň: klacek zapálený u hořícího ohně. Svítí kolem postavy (vidí ji i ostatní) a vlk se jí bojí jako ohně.
/// Hoří torchMinutes herních minut, v dešti a bouřce rychleji (weather.json).
/// </summary>
public static class Torch
{
    /// <summary>Zapálí pochodeň od hořícího ohně poblíž (stačí blízko jako na pečení). Spotřebuje jeden klacek.</summary>
    public static FireResult Light(Island island, Fires fires, Character c, Inventory inventory, double x, double y)
    {
        if (c.TorchLit)
        {
            return FireResult.TorchAlreadyLit;
        }

        if (!fires.BurningNear(island, x, y, GameData.Fire.Fire.CookRadius))
        {
            return FireResult.NoFireNear;
        }

        if (!inventory.TryRemove(ItemKind.Stick, 1))
        {
            return FireResult.NoStick;
        }

        c.TorchMinutes = GameData.Weather.TorchMinutes;
        c.TorchWarned = false;
        return FireResult.TorchLit;
    }

    /// <summary>Pochodeň dohořívá. Před koncem hra jednou upozorní, na konci hlásí, že zhasla.</summary>
    public static void Burn(Character c, double now, double minutes, WeatherState weather)
    {
        if (!c.TorchLit)
        {
            return;
        }

        c.TorchMinutes = Math.Max(0, c.TorchMinutes - minutes * Weather.BurnMultiplier(weather));
        if (!c.TorchLit)
        {
            c.Report(now, BodyEvent.TorchOut);
        }
        else if (!c.TorchWarned && c.TorchMinutes <= GameData.Weather.TorchWarnMinutes)
        {
            c.TorchWarned = true;
            c.Report(now, BodyEvent.TorchLow);
        }
    }
}

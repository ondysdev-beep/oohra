using Oohra.Core.Characters;
using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>
/// Pohyb po ostrově a kolize (prototyp blockedTile, canStand, terrainSpeed a movePlayer v part4.js).
/// Rozdíl proti prototypu: strom neblokuje celou dlaždici, jen kmen (treeTrunkRadius v movement.json).
/// Souřadnice jsou v dlaždicích, střed dlaždice (x, y) je (x + 0,5; y + 0,5).
/// </summary>
public static class Movement
{
    /// <summary>
    /// Obsazená dlaždice: mimo mapu, moře, jezírko, strom. Nikdo se tam nenarodí.
    /// Pro chůzi platí CanStand: strom tam blokuje jen svůj kmen.
    /// </summary>
    public static bool IsBlocked(Island island, int tx, int ty)
    {
        return IsImpassable(island, tx, ty) || island.HasTrunk(island.Index(tx, ty));
    }

    /// <summary>
    /// Postava o daném poloměru se na místo vejde: žádný ze čtyř rohů není ve vodě ani mimo mapu
    /// a postava nenaráží do kmene stromu ani do zdi. Rostliny nepřekáží.
    /// </summary>
    public static bool CanStand(Island island, double x, double y, double radius, int level = 0)
    {
        if (level > 0)
        {
            // V patře: všechny čtyři rohy na podlaze patra a nic v cestě ve stejném podlaží.
            return Roofs.StandsOn(island.Structures, x - radius, y - radius, level) && Roofs.StandsOn(island.Structures, x + radius, y - radius, level)
                && Roofs.StandsOn(island.Structures, x - radius, y + radius, level) && Roofs.StandsOn(island.Structures, x + radius, y + radius, level)
                && !island.Structures.Blocks(x, y, radius, level);
        }

        return !IsImpassable(island, Floor(x - radius), Floor(y - radius))
            && !IsImpassable(island, Floor(x + radius), Floor(y - radius))
            && !IsImpassable(island, Floor(x - radius), Floor(y + radius))
            && !IsImpassable(island, Floor(x + radius), Floor(y + radius))
            && !HitsTrunk(island, x, y, radius)
            && !island.Structures.Blocks(x, y, radius)
            && !island.Rocks.Blocks(island, x, y, radius)
            && !island.Forestry.Blocks(x, y, radius);
    }

    /// <summary>Dlaždice, na kterou nejde vůbec vstoupit: mimo mapu, moře, jezírko, skála kolem jeskyně.</summary>
    private static bool IsImpassable(Island island, int tx, int ty)
    {
        if (!island.InBounds(tx, ty))
        {
            return true;
        }

        var tile = island.TileAt(tx, ty);
        return IsWaterTile(tile) || tile == TileType.CaveWall;
    }

    /// <summary>Voda, do které se nedá vstoupit (moře, jezírko, jezero, řeka). Brod se přejít dá.</summary>
    public static bool IsWaterTile(TileType tile)
    {
        return tile == TileType.Sea || tile == TileType.Pond;
    }

    /// <summary>Postava by se překrývala s kmenem stromu (kmen stojí kdekoli ve své dlaždici, Island.TrunkPosition).</summary>
    private static bool HitsTrunk(Island island, double x, double y, double radius)
    {
        var reach = radius + GameData.Movement.TreeTrunkRadius;
        var tx = Floor(x);
        var ty = Floor(y);
        for (var yy = ty - 1; yy <= ty + 1; yy++)
        {
            for (var xx = tx - 1; xx <= tx + 1; xx++)
            {
                if (!island.InBounds(xx, yy) || !island.HasTrunk(island.Index(xx, yy)))
                {
                    continue;
                }

                var (trunkX, trunkY) = island.TrunkPosition(island.Index(xx, yy));
                var dx = trunkX - x;
                var dy = trunkY - y;
                if (dx * dx + dy * dy < reach * reach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsWalkable(Island island, double x, double y, double radius)
    {
        return !IsBlocked(island, Floor(x), Floor(y)) && CanStand(island, x, y, radius);
    }

    /// <summary>Násobek rychlosti podle dlaždice pod postavou (bažina zpomalí).</summary>
    public static double TerrainSpeed(Island island, double x, double y)
    {
        var tx = Floor(x);
        var ty = Floor(y);
        if (!island.InBounds(tx, ty))
        {
            return 1;
        }

        // Na jaře zatopená souš se brodí jako bažina.
        var tile = island.Flood.IsFlooded(tx, ty) ? TileType.Bog : island.TileAt(tx, ty);
        return GameData.Movement.TerrainSpeed.TryGetValue(tile, out var speed) ? speed : 1;
    }

    /// <summary>S kladem v náruči jde postava pomaleji (carrySpeed ve woodcutting.json).</summary>
    public static double CarryMultiplier(Items.Inventory inventory)
    {
        return inventory.Count(Items.ItemKind.LogPiece) > 0 ? GameData.Wood.CarrySpeed : 1;
    }

    /// <summary>Násobek rychlosti podle stavu těla a generace (otrava a horečka zpomalí, povzbuzení zrychlí).</summary>
    public static double BodySpeed(Character c)
    {
        var rules = GameData.Movement.Body;
        var m = 1.0;
        m -= Math.Clamp((c.Toxin - rules.ToxinAbove) / rules.ToxinScale, 0, rules.ToxinMaxSlowdown);
        m -= Math.Clamp((c.Fever - rules.FeverAbove) / rules.FeverScale, 0, rules.FeverMaxSlowdown);
        m -= Math.Clamp(c.Limp / 100 * rules.LimpMaxSlowdown, 0, rules.LimpMaxSlowdown);
        if (c.Energy < rules.TiredEnergyBelow)
        {
            m -= rules.TiredSlowdown;
        }

        if (c.StimulantMinutes > 0)
        {
            m += rules.StimulantBonus;
        }

        return Math.Clamp(m * c.Traits.SpeedMultiplier, rules.MinMultiplier, rules.MaxMultiplier);
    }

    /// <summary>Úhly (ve stupních), o které se postava zkusí odklonit, když jí něco stojí přímo v cestě.</summary>
    private static readonly double[] SteerDegrees = [15, -15, 30, -30, 45, -45, 60, -60];

    /// <summary>
    /// Posune postavu směrem (dirX, dirY) o vzdálenost.
    /// Když je cesta přímo blokovaná, zkusí se mírně odklonit (obejde kmen, projde mezi stromy).
    /// Když ani to nejde, osy se zkoušejí zvlášť a postava podél překážky klouže (jako v prototypu).
    /// Kolmo do zdi nebo vody se postava zastaví.
    /// </summary>
    public static (double X, double Y) Step(Island island, double x, double y, double dirX, double dirY, double distance, int level = 0)
    {
        var length = Math.Sqrt(dirX * dirX + dirY * dirY);
        if (length <= 0)
        {
            return (x, y);
        }

        var radius = GameData.Movement.Radius;
        var ux = dirX / length;
        var uy = dirY / length;
        foreach (var degrees in SteerDegrees.Prepend(0))
        {
            var angle = degrees * Math.PI / 180;
            var sx = ux * Math.Cos(angle) - uy * Math.Sin(angle);
            var sy = ux * Math.Sin(angle) + uy * Math.Cos(angle);
            if (CanStand(island, x + sx * distance, y + sy * distance, radius, level))
            {
                return (x + sx * distance, y + sy * distance);
            }
        }

        if (level == 0 && TrunkSlide(island, x, y, ux, uy, radius) is var (tx, ty) && CanStand(island, x + tx * distance, y + ty * distance, radius))
        {
            return (x + tx * distance, y + ty * distance);
        }

        var dx = ux * distance;
        var dy = uy * distance;
        if (CanStand(island, x + dx, y, radius, level))
        {
            x += dx;
        }

        if (CanStand(island, x, y + dy, radius, level))
        {
            y += dy;
        }

        return (x, y);
    }

    /// <summary>
    /// Směr podél obvodu kmene, o který postava zrovna opírá (aby kmen obešla i při čelním nárazu).
    /// Null, když žádný kmen v cestě není.
    /// </summary>
    private static (double X, double Y)? TrunkSlide(Island island, double x, double y, double ux, double uy, double radius)
    {
        var reach = radius + GameData.Movement.TreeTrunkRadius + 0.05;
        var tx = Floor(x);
        var ty = Floor(y);
        for (var yy = ty - 1; yy <= ty + 1; yy++)
        {
            for (var xx = tx - 1; xx <= tx + 1; xx++)
            {
                if (!island.InBounds(xx, yy) || !island.HasTrunk(island.Index(xx, yy)))
                {
                    continue;
                }

                // n: od středu kmene k postavě. Když postava jde proti kmeni (u · n < 0), jde se po tečně.
                var (trunkX, trunkY) = island.TrunkPosition(island.Index(xx, yy));
                var nx = x - trunkX;
                var ny = y - trunkY;
                var d = Math.Sqrt(nx * nx + ny * ny);
                if (d > reach || d <= 0)
                {
                    continue;
                }

                nx /= d;
                ny /= d;
                if (ux * nx + uy * ny >= 0)
                {
                    continue;
                }

                // Tečna na tu stranu, kam postava míří víc. Při úplně čelním nárazu vždy stejná strana.
                var side = nx * uy - ny * ux >= 0 ? 1 : -1;
                return (-ny * side, nx * side);
            }
        }

        return null;
    }

    /// <summary>Kolik dlaždic postava ujde za danou dobu (sekundy reálného času).</summary>
    public static double Distance(Island island, Character c, double x, double y, double seconds)
    {
        return GameData.Movement.WalkSpeed * BodySpeed(c) * TerrainSpeed(island, x, y) * seconds;
    }

    private static int Floor(double value)
    {
        return (int)Math.Floor(value);
    }
}

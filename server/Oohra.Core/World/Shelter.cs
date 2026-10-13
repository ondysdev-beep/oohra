using Oohra.Core.Data;

namespace Oohra.Core.World;

/// <summary>
/// Úkryt: postava je v prostoru uzavřeném zdmi. Z postavy se vyšlou paprsky do všech stran
/// (rays v building.json) a každý musí do maxDistance narazit na zeď. Stromy a voda se jako stěna
/// nepočítají, jen zdi, sloupky zdi s otvorem a dveře (jako v prototypu sheltered, jen bez dlaždic,
/// protože zdi mají libovolný úhel). Otvor bez dveří úkryt neuzavře.
/// Mezeru užší, než je rozestup paprsků, úkryt nepozná. Postava jí stejně neprojde.
/// </summary>
public static class Shelter
{
    public static bool IsSheltered(Island island, double x, double y)
    {
        var rules = GameData.Building.Shelter;
        var walls = island.Structures.Near(x, y, rules.MaxDistance).ToList();
        if (walls.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < rules.Rays; i++)
        {
            var angle = 2 * Math.PI * i / rules.Rays;
            if (!RayHitsWall(walls, x, y, Math.Cos(angle), Math.Sin(angle), rules.MaxDistance))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RayHitsWall(List<Structure> walls, double x, double y, double dx, double dy, double maxDistance)
    {
        foreach (var wall in walls)
        {
            foreach (var s in wall.ShelterSegments())
            {
                var hit = Geometry.RayHitsSegment(x, y, dx, dy, s.X1, s.Y1, s.X2, s.Y2);
                if (hit is { } distance && distance <= maxDistance)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

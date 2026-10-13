using Oohra.Core.Data;
using Oohra.Core.Items;

namespace Oohra.Core.World;

/// <summary>
/// Zdi z klacků postavené hráči (prototyp S.walls, buildWall a nabídka Zeď v part5.js).
/// Zeď zabere celou dlaždici a nedá se projít. Mění se během hry, proto není v mapě dlaždic.
/// </summary>
public sealed class Walls
{
    private readonly HashSet<int> _tiles = [];

    /// <summary>Čísla dlaždic se zdí (y * šířka + x).</summary>
    public IReadOnlyCollection<int> Tiles => _tiles;

    /// <summary>Zvýší se při každé změně. Server pak pošle seznam zdí.</summary>
    public int Version { get; private set; }

    public bool Has(int tileIndex)
    {
        return _tiles.Contains(tileIndex);
    }

    /// <summary>Postaví zeď bez klacků a kontrol (pro testy a později pro načtení uloženého světa).</summary>
    public void Place(int tileIndex)
    {
        if (_tiles.Add(tileIndex))
        {
            Version++;
        }
    }

    /// <summary>
    /// Postaví zeď z klacků na dlaždici, kam se postava dívá. Nejde na vodu, strom, jinou zeď, oheň
    /// ani tam, kde stojí nějaký hráč (players: polohy všech hráčů včetně stavitele).
    /// </summary>
    public BuildResult Build(
        Island island, Fires fires, Inventory inventory, double playerX, double playerY, double facing, IEnumerable<Position> players)
    {
        var rules = GameData.Building.Wall;
        if (inventory.Count(ItemKind.Stick) < rules.SticksCost)
        {
            return BuildResult.NeedSticks;
        }

        var (tileX, tileY) = Fires.FacingTile(playerX, playerY, facing);
        if (Movement.IsBlocked(island, tileX, tileY)
            || fires.All.Any(f => f.TileX == tileX && f.TileY == tileY)
            || players.Any(p => Overlaps(p, tileX, tileY)))
        {
            return BuildResult.CannotBuildHere;
        }

        inventory.TryRemove(ItemKind.Stick, rules.SticksCost);
        Place(island.Index(tileX, tileY));
        return BuildResult.Built;
    }

    /// <summary>Rozebere zeď na dlaždici. Část klacků se vrátí do batohu (když se tam vejdou).</summary>
    public BuildResult Remove(Island island, int tileX, int tileY, Inventory inventory, double playerX, double playerY)
    {
        if (!island.InBounds(tileX, tileY) || !Has(island.Index(tileX, tileY)))
        {
            return BuildResult.NoWall;
        }

        var rules = GameData.Building.Wall;
        var dx = tileX + 0.5 - playerX;
        var dy = tileY + 0.5 - playerY;
        if (Math.Sqrt(dx * dx + dy * dy) > rules.Reach)
        {
            return BuildResult.TooFar;
        }

        _tiles.Remove(island.Index(tileX, tileY));
        Version++;
        for (var i = 0; i < rules.SticksBack; i++)
        {
            inventory.TryAdd(Item.Stick);
        }

        return BuildResult.Removed;
    }

    /// <summary>Postava (kruh o poloměru z movement.json) zasahuje do dlaždice.</summary>
    private static bool Overlaps(Position player, int tileX, int tileY)
    {
        var nearestX = Math.Clamp(player.X, tileX, tileX + 1);
        var nearestY = Math.Clamp(player.Y, tileY, tileY + 1);
        var dx = player.X - nearestX;
        var dy = player.Y - nearestY;
        var radius = GameData.Movement.Radius;
        return dx * dx + dy * dy < radius * radius;
    }
}

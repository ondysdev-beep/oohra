using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.Time;

namespace Oohra.Core.World;

/// <summary>
/// Klacky ze stromů a kameny ze skal (prototyp nabídky Strom a Skála v part5.js).
/// Strom dá několikrát za den spadlé větve, pak je potřeba počkat do zítřka. Kameny jsou v hromadách na skalách.
/// </summary>
public sealed class Materials
{
    private readonly Dictionary<int, TreeSticks> _trees = [];

    /// <summary>Nasbírá u stromu na dlaždici (tileX, tileY) 1 nebo 2 klacky.</summary>
    public MaterialResult GatherSticks(
        Island island, int tileX, int tileY, GameClock clock, Inventory inventory, Mulberry32 rng, double playerX, double playerY)
    {
        if (!island.InBounds(tileX, tileY) || !island.HasTree(island.Index(tileX, tileY)))
        {
            return MaterialResult.WrongPlace;
        }

        var rules = GameData.Fire;
        if (!InReach(tileX, tileY, playerX, playerY, rules.Reach.Tree))
        {
            return MaterialResult.TooFar;
        }

        var tree = TreeAt(island.Index(tileX, tileY), clock.TotalMinutes, rules.Sticks);
        if (tree.Left <= 0)
        {
            return MaterialResult.NothingLeft;
        }

        if (!inventory.TryAdd(Item.Stick))
        {
            return MaterialResult.InventoryFull;
        }

        tree.Left--;
        if (rng.NextDouble() < rules.Sticks.ExtraStickChance)
        {
            inventory.TryAdd(Item.Stick);
        }

        return MaterialResult.GotSticks;
    }

    /// <summary>
    /// Vezme kámen z hromady na skalní dlaždici (tileX, tileY). Hromada se tím zmenší, prázdná zmizí
    /// a za čas se obnoví (RockPiles). Skála bez hromady kámen nedá.
    /// </summary>
    public static MaterialResult TakeStone(Island island, int tileX, int tileY, Inventory inventory, double playerX, double playerY, double now)
    {
        if (!island.InBounds(tileX, tileY) || island.TileAt(tileX, tileY) is not (TileType.Rock or TileType.LayeredRock))
        {
            return MaterialResult.WrongPlace;
        }

        if (!InReach(tileX, tileY, playerX, playerY, GameData.Fire.Reach.Rock))
        {
            return MaterialResult.TooFar;
        }

        var pile = island.Rocks.At(island.Index(tileX, tileY));
        if (pile is null || pile.Stones <= 0)
        {
            return MaterialResult.NothingLeft;
        }

        if (!inventory.TryAdd(Item.Stone))
        {
            return MaterialResult.InventoryFull;
        }

        island.Rocks.TakeOne(pile.TileIndex, now);
        return MaterialResult.GotStone;
    }

    /// <summary>Stav stromu. Po prvním sběru se za den větve obnoví (prototyp c.c a c.r).</summary>
    private TreeSticks TreeAt(int tileIndex, double now, StickRules rules)
    {
        if (!_trees.TryGetValue(tileIndex, out var tree) || now > tree.RefreshAt)
        {
            tree = new TreeSticks { Left = rules.PickingsPerTree, RefreshAt = now + rules.RefreshMinutes };
            _trees[tileIndex] = tree;
        }

        return tree;
    }

    private static bool InReach(int tileX, int tileY, double playerX, double playerY, double reach)
    {
        var dx = tileX + 0.5 - playerX;
        var dy = tileY + 0.5 - playerY;
        return Math.Sqrt(dx * dx + dy * dy) <= reach;
    }

    private sealed class TreeSticks
    {
        public int Left { get; set; }

        public double RefreshAt { get; set; }
    }
}

using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Persistence;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Kácení a dřevo (návrh 0.8, kap. 2): sekera, seknutí, pád stromu, kmen, klady, dřevo, třísky, nošení.</summary>
public class ForestryTests
{
    [Fact]
    public void AxeIsMadeFromStickAndStones()
    {
        var c = new Character(0, new Mulberry32(1));
        var inventory = new Inventory(10);
        var place = new CraftPlace(false, false, 0);

        Assert.Equal(CraftResult.NeedMaterials, Crafting.Make("axe", inventory, c, place));
        Give(inventory, Item.Stick, 1);
        Give(inventory, Item.Stone, 2);
        Assert.Equal(CraftResult.Made, Crafting.Make("axe", inventory, c, place));

        var axe = Assert.Single(inventory.Stacks).Item;
        Assert.Equal(ItemKind.Axe, axe.Kind);
        Assert.Equal(GameData.Crafting.Find("axe")!.Uses, axe.Uses);
        Assert.Equal(CraftResult.AlreadyHave, Crafting.Make("axe", inventory, c, place));
    }

    [Fact]
    public void TreeNeedsAxeReachAndRhythm()
    {
        var island = World();
        var tree = OpenTree(island);
        var forestry = island.Forestry;
        var bare = new Inventory(10);
        var (px, py) = (tree.X - 1, tree.Y);

        Assert.Equal(WoodResult.NeedAxe, forestry.Chop(island, Tx(tree), Ty(tree), 1, px, py, bare));
        var inventory = WithAxe();
        Assert.Equal(WoodResult.TooFar, forestry.Chop(island, Tx(tree), Ty(tree), 1, px - 3, py, inventory));
        Assert.Equal(WoodResult.Chopped, forestry.Chop(island, Tx(tree), Ty(tree), 1, px, py, inventory));
        Assert.Equal(WoodResult.TooSoon, forestry.Chop(island, Tx(tree), Ty(tree), 1, px, py, inventory));
        Assert.Equal(1, forestry.HitsOf(tree.TileIndex));
        Assert.Equal(499, inventory.Stacks[0].Item.Uses);
    }

    /// <summary>
    /// Strom padne na stranu od toho, kdo sekal: kdo stojí za ním v pruhu pádu, je zraněný, kdo sekal, ne.
    /// Zbude pařez a ležící kmen (celé metry nad pařezem), který blokuje cestu.
    /// </summary>
    [Fact]
    public void FellingDropsTrunkAwayFromChopperAndHurtsWhoIsUnder()
    {
        var island = World();
        var tree = OpenTree(island);
        var forestry = island.Forestry;
        var chopper = new AnimalTarget(1, tree.X - 1, tree.Y, Person());
        var victim = new AnimalTarget(2, tree.X + 2, tree.Y, Person());
        var animals = new Animals(1);
        var rabbit = animals.Place(AnimalKind.Rabbit, tree.X + 3, tree.Y);

        Fell(island, tree, chopper, animals, [chopper, victim]);

        Assert.False(island.HasTree(tree.TileIndex));
        Assert.True(island.HasTrunk(tree.TileIndex), "Pařez dál překáží.");
        Assert.Contains(forestry.TreeStates(), s => s.TileIndex == tree.TileIndex && s.State == TreeState.Stump);
        var trunk = Assert.Single(forestry.Timbers);
        Assert.Equal(TimberKind.Trunk, trunk.Kind);
        Assert.Equal(tree.Kind, trunk.Tree);
        Assert.Equal((int)Math.Floor(tree.Height - GameData.Wood.StumpHeight), trunk.Length);
        Assert.True(trunk.X > tree.X + 0.5, "Kmen leží na straně od sekáče.");
        Assert.Contains(victim.Character.TakeMessages(), m => m.Event == BodyEvent.HitByTree);
        Assert.DoesNotContain(chopper.Character.TakeMessages(), m => m.Event == BodyEvent.HitByTree);
        Assert.True(victim.Character.Health < 100);
        Assert.True(rabbit.IsDead);
        Assert.False(Movement.CanStand(island, trunk.X, trunk.Y, 0.28), "Ležící kmen blokuje cestu.");
    }

    /// <summary>Kmen se rozseká na klady, klad na díly dřeva (podle druhu), díl dřeva na třísky.</summary>
    [Fact]
    public void TrunkIsCutIntoPiecesWoodAndSplinters()
    {
        var island = World();
        var tree = OpenTree(island);
        var forestry = island.Forestry;
        var chopper = new AnimalTarget(1, tree.X - 1, tree.Y, Person());
        Fell(island, tree, chopper, new Animals(1), [chopper]);
        var trunk = forestry.Timbers[0];
        var length = trunk.Length;
        var inventory = WithAxe(capacity: 13);

        for (var i = 0; i < length; i++)
        {
            Swing(island);
            Assert.Equal(WoodResult.PieceCut, forestry.ChopTimber(trunk.Id, 1, trunk.X, trunk.Y, inventory));
        }

        Assert.Equal(length, forestry.Timbers.Count);
        Assert.All(forestry.Timbers, t => Assert.Equal(TimberKind.Piece, t.Kind));

        var piece = forestry.Timbers[0];
        Swing(island);
        Assert.Equal(WoodResult.PieceSplit, forestry.ChopTimber(piece.Id, 1, piece.X, piece.Y, inventory));
        Assert.Equal(GameData.Trees.For(tree.Kind).WoodPerPiece, inventory.Count(ItemKind.Wood));

        var wood = inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == ItemKind.Wood);
        Assert.Equal(WoodResult.WoodSplit, Forestry.SplitWood(inventory, wood));
        Assert.Equal(GameData.Wood.SplintersPerWood, inventory.Count(ItemKind.Splinter));
    }

    [Fact]
    public void OnlyOnePieceIsCarriedAndCanBeDropped()
    {
        var island = World();
        var tree = OpenTree(island);
        var forestry = island.Forestry;
        var chopper = new AnimalTarget(1, tree.X - 1, tree.Y, Person());
        Fell(island, tree, chopper, new Animals(1), [chopper]);
        var trunk = forestry.Timbers[0];
        var inventory = WithAxe();
        Assert.Equal(WoodResult.TooHeavy, forestry.PickUp(trunk.Id, trunk.X, trunk.Y, inventory));
        Swing(island);
        forestry.ChopTimber(trunk.Id, 1, trunk.X, trunk.Y, inventory);
        Swing(island);
        forestry.ChopTimber(trunk.Id, 1, trunk.X, trunk.Y, inventory);
        var pieces = forestry.Timbers.Where(t => t.Kind == TimberKind.Piece).ToList();

        Assert.Equal(WoodResult.PickedUp, forestry.PickUp(pieces[0].Id, pieces[0].X, pieces[0].Y, inventory));
        Assert.Equal(WoodResult.AlreadyCarrying, forestry.PickUp(pieces[1].Id, pieces[1].X, pieces[1].Y, inventory));
        Assert.Equal(tree.Kind, inventory.Stacks.Single(s => s.Item.Kind == ItemKind.LogPiece).Item.Tree);
        Assert.Equal(WoodResult.Dropped, forestry.Drop(island, inventory, tree.X - 2, tree.Y, 0));
        Assert.Equal(0, inventory.Count(ItemKind.LogPiece));
        Assert.Equal(WoodResult.NotCarrying, forestry.Drop(island, inventory, tree.X - 2, tree.Y, 0));
    }

    [Fact]
    public void WornAxeBreaks()
    {
        var island = World();
        var tree = OpenTree(island);
        var inventory = new Inventory(10);
        inventory.TryAdd(new Item(ItemKind.Axe, Uses: 1));

        Assert.Equal(WoodResult.AxeBroke, island.Forestry.Chop(island, Tx(tree), Ty(tree), 1, tree.X - 1, tree.Y, inventory));
        Assert.Equal(0, inventory.Count(ItemKind.Axe));
        Assert.Equal(1, island.Forestry.HitsOf(tree.TileIndex));
    }

    [Fact]
    public void SplinterLightsTorch()
    {
        var island = World();
        var camp = island.Camps[0];
        var (x, y) = (camp.X + 0.5, camp.Y + 0.5);
        var c = Person();
        var inventory = new Inventory(10);
        inventory.TryAdd(Item.Splinter);

        Assert.Equal(FireResult.TorchLit, Torch.Light(island, new Fires(), c, inventory, x, y));
        Assert.Equal(0, inventory.Count(ItemKind.Splinter));
    }

    [Fact]
    public void ForestryIsSavedAndRestored()
    {
        var island = World();
        var tree = OpenTree(island);
        var chopper = new AnimalTarget(1, tree.X - 1, tree.Y, Person());
        Fell(island, tree, chopper, new Animals(1), [chopper]);
        var other = island.Trees.First(t => t.TileIndex != tree.TileIndex && island.HasTree(t.TileIndex));
        var inventory = WithAxe();
        Swing(island);
        island.Forestry.Chop(island, Tx(other), Ty(other), 1, other.X - 1, other.Y, inventory);

        var save = island.Forestry.Export();
        var again = World();
        again.Forestry.Restore(again, save);

        Assert.False(again.HasTree(tree.TileIndex));
        Assert.Equal(1, again.Forestry.HitsOf(other.TileIndex));
        Assert.Equal(island.Forestry.Timbers.Select(t => (t.Id, t.Kind, t.X, t.Length)), again.Forestry.Timbers.Select(t => (t.Id, t.Kind, t.X, t.Length)));
    }

    /// <summary>Za pokácený strom vyroste kousek od pařezu mladý strom stejného druhu, za dva herní roky je z něj strom.</summary>
    [Fact]
    public void FelledTreeIsReplacedBySaplingThatGrowsUp()
    {
        var island = World();
        var tree = OpenTree(island);
        var chopper = new AnimalTarget(1, tree.X - 1, tree.Y, Person());
        Fell(island, tree, chopper, new Animals(1), [chopper]);

        var sapling = Assert.Single(island.Forestry.Saplings());
        var (sx, sy) = (sapling.TileIndex % island.Width + 0.5, sapling.TileIndex / island.Width + 0.5);
        var distance = Math.Sqrt((sx - tree.X) * (sx - tree.X) + (sy - tree.Y) * (sy - tree.Y));
        var rules = GameData.Trees.Regrowth;
        Assert.Equal(tree.Kind, sapling.Kind);
        Assert.InRange(distance, rules.MinDistance - 1, rules.MaxDistance + 1);
        Assert.False(island.HasTree(sapling.TileIndex), "Mladý strom se ještě nekácí.");
        Assert.False(island.HasTrunk(sapling.TileIndex), "Malý stromek nepřekáží.");

        var clock = TestPlants.Clock();
        // Herní rok ve skutečných sekundách (měsíc má v TestPlants.Clock 32 400 s).
        var year = 32400.0 * GameData.Settings.MonthsPerYear;
        clock.Advance(year * rules.GrowYears * rules.BlocksFrom + 60);
        island.Forestry.Update(island, new Animals(1), [], 0.1, clock);
        Assert.True(island.HasTrunk(sapling.TileIndex), "Povyrostlý mladý strom překáží.");
        Assert.Contains(island.Forestry.TreeStates(), t => t.TileIndex == sapling.TileIndex && t.State == TreeState.Sapling && t.Growth >= rules.BlocksFrom);

        clock.Advance(year * rules.GrowYears);
        island.Forestry.Update(island, new Animals(1), [], 0.1, clock);
        Assert.Empty(island.Forestry.Saplings());
        Assert.True(island.HasTree(sapling.TileIndex));
        Assert.Equal(tree.Kind, island.TreeAt(sapling.TileIndex)!.Value.Kind);
        Assert.Contains(island.Forestry.TreeStates(), t => t.TileIndex == sapling.TileIndex && t.State == TreeState.Grown);

        // Dorostlý strom se uloží a jde zase kácet.
        var again = World();
        again.Forestry.Restore(again, island.Forestry.Export());
        Assert.True(again.HasTree(sapling.TileIndex));
        Assert.Equal(WoodResult.Chopped, again.Forestry.Chop(again, (int)sx, (int)sy, 1, sx - 1, sy, WithAxe()));
    }

    /// <summary>U stavby hráče mladý strom uschne.</summary>
    [Fact]
    public void SaplingNearStructureDies()
    {
        var island = World();
        var tree = OpenTree(island);
        var chopper = new AnimalTarget(1, tree.X - 1, tree.Y, Person());
        Fell(island, tree, chopper, new Animals(1), [chopper]);
        var sapling = island.Forestry.Saplings().Single();
        var (sx, sy) = (sapling.TileIndex % island.Width + 0.5, sapling.TileIndex / island.Width + 0.5);
        island.Structures.Restore([new StructureSave(1, BuildKind.StickWall, sx + 2, sy, 0, 1, 0, 7, string.Empty, null, null)]);

        var clock = TestPlants.Clock();
        clock.Advance(32400 * 3);
        island.Forestry.Update(island, new Animals(1), [], 0.1, clock);

        Assert.Empty(island.Forestry.Saplings());
    }

    // ---------- pomůcky ----------

    private static Island World() => WorldGenerator.Build(1, 128, 128);

    /// <summary>Strom, kolem kterého je volno (sekáč stojí západně, strom padá na východ).</summary>
    private static Tree OpenTree(Island island)
    {
        return island.Trees.First(t =>
            Enumerable.Range(-2, 10).All(dx => island.InBounds(Tx(t) + dx, Ty(t)) && !Movement.IsWaterTile(island.TileAt(Tx(t) + dx, Ty(t)))
                && (dx == 0 || !island.HasTrunk(island.Index(Tx(t) + dx, Ty(t))))));
    }

    /// <summary>Sekne tolikrát, kolik strom vydrží, a nechá ho dopadnout.</summary>
    private static void Fell(Island island, Tree tree, AnimalTarget chopper, Animals animals, IReadOnlyList<AnimalTarget> players)
    {
        var inventory = WithAxe();
        var result = WoodResult.Chopped;
        while (result != WoodResult.TreeFalling)
        {
            island.Forestry.Update(island, animals, players, 1, TestPlants.Clock());
            result = island.Forestry.Chop(island, Tx(tree), Ty(tree), chopper.Id, chopper.X, chopper.Y, inventory);
            Assert.Contains(result, new[] { WoodResult.Chopped, WoodResult.TreeFalling });
        }

        Assert.False(island.HasTree(tree.TileIndex), "Padající strom už nejde kácet.");
        island.Forestry.Update(island, animals, players, GameData.Wood.WarnSeconds + 0.1, TestPlants.Clock());
    }

    /// <summary>Počká na další seknutí (rytmus sekání).</summary>
    private static void Swing(Island island)
    {
        island.Forestry.Update(island, new Animals(1), [], 1, TestPlants.Clock());
    }

    private static Inventory WithAxe(int capacity = 10)
    {
        var inventory = new Inventory(capacity);
        inventory.TryAdd(new Item(ItemKind.Axe, Uses: 500));
        return inventory;
    }

    private static void Give(Inventory inventory, Item item, int count)
    {
        for (var i = 0; i < count; i++)
        {
            inventory.TryAdd(item);
        }
    }

    private static Character Person() => new(1, new Mulberry32(1));

    private static int Tx(Tree t) => (int)t.X;

    private static int Ty(Tree t) => (int)t.Y;
}

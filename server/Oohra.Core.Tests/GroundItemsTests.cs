using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Věci na zemi: zahodit, sebrat, výroba z hromádek u postavy, zmizení a pád z patra (ground.json).</summary>
public class GroundItemsTests
{
    private static readonly Island _island = WorldGenerator.Build(1, 128, 128);

    /// <summary>Místo na louce, kolem kterého je všude suchá zem (ať se věc položí opravdu před postavu).</summary>
    private static (double X, double Y) DryPoint()
    {
        for (var y = 10; y < _island.Height - 10; y++)
        {
            for (var x = 10; x < _island.Width - 10; x++)
            {
                if (Enumerable.Range(-3, 7).All(dy => Enumerable.Range(-3, 7).All(dx => Placement.OnDryLand(_island, x + dx, y + dy))))
                {
                    return (x + 0.5, y + 0.5);
                }
            }
        }

        throw new InvalidOperationException("Na ostrově není suché místo.");
    }

    private static CraftPlace PlaceAt(GroundItems ground, double x, double y)
    {
        return new CraftPlace(false, false, 0, Ground: new GroundAccess(ground, _island, x, y, 0, 0));
    }

    [Fact]
    public void DroppedItemsLieOnGroundAndCanBePickedUp()
    {
        var (x, y) = DryPoint();
        var ground = new GroundItems();
        var hands = Inventory.For(GenerationTraits.For(1));
        hands.TryAdd(Item.Stick);
        hands.TryAdd(Item.Stick);
        hands.TryAdd(Item.Stick);
        hands.TryAdd(Item.Stone);

        Assert.Equal(GroundResult.Dropped, ground.Drop(_island, hands, 0, x, y, 0, 0, 0));

        // Klacky leží před postavou (na východ), v rukou zůstal kámen.
        var pile = Assert.Single(ground.All);
        Assert.Equal(3, pile.Count);
        Assert.Equal(x + GameData.Ground.DropDistance, pile.X, 3);
        Assert.Equal(ItemKind.Stone, Assert.Single(hands.Stacks).Item.Kind);

        // Další klacek se přidá na stejnou hromádku.
        hands.TryAdd(Item.Stick);
        ground.Drop(_island, hands, 1, x, y, 0, 0, 0);
        Assert.Equal(4, Assert.Single(ground.All).Count);

        // Zdálky nic, zblízka se sebere celá hromádka.
        Assert.Equal(GroundResult.TooFar, ground.PickUp(_island, pile.Id, hands, x + 5, y, 0));
        Assert.Equal(GroundResult.TooFar, ground.PickUp(_island, pile.Id, hands, x, y, 1));
        Assert.Equal(GroundResult.Taken, ground.PickUp(_island, pile.Id, hands, x, y, 0));
        Assert.Empty(ground.All);
        Assert.Equal(4, hands.Count(ItemKind.Stick));
    }

    [Fact]
    public void FullHandsTakeOnlyWhatFits()
    {
        var (x, y) = DryPoint();
        var ground = new GroundItems();
        var hands = Inventory.For(GenerationTraits.For(1));
        var pile = ground.Put(_island, Item.Stone, 12, x, y, 0, 0, 0);
        hands.TryAdd(Item.Stick);
        hands.TryAdd(Item.Bast);

        Assert.Equal(GroundResult.InventoryFull, ground.PickUp(_island, pile.Id, hands, x, y, 0));

        hands.Drop(1);
        Assert.Equal(GroundResult.Taken, ground.PickUp(_island, pile.Id, hands, x, y, 0));
        Assert.Equal(GameData.Gathering.Inventory.StackSize, hands.Count(ItemKind.Stone));
        Assert.Equal(12 - GameData.Gathering.Inventory.StackSize, Assert.Single(ground.All).Count);
    }

    /// <summary>
    /// Nový člověk má jen dvě ruce: nůž v ruce, lýko a klacky na zemi u sebe, a batoh se povede. Batoh si hned
    /// nasadí, materiál ze země zmizí.
    /// </summary>
    [Fact]
    public void NewcomerMakesBagFromMaterialOnGround()
    {
        var (x, y) = DryPoint();
        var ground = new GroundItems();
        var maker = new Character(1, new Mulberry32(5));
        maker.Learn("smallBag");
        var hands = Inventory.For(GenerationTraits.For(1));
        hands.TryAdd(new Item(ItemKind.Knife, Uses: 10));
        ground.Put(_island, Item.Bast, 4, x, y, 0, 0, 0);
        ground.Put(_island, Item.Stick, 2, x, y, 0, Math.PI, 0);

        Assert.Equal(CraftResult.NeedMaterials, Crafting.Make("smallBag", hands, maker, new CraftPlace(false, false, 0)));
        Assert.Equal(CraftResult.Made, Crafting.Make("smallBag", hands, maker, PlaceAt(ground, x, y)));

        Assert.Equal(ItemKind.SmallBag, hands.Bag!.Kind);
        Assert.Empty(ground.All);
    }

    /// <summary>Výrobek, který se do plných rukou nevejde, se položí na zem. Dřív se výroba nepovedla.</summary>
    [Fact]
    public void ProductThatDoesNotFitLandsOnGround()
    {
        var (x, y) = DryPoint();
        var ground = new GroundItems();
        var maker = new Character(1, new Mulberry32(5));
        maker.Learn("spear");
        var hands = Inventory.For(GenerationTraits.For(1));
        for (var i = 0; i < 3; i++)
        {
            hands.TryAdd(Item.Stick);
            hands.TryAdd(Item.Stone);
        }

        Assert.Equal(CraftResult.InventoryFull, Crafting.Make("spear", hands, maker, new CraftPlace(false, false, 0)));
        Assert.Equal(CraftResult.Made, Crafting.Make("spear", hands, maker, PlaceAt(ground, x, y)));

        Assert.Equal(ItemKind.Spear, Assert.Single(ground.All).Item.Kind);
        Assert.Equal(2, hands.Count(ItemKind.Stick));
        Assert.Equal(2, hands.Count(ItemKind.Stone));
    }

    /// <summary>Materiál se bere napřed ze země. Hromádky dál než craftReach se nepočítají.</summary>
    [Fact]
    public void MaterialIsTakenFromNearbyGroundFirst()
    {
        var (x, y) = DryPoint();
        var ground = new GroundItems();
        var maker = new Character(1, new Mulberry32(5));
        maker.Learn("knife");
        var hands = Inventory.For(GenerationTraits.For(1));
        hands.TryAdd(Item.Stone);
        hands.TryAdd(Item.Stone);
        var far = ground.Put(_island, Item.Stone, 2, x + GameData.Ground.CraftReach + 2, y, 0, 0, 0);
        ground.Put(_island, Item.Stone, 1, x, y, 0, 0, 0);

        Assert.Equal(CraftResult.Made, Crafting.Make("knife", hands, maker, PlaceAt(ground, x, y)));

        // Kámen u nohou se spotřeboval, druhý z rukou, daleká hromádka zůstala.
        Assert.Equal(1, hands.Count(ItemKind.Stone));
        Assert.Equal(1, hands.Count(ItemKind.Knife));
        Assert.Equal(far.Id, Assert.Single(ground.All).Id);
    }

    /// <summary>Zkoušení (Spojit) jde i s hromádkami na zemi, které hráč zaškrtne.</summary>
    [Fact]
    public void CombineUsesSelectedGroundPiles()
    {
        var (x, y) = DryPoint();
        var ground = new GroundItems();
        var maker = new Character(1, new Mulberry32(5));
        maker.Learn("axe");
        maker.Learn("spear");
        var hands = Inventory.For(GenerationTraits.For(1));
        hands.TryAdd(Item.Stick);
        var stones = ground.Put(_island, Item.Stone, 2, x, y, 0, 0, 0);

        var (result, recipe) = Crafting.Combine([0], hands, maker, PlaceAt(ground, x, y), [stones.Id]);

        Assert.Equal(CraftResult.Made, result);
        Assert.Equal("axe", recipe!.Id);
        Assert.Equal(ItemKind.Axe, Assert.Single(hands.Stacks).Item.Kind);
        Assert.Empty(ground.All);
    }

    [Fact]
    public void PilesDecayAndFallFromUpperFloor()
    {
        var (x, y) = DryPoint();
        var ground = new GroundItems();
        var old = ground.Put(_island, Item.Stick, 1, x, y, 0, 0, 0);
        var upstairs = ground.Put(_island, Item.Stone, 1, x, y, 1, Math.PI, 600);
        var lifetime = GameData.Ground.LifetimeDays * 1440;

        ground.Decay(lifetime + 1);
        Assert.Null(ground.Find(old.Id));
        Assert.NotNull(ground.Find(upstairs.Id));

        ground.Settle(_ => false);
        Assert.Equal(0, ground.Find(upstairs.Id)!.Level);
    }
}

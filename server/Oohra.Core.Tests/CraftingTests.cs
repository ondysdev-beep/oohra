using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Items;
using Oohra.Core.Rng;
using Oohra.Core.World;

namespace Oohra.Core.Tests;

/// <summary>Ruce, batohy, recepty se zkoušením, opravy, lýko, kůže, popel a pazourek (návrh 0.8, kap. 3).</summary>
public class CraftingTests
{
    private static readonly CraftPlace Anywhere = new(false, false, 0);

    [Fact]
    public void NewcomerHasOnlyHandsAndBagAddsSlots()
    {
        var inventory = Inventory.For(GenerationTraits.For(1));
        var rules = GameData.Crafting;

        Assert.Equal(rules.Hands, inventory.Capacity);
        Assert.False(inventory.HasBag);
        inventory.TryAdd(Item.Stick);
        inventory.TryAdd(Item.Stone);
        Assert.False(inventory.TryAdd(Item.Bast), "V rukou jsou jen dvě místa.");

        // Batoh se nasadí na záda (nezabere místo v rukou) a přidá místa.
        Assert.True(inventory.TryAdd(new Item(ItemKind.SmallBag)));
        Assert.Equal(ItemKind.SmallBag, inventory.Bag!.Kind);
        Assert.Equal(2, inventory.Stacks.Count);
        Assert.Equal(rules.Hands + rules.BagSlots[ItemKind.SmallBag], inventory.Capacity);

        // Noví mají v batohu místa navíc. Větší batoh se nasadí místo menšího, ten pak nese v rukou.
        var later = Inventory.For(GenerationTraits.For(4));
        later.TryAdd(new Item(ItemKind.SmallBag));
        later.TryAdd(new Item(ItemKind.LargeBag));
        Assert.Equal(ItemKind.LargeBag, later.Bag!.Kind);
        Assert.Equal(ItemKind.SmallBag, Assert.Single(later.Stacks).Item.Kind);
        Assert.Equal(rules.Hands + rules.BagSlots[ItemKind.LargeBag] + Inventory.BagBonusFor(4), later.Capacity);
    }

    /// <summary>Bez batohu věci zůstanou, ale nic dalšího se nevejde, dokud jich není míň než míst.</summary>
    [Fact]
    public void DroppingBagKeepsItemsButBlocksNew()
    {
        var inventory = Inventory.For(GenerationTraits.For(1));
        inventory.TryAdd(new Item(ItemKind.SmallBag));
        inventory.TryAdd(Item.Stick);
        inventory.TryAdd(Item.Stone);
        inventory.TryAdd(Item.Bast);

        Assert.True(inventory.TakeOffBag());

        Assert.Equal(4, inventory.Stacks.Count);
        Assert.False(inventory.TryAdd(Item.Bone));
        Assert.Equal(0, inventory.RoomFor(Item.Bone));
    }

    /// <summary>
    /// Zkoušení: spojit věci, ze kterých jde recept. Dokud ho postava neumí, může se nepovést (kus materiálu
    /// přijde vniveč). Když se povede, recept umí. Z věcí, ze kterých nic nejde, nic nebude a nic se neztratí.
    /// </summary>
    [Fact]
    public void CombiningTeachesRecipeByTrying()
    {
        var c = new Character(1, new Mulberry32(3));
        var inventory = new Inventory(10);
        Assert.False(c.Knows("knife"));

        var results = new List<CraftResult>();
        for (var attempt = 0; attempt < 20 && !c.Knows("knife"); attempt++)
        {
            inventory.TryAdd(Item.Stone);
            inventory.TryAdd(Item.Stone);
            var stones = inventory.Stacks.ToList().FindIndex(s => s.Item.Kind == ItemKind.Stone);
            var (result, recipe) = Crafting.Combine([stones], inventory, c, Anywhere);
            Assert.Equal("knife", recipe!.Id);
            results.Add(result);
        }

        Assert.True(c.Knows("knife"));
        Assert.Equal(CraftResult.Learned, results[^1]);
        Assert.All(results.Take(results.Count - 1), r => Assert.Equal(CraftResult.Failed, r));
        Assert.Equal(GameData.Crafting.Find("knife")!.Uses, inventory.Stacks.Single(s => s.Item.Kind == ItemKind.Knife).Item.Uses);

        var nothing = new Inventory(10);
        nothing.TryAdd(Item.Bone);
        nothing.TryAdd(Item.Ash);
        Assert.Equal(CraftResult.NothingCame, Crafting.Combine([0, 1], nothing, c, Anywhere).Result);
        Assert.Equal(2, nothing.Stacks.Count);
    }

    /// <summary>Kopí i sekera jsou z klacku a kamene: zkoušení dá přednost tomu, co postava ještě neumí.</summary>
    [Fact]
    public void CombinePrefersUnknownRecipe()
    {
        var c = new Character(1, new Mulberry32(1));
        c.Learn("spear");
        var inventory = new Inventory(10);
        inventory.TryAdd(Item.Stick);
        inventory.TryAdd(Item.Stone);
        inventory.TryAdd(Item.Stone);

        Assert.Equal("axe", Crafting.Combine([0, 1], inventory, c, Anywhere).Recipe!.Id);

        var one = new Inventory(10);
        one.TryAdd(Item.Stick);
        one.TryAdd(Item.Stone);
        Assert.Equal("spear", Crafting.Combine([0, 1], one, c, Anywhere).Recipe!.Id);
    }

    [Fact]
    public void FailingUnknownRecipeLosesOnePiece()
    {
        // Najde postavu, které se první pokus nepovede.
        for (var seed = 1; seed < 50; seed++)
        {
            var c = new Character(1, new Mulberry32(seed));
            var inventory = new Inventory(10);
            inventory.TryAdd(Item.Stick);
            inventory.TryAdd(Item.Stone);
            if (Crafting.Make("spear", inventory, c, Anywhere) != CraftResult.Failed)
            {
                continue;
            }

            Assert.Equal(1, inventory.Stacks.Sum(s => s.Count));
            Assert.False(c.Knows("spear"));
            return;
        }

        Assert.Fail("Žádný první pokus se nepovedl, failChance je asi nula.");
    }

    [Fact]
    public void AncientsKnowBasicRecipesAndDeathForgetsThem()
    {
        var ancient = new Character(0, new Mulberry32(1));
        var newcomer = new Character(1, new Mulberry32(1));
        newcomer.Learn("smallBag");

        Assert.True(ancient.Knows("spear") && ancient.Knows("axe") && ancient.Knows("knife"));
        Assert.False(newcomer.Knows("axe"));
        Assert.True(Character.FromSave(newcomer.ToSave()).Knows("smallBag"));
        Assert.False(new Character(1, new Mulberry32(2)).Knows("smallBag"), "Nová postava (po smrti) začíná bez receptů.");
    }

    [Fact]
    public void TanningNeedsWaterFreshHideAndAsh()
    {
        var c = new Character(1, new Mulberry32(1));
        c.Learn("tanHide");
        var inventory = new Inventory(10);
        inventory.TryAdd(new Item(ItemKind.Hide, Day: 0));
        inventory.TryAdd(Item.Ash);

        Assert.Equal(CraftResult.NeedWater, Crafting.Make("tanHide", inventory, c, Anywhere));
        var rotten = new CraftPlace(true, false, (GameData.Crafting.HideRotDays + 1) * 1440);
        Assert.Equal(CraftResult.Rotten, Crafting.Make("tanHide", inventory, c, rotten));
        Assert.Equal(CraftResult.Made, Crafting.Make("tanHide", inventory, c, new CraftPlace(true, false, 600)));
        Assert.Equal(1, inventory.Count(ItemKind.TannedHide));
    }

    [Fact]
    public void LargeBagNeedsKnifeAndWearsIt()
    {
        var c = new Character(1, new Mulberry32(1));
        c.Learn("largeBag");
        var inventory = new Inventory(10);
        inventory.TryAdd(new Item(ItemKind.TannedHide));
        inventory.TryAdd(new Item(ItemKind.TannedHide));
        inventory.TryAdd(Item.Bone);
        inventory.TryAdd(Item.Bone);
        inventory.TryAdd(Item.Bast);

        Assert.Equal(CraftResult.NeedTool, Crafting.Make("largeBag", inventory, c, Anywhere));
        inventory.TryAdd(new Item(ItemKind.Knife, Uses: 10));
        Assert.Equal(CraftResult.Made, Crafting.Make("largeBag", inventory, c, Anywhere));
        Assert.Equal(1, inventory.Count(ItemKind.LargeBag));
        Assert.Equal(9, inventory.Stacks.Single(s => s.Item.Kind == ItemKind.Knife).Item.Uses);
    }

    [Fact]
    public void RepairAddsUsesUpToMaximum()
    {
        var c = new Character(1, new Mulberry32(1));
        c.Learn("repairAxe");
        var inventory = new Inventory(10);
        inventory.TryAdd(new Item(ItemKind.Axe, Uses: 10));
        inventory.TryAdd(Item.Stone);
        inventory.TryAdd(Item.Bast);
        var recipe = GameData.Crafting.Find("repairAxe")!;

        Assert.Equal(CraftResult.Made, Crafting.Make(recipe, inventory, c, Anywhere));
        Assert.Equal(10 + recipe.RepairUses, inventory.Stacks.Single(s => s.Item.Kind == ItemKind.Axe).Item.Uses);
        Assert.Equal(CraftResult.NeedMaterials, Crafting.Make(recipe, inventory, c, Anywhere));
    }

    [Fact]
    public void LayeredRockGivesFlintSometimes()
    {
        var island = WorldGenerator.Build(1);
        var piles = island.Rocks.All.Where(p => island.Tiles[p.TileIndex] == TileType.LayeredRock).Take(20).ToList();
        var inventory = new Inventory(40);
        foreach (var pile in piles)
        {
            var (x, y) = (pile.TileIndex % island.Width, pile.TileIndex / island.Width);
            Materials.TakeStone(island, x, y, inventory, pile.X + 1, pile.Y, 0);
        }

        Assert.NotEmpty(piles);
        Assert.True(inventory.Count(ItemKind.Flint) > 0, "Z vrstvené skály nepadl žádný pazourek.");
        Assert.True(inventory.Count(ItemKind.Stone) > 0);
    }

    [Fact]
    public void BoarGivesHidesAndBones()
    {
        var animals = new Animals(1);
        var boar = animals.Place(AnimalKind.Boar, 10, 10);
        boar.Hp = 0;
        animals.Crush(9, 10, 11, 10, 1, 1, 0);
        var inventory = new Inventory(10);

        Assert.Equal(HuntResult.Butchered, animals.Butcher(boar.Id, inventory, 10, 10.5, 0));
        Assert.Equal(GameData.Crafting.Carcass[AnimalKind.Boar].Hides, inventory.Count(ItemKind.Hide));
        Assert.Equal(GameData.Crafting.Carcass[AnimalKind.Boar].Bones, inventory.Count(ItemKind.Bone));
    }

    /// <summary>Deník ušitý jehlou dostane číslo, kreslí se do něj po stránkách a obsah se uloží.</summary>
    [Fact]
    public void JournalIsSewnWithNeedleAndKeepsDrawings()
    {
        var c = new Character(1, new Mulberry32(1));
        c.Learn("journal");
        var journals = new Journals();
        var inventory = new Inventory(10);
        inventory.TryAdd(new Item(ItemKind.TannedHide));
        inventory.TryAdd(Item.Bast);
        var place = new CraftPlace(false, false, 0, journals);

        Assert.Equal(CraftResult.NeedTool, Crafting.Make("journal", inventory, c, place));
        inventory.TryAdd(new Item(ItemKind.Needle, Uses: 5));
        Assert.Equal(CraftResult.Made, Crafting.Make("journal", inventory, c, place));
        var journal = inventory.Stacks.Single(s => s.Item.Kind == ItemKind.Journal).Item;
        Assert.True(journal.Ref > 0);
        Assert.Equal(GameData.Crafting.JournalPages, journals.Pages(journal.Ref)!.Count);

        var bits = SignBits.Empty();
        SignBits.Set(bits, 3, 4, true);
        Assert.True(journals.Draw(journal.Ref, 2, bits));
        Assert.False(journals.Draw(journal.Ref, 99, bits));

        var again = new Journals();
        again.Restore(journals.Export(), journals.NextId);
        Assert.True(SignBits.Get(again.Pages(journal.Ref)![2], 3, 4));
        Assert.NotEqual(journal.Ref, again.Create().Ref);
    }

    [Fact]
    public void BoneGivesNeedleAndHook()
    {
        var c = new Character(1, new Mulberry32(1));
        c.Learn("needle");
        c.Learn("hook");
        var inventory = new Inventory(10);
        inventory.TryAdd(new Item(ItemKind.Knife, Uses: 10));
        inventory.TryAdd(Item.Bone);
        inventory.TryAdd(Item.Bone);
        inventory.TryAdd(Item.Bast);

        Assert.Equal(CraftResult.Made, Crafting.Make("needle", inventory, c, Anywhere));
        Assert.Equal(CraftResult.Made, Crafting.Make("hook", inventory, c, Anywhere));
        Assert.Equal(1, inventory.Count(ItemKind.Needle));
        Assert.Equal(1, inventory.Count(ItemKind.Hook));
    }

    /// <summary>Ukázat recept: ten druhý ho pak umí bez zkoušení. Jen kdo ho umí a jen zblízka.</summary>
    [Fact]
    public void RecipeCanBeTaughtToSomeoneNearby()
    {
        var ancient = new Character(0, new Mulberry32(1));
        var newcomer = new Character(1, new Mulberry32(2));

        Assert.Equal(CraftResult.TooFarToTeach, Crafting.Teach(ancient, newcomer, "axe", 10));
        Assert.Equal(CraftResult.DontKnow, Crafting.Teach(newcomer, ancient, "smallBag", 1));
        Assert.Equal(CraftResult.Taught, Crafting.Teach(ancient, newcomer, "axe", 1));
        Assert.True(newcomer.Knows("axe"));
        Assert.Equal(CraftResult.AlreadyKnows, Crafting.Teach(ancient, newcomer, "axe", 1));
    }

    /// <summary>Mršina se objeví v lese, daleko od hráčů, jako mrtvé zvíře (jde z ní stáhnout kůže).</summary>
    [Fact]
    public void CarcassAppearsInForestAwayFromPlayers()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var animals = new Animals(1);
        Animal? carcass = null;
        for (var i = 0; i < 20 && carcass is null; i++)
        {
            carcass = animals.PlaceCarcass(island, 0, []);
        }

        Assert.NotNull(carcass);
        Assert.True(carcass!.IsDead);
        Assert.Equal(RegionKind.Forest, island.RegionAt((int)carcass.X, (int)carcass.Y));
        Assert.Contains(carcass.Kind, GameData.Animals.Carcasses.Kinds);
    }

    /// <summary>S háčkem se rybaří i bez kopí, háček se opotřebuje.</summary>
    [Fact]
    public void HookFishesWithoutSpear()
    {
        var island = WorldGenerator.Build(1, 128, 128);
        var pond = Enumerable.Range(0, island.Tiles.Length).First(i => island.Tiles[i] == TileType.Pond);
        var (tx, ty) = (pond % island.Width, pond / island.Width);
        var animals = new Animals(1);
        var inventory = new Inventory(10);
        inventory.TryAdd(new Item(ItemKind.Hook, Uses: 5));
        var fisher = new AnimalTarget(1, tx + 0.5, ty + 1.2, new Character(1, new Mulberry32(1)));

        var result = animals.Fish(island, tx, ty, fisher, inventory, 0);

        Assert.Contains(result, new[] { HuntResult.FishCaught, HuntResult.FishEscaped });
        Assert.Equal(4, inventory.Stacks.Single(s => s.Item.Kind == ItemKind.Hook).Item.Uses);
    }
}

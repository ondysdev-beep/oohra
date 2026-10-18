using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.World;

namespace Oohra.Core.Items;

/// <summary>Výroba nástrojů z batohu (prototyp makeSpear v part5.js).</summary>
public static class Crafting
{
    /// <summary>Kopí: klacek a kámen. Jedno kopí stačí. Procvičí řemeslo.</summary>
    public static HuntResult MakeSpear(Inventory inventory, Character maker)
    {
        if (inventory.Count(ItemKind.Spear) > 0)
        {
            return HuntResult.AlreadyHasSpear;
        }

        if (inventory.Count(ItemKind.Stick) < 1 || inventory.Count(ItemKind.Stone) < 1)
        {
            return HuntResult.NeedMaterials;
        }

        inventory.TryRemove(ItemKind.Stick, 1);
        inventory.TryRemove(ItemKind.Stone, 1);
        if (!inventory.TryAdd(Item.Spear))
        {
            inventory.TryAdd(Item.Stick);
            inventory.TryAdd(Item.Stone);
            return HuntResult.InventoryFull;
        }

        maker.Practice(SkillKind.Craft, GameData.Animals.Hunt.PracticeSpear);
        return HuntResult.SpearMade;
    }

    /// <summary>
    /// Kamenná sekera (návrh 0.8, kap. 3): klacek a kameny (woodcutting.json). Jedna stačí.
    /// Opotřebuje se seknutím (uses). Procvičí řemeslo jako kopí.
    /// </summary>
    public static WoodResult MakeAxe(Inventory inventory, Character maker)
    {
        var rules = GameData.Wood.Axe;
        if (inventory.Count(ItemKind.Axe) > 0)
        {
            return WoodResult.AlreadyHasAxe;
        }

        if (inventory.Count(ItemKind.Stick) < rules.Sticks || inventory.Count(ItemKind.Stone) < rules.Stones)
        {
            return WoodResult.NeedMaterials;
        }

        inventory.TryRemove(ItemKind.Stick, rules.Sticks);
        inventory.TryRemove(ItemKind.Stone, rules.Stones);
        if (!inventory.TryAdd(new Item(ItemKind.Axe, Uses: rules.Uses)))
        {
            for (var i = 0; i < rules.Sticks; i++) inventory.TryAdd(Item.Stick);
            for (var i = 0; i < rules.Stones; i++) inventory.TryAdd(Item.Stone);
            return WoodResult.InventoryFull;
        }

        maker.Practice(SkillKind.Craft, GameData.Animals.Hunt.PracticeSpear);
        return WoodResult.AxeMade;
    }
}

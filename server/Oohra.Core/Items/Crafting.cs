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
}

using Oohra.Core.Plants;

namespace Oohra.Core.Items
{
    /// <summary>Druh věci v batohu.</summary>
    public enum ItemKind
    {
        Plant = 0,
        Stick = 1,
        Stone = 2,

        /// <summary>Maso ze zvířete (syrové nebo upečené).</summary>
        Meat = 3,

        /// <summary>Kopí: klacek a ostrý kámen. Silnější rána při lovu.</summary>
        Spear = 4,
    }

    /// <summary>
    /// Věc v batohu: část rostliny, maso, materiál (klacek, kámen), nebo nástroj (kopí).
    /// Část rostliny nese i to, odkud je a jak je upravená. Maso nese, jestli je upečené.
    /// Stejné věci se skládají na hromádku.
    /// </summary>
    public sealed record Item(ItemKind Kind, PlantItem? Plant = null, Cooking Cooking = Cooking.Raw)
    {
        public static readonly Item Stick = new Item(ItemKind.Stick);

        public static readonly Item Stone = new Item(ItemKind.Stone);

        public static readonly Item RawMeat = new Item(ItemKind.Meat);

        public static readonly Item BakedMeat = new Item(ItemKind.Meat, null, Cooking.Baked);

        public static readonly Item Spear = new Item(ItemKind.Spear);

        public static Item Of(PlantItem plant)
        {
            return new Item(ItemKind.Plant, plant);
        }
    }
}

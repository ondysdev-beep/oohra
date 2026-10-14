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

        /// <summary>Kopí: klacek a ostrý kámen. Silnější rána při lovu, rybaření.</summary>
        Spear = 4,

        /// <summary>Ryba (jako maso: syrová nebo upečená).</summary>
        Fish = 5,
    }

    /// <summary>
    /// Věc v batohu: část rostliny, maso nebo ryba, materiál (klacek, kámen), nebo nástroj (kopí).
    /// Část rostliny nese i to, odkud je a jak je upravená. Maso a ryba nesou, jestli jsou upečené,
    /// a herní den, kdy byly získané (časem se zkazí). Stejné věci se skládají na hromádku.
    /// </summary>
    public sealed record Item(ItemKind Kind, PlantItem? Plant = null, Cooking Cooking = Cooking.Raw, int Day = 0)
    {
        public static readonly Item Stick = new Item(ItemKind.Stick);

        public static readonly Item Stone = new Item(ItemKind.Stone);

        public static readonly Item Spear = new Item(ItemKind.Spear);

        /// <summary>Maso nebo ryba (IsFlesh).</summary>
        public bool IsFlesh => Kind == ItemKind.Meat || Kind == ItemKind.Fish;

        public static Item Of(PlantItem plant)
        {
            return new Item(ItemKind.Plant, plant);
        }

        /// <summary>Syrové maso získané v daný herní den.</summary>
        public static Item RawMeat(int day)
        {
            return new Item(ItemKind.Meat, null, Cooking.Raw, day);
        }

        /// <summary>Syrová ryba chycená v daný herní den.</summary>
        public static Item RawFish(int day)
        {
            return new Item(ItemKind.Fish, null, Cooking.Raw, day);
        }
    }
}

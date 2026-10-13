using Oohra.Core.Plants;

namespace Oohra.Core.Items
{
    /// <summary>Druh věci v batohu.</summary>
    public enum ItemKind
    {
        Plant = 0,
        Stick = 1,
        Stone = 2,
    }

    /// <summary>
    /// Věc v batohu: část rostliny, nebo materiál (klacek, kámen).
    /// Část rostliny nese i to, odkud je a jak je upravená. Stejné věci se skládají na hromádku.
    /// </summary>
    public sealed record Item(ItemKind Kind, PlantItem? Plant = null)
    {
        public static readonly Item Stick = new Item(ItemKind.Stick);

        public static readonly Item Stone = new Item(ItemKind.Stone);

        public static Item Of(PlantItem plant)
        {
            return new Item(ItemKind.Plant, plant);
        }
    }
}

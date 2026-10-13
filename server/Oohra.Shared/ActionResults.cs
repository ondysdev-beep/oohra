namespace Oohra.Core.World
{
    /// <summary>Jak sběr dopadl. Klient si k tomu přiřadí text.</summary>
    public enum HarvestResult
    {
        Picked,
        TooFar,
        PlantGone,
        NoLeavesLeft,
        NoFruit,
        InventoryFull,
    }
}

namespace Oohra.Core.Characters
{
    /// <summary>Jak pití dopadlo. Klient si k tomu přiřadí text.</summary>
    public enum DrinkResult
    {
        Clean,
        Muddy,

        /// <summary>Kalná voda: instinkt Nových, napil se jen trochu.</summary>
        MuddySip,

        Salty,
        NotWater,
        TooFar,
    }
}

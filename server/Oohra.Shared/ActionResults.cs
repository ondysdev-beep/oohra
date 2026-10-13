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

    /// <summary>Jak dopadlo sbírání klacků u stromu nebo kamene u skály.</summary>
    public enum MaterialResult
    {
        /// <summary>Nasbíral klacky (1 nebo 2).</summary>
        GotSticks,

        GotStone,
        TooFar,

        /// <summary>Spadlé větve jsou pryč, dorostou zítra.</summary>
        NothingLeft,

        InventoryFull,

        /// <summary>Tady není strom (skála).</summary>
        WrongPlace,
    }

    /// <summary>Jak dopadlo rozdělání ohně, přiložení nebo pečení.</summary>
    public enum FireResult
    {
        Lit,
        Fed,
        Relit,
        Baked,

        /// <summary>Na rozdělání ohně chybí klacky nebo kámen.</summary>
        NeedMaterials,

        /// <summary>Přiložit: nemá klacek.</summary>
        NoStick,

        /// <summary>Před postavou nejde oheň rozdělat (voda, strom, jiný oheň).</summary>
        CannotBuildHere,

        TooFar,

        /// <summary>Pečení: žádný hořící oheň poblíž.</summary>
        NoFireNear,

        /// <summary>Pečení: tohle se péct nedá (není to jídlo, nebo už je upečené).</summary>
        CannotBake,

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

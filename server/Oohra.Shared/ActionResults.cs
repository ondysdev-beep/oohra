namespace Oohra.Core.World
{
    /// <summary>Jak dopadlo kácení, sekání kmene, štípání, nošení kladu nebo výroba sekery.</summary>
    public enum WoodResult
    {
        AxeMade,

        /// <summary>Sekera už je v batohu.</summary>
        AlreadyHasAxe,

        /// <summary>Na sekeru chybí klacek nebo kameny.</summary>
        NeedMaterials,

        /// <summary>Bez sekery to nejde (rukama jen klacky).</summary>
        NeedAxe,

        /// <summary>Seknutí do stromu (strom ještě stojí).</summary>
        Chopped,

        /// <summary>Strom zapraskal a padá (na stranu od toho, kdo sekal).</summary>
        TreeFalling,

        /// <summary>Odsekl klad z kmene.</summary>
        PieceCut,

        /// <summary>Rozštípal klad na díly dřeva.</summary>
        PieceSplit,

        /// <summary>Naštípal díl dřeva na třísky.</summary>
        WoodSplit,

        /// <summary>Sekera se rozpadla (byla moc opotřebovaná).</summary>
        AxeBroke,

        PickedUp,
        Dropped,

        /// <summary>Už nese klad, druhý neunese.</summary>
        AlreadyCarrying,

        /// <summary>Celý kmen jeden člověk neunese, napřed ho rozsekej na klady.</summary>
        TooHeavy,

        /// <summary>Nic nenese (položit).</summary>
        NotCarrying,

        TooFar,

        /// <summary>Tady už strom (nebo dřevo) není.</summary>
        NotFound,

        /// <summary>Moc rychle po sobě (sekat jde jen v rytmu).</summary>
        TooSoon,

        InventoryFull,

        /// <summary>Tohle se štípat nedá (není to díl dřeva).</summary>
        CannotSplit,
    }

    /// <summary>Stav stromu, který už nestojí celý: padá, nebo zbyl jen pařez.</summary>
    public enum TreeState
    {
        Falling = 1,
        Stump = 2,
    }

    /// <summary>Dřevo ležící ve světě: celý kmen (blokuje cestu, seká se na klady), nebo klad (1 m).</summary>
    public enum TimberKind
    {
        Trunk = 0,
        Piece = 1,
    }

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

    /// <summary>Jak dopadla stavba nebo rozebrání stavby.</summary>
    public enum BuildResult
    {
        Built,
        Removed,

        /// <summary>Chybí materiál (klacky).</summary>
        NeedMaterials,

        /// <summary>Tady nejde stavět (voda, kmen stromu, oheň, stojí tam hráč).</summary>
        CannotBuildHere,

        TooFar,

        /// <summary>Rozebrat: taková stavba už není.</summary>
        NotFound,

        /// <summary>Dveře: poblíž není zeď s volným otvorem.</summary>
        NoDoorway,

        /// <summary>Cizí stavba: rozebrat ji smí jen majitel a jeho kmen.</summary>
        NotYours,

        /// <summary>V zásobách něco je, napřed je vyber.</summary>
        NotEmpty,

        /// <summary>Tady se nedávno narodil cizí hráč: místo je ještě chráněné před stavbou.</summary>
        BirthPlaceProtected,
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

        /// <summary>Tady nejde oheň rozdělat (voda, kmen stromu, jiný oheň, zeď).</summary>
        CannotBuildHere,

        TooFar,

        /// <summary>Pečení: žádný hořící oheň poblíž.</summary>
        NoFireNear,

        /// <summary>Pečení: tohle se péct nedá (není to jídlo, nebo už je upečené).</summary>
        CannotBake,

        InventoryFull,

        /// <summary>Zapálil pochodeň od ohně (klacek z batohu).</summary>
        TorchLit,

        /// <summary>Pochodeň už hoří.</summary>
        TorchAlreadyLit,
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

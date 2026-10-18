namespace Oohra.Core.World
{
    /// <summary>Jak dopadla výroba nebo zkoušení receptu (spojit věci z batohu).</summary>
    public enum CraftResult
    {
        /// <summary>Vyrobil to (recept už znal).</summary>
        Made,

        /// <summary>Poprvé se to povedlo: recept teď umí.</summary>
        Learned,

        /// <summary>Recept ještě neumí a nepovedlo se: kus materiálu přišel vniveč.</summary>
        Failed,

        /// <summary>Z těchhle věcí nic nebude (žádný recept).</summary>
        NothingCame,

        NeedMaterials,

        /// <summary>Chybí nástroj (třeba nůž).</summary>
        NeedTool,

        /// <summary>Tohle se dělá u vody.</summary>
        NeedWater,

        /// <summary>Tohle se dělá u ohně.</summary>
        NeedFire,

        /// <summary>Tuhle věc už má (stačí jedna).</summary>
        AlreadyHave,

        /// <summary>Oprava: nástroj není, nebo je celý.</summary>
        NothingToRepair,

        /// <summary>Syrová kůže už zahnila.</summary>
        Rotten,

        InventoryFull,
    }

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

        /// <summary>Sloupl nožem lýko z kůry.</summary>
        BastStripped,

        /// <summary>Kůra už je sloupaná (nebo strom lýko nedá).</summary>
        NoBark,

        /// <summary>Bez nože to nepůjde.</summary>
        NeedKnife,

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

    /// <summary>
    /// Stav stromu, který není ze seedu celý: padá, zbyl z něj pařez, roste nový mladý strom (Sapling, s růstem),
    /// nebo mladý strom už dorostl (Grown, je to normální strom).
    /// </summary>
    public enum TreeState
    {
        Falling = 1,
        Stump = 2,
        Sapling = 3,
        Grown = 4,
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

        /// <summary>Z vrstvené skály se odloupl pazourek.</summary>
        GotFlint,

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

        /// <summary>Nabral popel z ohniště.</summary>
        AshTaken,

        /// <summary>Popel tu ještě není (oheň hořel krátce), nebo už je dnes vybraný.</summary>
        NoAsh,

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

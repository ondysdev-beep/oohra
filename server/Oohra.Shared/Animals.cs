namespace Oohra.Core.World
{
    /// <summary>Druhy zvířat (prototyp ECO v part4.js, návrh 12.5).</summary>
    public enum AnimalKind
    {
        /// <summary>Divočák: osamělé nebezpečné zvíře. Zaměří se, vyrazí, pak je vyčerpaný.</summary>
        Boar = 0,

        /// <summary>Králík (malé zvíře): pase se na rostlinách, utíká, v noci spí.</summary>
        Rabbit = 1,

        /// <summary>Jelen: stádo, útěk.</summary>
        Deer = 2,

        /// <summary>Vlk: teritoriální predátor, v noci se plíží k lidem mimo oheň a úkryt.</summary>
        Wolf = 3,
    }

    /// <summary>Co zvíře právě dělá. Klient podle toho mění vzhled (výpad, vyčerpání, smrt).</summary>
    public enum AnimalState
    {
        Wander = 0,
        Graze = 1,
        Flee = 2,
        Tired = 3,
        Asleep = 4,
        Stalk = 5,
        Alert = 6,
        Charge = 7,
        Exhaust = 8,
        Dead = 9,
    }

    /// <summary>Jak dopadl lov, stažení masa nebo výroba kopí. Klient si k tomu přiřadí text.</summary>
    public enum HuntResult
    {
        /// <summary>Udeřil pěstí, zvíře žije (spíš ho to rozzuřilo).</summary>
        HitFist,

        /// <summary>Zasáhl kopím, zvíře žije.</summary>
        HitSpear,

        Killed,

        /// <summary>Chytil malé zvíře (králíka).</summary>
        CaughtSmall,

        TooFar,

        /// <summary>Další rána až za chvíli (prodleva mezi ranami).</summary>
        TooSoon,

        /// <summary>Zvíře už tu není.</summary>
        NotFound,

        /// <summary>Stáhl maso z mrtvého zvířete.</summary>
        Butchered,

        /// <summary>Stáhnout maso jde jen z mrtvého zvířete.</summary>
        NotDead,

        InventoryFull,

        SpearMade,
        AlreadyHasSpear,

        /// <summary>Na kopí chybí klacek nebo kámen.</summary>
        NeedMaterials,

        /// <summary>V pasti nic není.</summary>
        TrapEmpty,

        FishCaught,

        /// <summary>Ryba proklouzla.</summary>
        FishEscaped,

        /// <summary>Na rybaření je potřeba kopí.</summary>
        NeedSpear,

        /// <summary>Tady se rybařit nedá (jen jezírko a moře).</summary>
        NotWater,

        /// <summary>Cizí past: úlovek smí vzít jen majitel a jeho kmen.</summary>
        NotYours,
    }
}

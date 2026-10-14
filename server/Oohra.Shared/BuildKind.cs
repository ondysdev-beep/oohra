namespace Oohra.Core.World
{
    /// <summary>
    /// Co se dá postavit v režimu stavby. Každá stavba má přesnou polohu a úhel, není vázaná na dlaždice.
    /// Další stavby a dekorace se přidají sem.
    /// </summary>
    public enum BuildKind
    {
        /// <summary>Příčka z klacků: tenká stěna dlouhá 1 m.</summary>
        StickWall = 0,

        /// <summary>Oheň (ohniště): klacky a kámen.</summary>
        Campfire = 1,

        /// <summary>Zeď s otvorem na dveře: stejně dlouhá jako zeď, blokují jen sloupky po krajích.</summary>
        DoorFrame = 2,

        /// <summary>Dveře: vsadí se do otvoru ve zdi. Pohyb neblokují (otevřou se), ale uzavřou úkryt.</summary>
        Door = 3,

        /// <summary>Past z klacků: chytí králíka nebo jelena, který jde kolem. Pohyb neblokuje.</summary>
        Trap = 4,

        /// <summary>Zásoby kmene: koš z klacků. Ukládat a brát smí jen majitel a jeho kmen. Pohyb neblokuje.</summary>
        Stockpile = 5,

        /// <summary>Dekorace ze seznamu v decorations.json (lavice, totem…). Která to je, říká DecorationId.</summary>
        Decoration = 6,
    }
}

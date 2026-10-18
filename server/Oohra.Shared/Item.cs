using Oohra.Core.Plants;
using Oohra.Core.World;

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

        /// <summary>Kamenná sekera: kácení stromů a štípání dřeva. Opotřebuje se (Uses), pak se rozpadne.</summary>
        Axe = 6,

        /// <summary>Díl dřeva z rozštípaného kladu. V ohni hoří déle než klacek, dá se naštípat na třísky.</summary>
        Wood = 7,

        /// <summary>Tříska: jako klacek (podpal, pochodeň), později jehla na šití.</summary>
        Splinter = 8,

        /// <summary>Klad (kus kmene 1 m). Nosí se jen jeden a postava s ním jde pomaleji.</summary>
        LogPiece = 9,

        /// <summary>Malý batoh z lýka a klacků: přidá místa k rukám.</summary>
        SmallBag = 10,

        /// <summary>Velký batoh z kůže a kostí.</summary>
        LargeBag = 11,

        /// <summary>Lýko: vnitřní kůra stromu (nožem z ležícího kmene). Na batoh a opravy.</summary>
        Bast = 12,

        /// <summary>Syrová kůže ze zvířete. Do pár dnů zahnije, vyčiní se popelem u vody.</summary>
        Hide = 13,

        /// <summary>Vyčiněná kůže: nehnije, na velký batoh (později oblečení a deník).</summary>
        TannedHide = 14,

        /// <summary>Kost ze zvířete: nůž, později jehla a háček.</summary>
        Bone = 15,

        /// <summary>Popel z ohniště: na činění kůže.</summary>
        Ash = 16,

        /// <summary>Nůž (kámen, pazourek nebo kost): loupání lýka, velký batoh. Opotřebuje se.</summary>
        Knife = 17,

        /// <summary>Pazourek z vrstvené skály: nejlepší nářadí.</summary>
        Flint = 18,
    }

    /// <summary>
    /// Věc v batohu: část rostliny, maso nebo ryba, materiál (klacek, kámen, dřevo), nebo nástroj (kopí, sekera).
    /// Část rostliny nese i to, odkud je a jak je upravená. Maso a ryba nesou, jestli jsou upečené,
    /// a herní den, kdy byly získané (časem se zkazí). Nástroj nese, kolik použití mu zbývá (Uses),
    /// klad, z jakého je stromu (Tree). Stejné věci se skládají na hromádku.
    /// </summary>
    public sealed record Item(ItemKind Kind, PlantItem? Plant = null, Cooking Cooking = Cooking.Raw, int Day = 0, int Uses = 0, TreeKind Tree = TreeKind.Oak)
    {
        public static readonly Item Stick = new Item(ItemKind.Stick);

        public static readonly Item Stone = new Item(ItemKind.Stone);

        public static readonly Item Spear = new Item(ItemKind.Spear);

        public static readonly Item Wood = new Item(ItemKind.Wood);

        public static readonly Item Splinter = new Item(ItemKind.Splinter);

        public static readonly Item Bast = new Item(ItemKind.Bast);

        public static readonly Item Bone = new Item(ItemKind.Bone);

        public static readonly Item Ash = new Item(ItemKind.Ash);

        public static readonly Item Flint = new Item(ItemKind.Flint);

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

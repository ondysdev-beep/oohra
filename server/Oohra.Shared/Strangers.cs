namespace Oohra.Core.Social
{
    /// <summary>Co cizinec (člověk z cizího kmene) právě dělá. Klient podle toho mění vzhled.</summary>
    public enum StrangerState
    {
        Idle = 0,

        /// <summary>Jde někam (k jídlu, k vodě, toulá se).</summary>
        Walk = 1,

        /// <summary>Jí nebo pije.</summary>
        Eat = 2,

        /// <summary>Utíká k táboru před zvířetem.</summary>
        Flee = 3,
    }

    /// <summary>Jak dopadlo, když hráč cizinci něco dal nebo od něj dostal.</summary>
    public enum StrangerResult
    {
        /// <summary>Cizinec hráči podal upečenou rostlinu.</summary>
        Gift = 0,

        /// <summary>Cizinec chtěl něco dát, ale batoh je plný.</summary>
        GiftNoRoom = 1,

        /// <summary>Dar přijal a usmál se (dar zpátky až za čas).</summary>
        Accepted = 2,

        /// <summary>Zavrtěl hlavou a věc zahodil (škodí).</summary>
        Discarded = 3,

        /// <summary>Neví, co s tím (není to jídlo).</summary>
        Puzzled = 4,

        TooFar = 5,
        NotFound = 6,
    }
}

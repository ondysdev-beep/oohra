namespace Oohra.Core.Social
{
    /// <summary>Výsledek pozvání do kmene, odpovědi, odchodu a práce se zásobami kmene.</summary>
    public enum TribeResult
    {
        /// <summary>Pozvánka odešla (pozvaný ji uvidí a odpoví).</summary>
        Invited = 0,
        Joined = 1,
        Declined = 2,
        Left = 3,
        TooFar = 4,

        /// <summary>Už jste spolu v kmeni.</summary>
        AlreadyTogether = 5,

        /// <summary>Pozvánka už neplatí (vypršela nebo žádná nebyla).</summary>
        NoInvite = 6,

        /// <summary>Hráč není v žádném kmeni.</summary>
        NotInTribe = 7,
        Stored = 8,
        Taken = 9,

        /// <summary>Zásoby jsou plné.</summary>
        StockFull = 10,
        InventoryFull = 11,

        /// <summary>Cizí zásoby: brát a ukládat smí jen majitel a jeho kmen.</summary>
        NotYours = 12,
        NotFound = 13,

        /// <summary>Pozvaný hráč odmítl pozvánku (zpráva pro toho, kdo zval).</summary>
        TheyDeclined = 14,

        /// <summary>Pozvaný hráč přijal (zpráva pro toho, kdo zval).</summary>
        TheyJoined = 15,
    }
}

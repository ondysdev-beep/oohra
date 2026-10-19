namespace Oohra.Core.World
{
    /// <summary>Výsledek položení věci na zem nebo sebrání hromádky ze země.</summary>
    public enum GroundResult
    {
        /// <summary>Položil věc na zem.</summary>
        Dropped = 0,

        /// <summary>Sebral hromádku (nebo kolik se vešlo).</summary>
        Taken = 1,

        TooFar = 2,

        /// <summary>Hromádka už tu není (někdo ji sebral, nebo zmizela).</summary>
        NotFound = 3,

        /// <summary>Do rukou ani do batohu se už nic nevejde.</summary>
        InventoryFull = 4,
    }
}

namespace Oohra.Core.World
{
    /// <summary>Druh stromu (návrh 0.8, kap. 2). Výšky a kde roste jsou v trees.json.</summary>
    public enum TreeKind : byte
    {
        /// <summary>Dub (listnatý), les a louky.</summary>
        Oak = 0,

        /// <summary>Jehličnan (borovice nebo smrk), les a vrchovina.</summary>
        Conifer = 1,

        /// <summary>Bříza nebo olše, les a okraj bažiny.</summary>
        Birch = 2,

        /// <summary>Vrba, bažina a břehy.</summary>
        Willow = 3,

        /// <summary>Suchý mrtvý strom, kdekoli, málo.</summary>
        Dead = 4,
    }
}

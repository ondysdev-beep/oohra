namespace Oohra.Core.Social
{
    /// <summary>Co hráč nahlašuje (tlačítko Nahlásit, návrh 14.3).</summary>
    public enum ReportTarget
    {
        /// <summary>Jiný hráč (chování, podvádění).</summary>
        Player = 0,

        /// <summary>Značka nebo rytina (nevhodná kresba).</summary>
        Sign = 1,

        /// <summary>Nic konkrétního: chyba ve hře nebo jiný problém.</summary>
        General = 2,
    }

    /// <summary>Důvod nahlášení. Volný text hra nemá, správce si zbytek zjistí ze záznamu.</summary>
    public enum ReportReason
    {
        /// <summary>Obtěžování, urážky, schválnosti.</summary>
        Behavior = 0,

        /// <summary>Podvádění (upravený klient, zneužití chyby).</summary>
        Cheating = 1,

        /// <summary>Nevhodná kresba na značce.</summary>
        OffensiveSign = 2,

        /// <summary>Chyba ve hře.</summary>
        Bug = 3,

        Other = 4,
    }

    /// <summary>Jak dopadlo nahlášení.</summary>
    public enum ReportResult
    {
        /// <summary>Uloženo, správce se na to podívá.</summary>
        Sent = 0,

        /// <summary>Nahlašuješ moc často, chvíli počkej.</summary>
        TooOften = 1,

        /// <summary>Hráč nebo značka už není (nebo nahlašuješ sám sebe).</summary>
        NotFound = 2,
    }
}

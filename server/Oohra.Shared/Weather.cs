namespace Oohra.Core.World
{
    /// <summary>Počasí na ostrově. Všichni hráči vidí stejné (počítá ho server ze seedu a času).</summary>
    public enum WeatherKind
    {
        Clear = 0,

        /// <summary>Mlha: nejčastěji ráno, přes den občas.</summary>
        Fog = 1,
        Rain = 2,

        /// <summary>Bouřka: silný déšť, vítr, blesky.</summary>
        Storm = 3,
    }
}

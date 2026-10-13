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
    }
}

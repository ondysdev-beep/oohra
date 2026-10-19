using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Katalog zvuků (soubor Assets/Resources/SoundCatalog.asset, menu Oohra → Zařadit zvuky ze složky Sounds).
    /// Nahrávky ze složky Assets/Sounds. Prázdné políčko = zvuk složený v kódu (Ambience), jako dosud.
    /// U jednorázových zvuků víc souborů = hra pokaždé vybere jeden (a trochu změní výšku), ať se neopakují.
    /// </summary>
    [CreateAssetMenu(menuName = "Oohra/Katalog zvuků", fileName = "SoundCatalog")]
    public class SoundCatalog : ScriptableObject
    {
        /// <summary>Název souboru v Assets/Resources.</summary>
        public const string Resource = "SoundCatalog";

        private static SoundCatalog _current;

        [Header("Smyčky (hrají pořád, konec se prolne se začátkem)")]
        [Tooltip("Praskání ohně (u nejbližšího hořícího ohně). Soubor fireplace.")]
        public AudioClip FireLoop;

        [Tooltip("Déšť. Soubor rain-sound.")]
        public AudioClip RainLoop;

        [Tooltip("Bouřka (místo deště, když je bouřka). Soubor thunderstorm.")]
        public AudioClip StormLoop;

        [Tooltip("Moře u pobřeží, sílí čím blíž je postava k moři. Soubory waves…")]
        public AudioClip SeaLoop;

        [Header("Kroky (prázdné = tráva, a když chybí i tráva, ťuknutí z kódu)")]
        [Tooltip("Tráva, les, louka, kamenitá půda. Soubory walk-on-grass…")]
        public AudioClip[] StepsGrass = new AudioClip[0];

        [Tooltip("Písek na pláži. Soubory walk-on-sand…")]
        public AudioClip[] StepsSand = new AudioClip[0];

        [Tooltip("Skála a jeskyně. Soubory walk-on-stone…")]
        public AudioClip[] StepsStone = new AudioClip[0];

        [Tooltip("Bažina, brod a zatopená zem. Soubory walk-in-mud…")]
        public AudioClip[] StepsMud = new AudioClip[0];

        [Tooltip("Podlaha stavby a patro. Soubory walk-on-wood…")]
        public AudioClip[] StepsWood = new AudioClip[0];

        [Header("Jednorázové zvuky")]
        [Tooltip("Seknutí sekerou do stromu, kmene nebo kladu. Soubory chopping-wood…")]
        public AudioClip[] Chop = new AudioClip[0];

        [Tooltip("Dopad padajícího stromu. Soubory tree-falls…")]
        public AudioClip[] TreeFall = new AudioClip[0];

        [Tooltip("Vzít kámen z hromady nebo pazourek ze skály. Soubory pickaxe-hit…")]
        public AudioClip[] TakeStone = new AudioClip[0];

        [Tooltip("Výroba z kamene nebo pazourku (nůž, sekera, kopí). Soubory rock-break…")]
        public AudioClip[] StoneWork = new AudioClip[0];

        [Tooltip("Postavená, rozebraná nebo opravená stavba. Soubory build_wall…")]
        public AudioClip[] Build = new AudioClip[0];

        [Tooltip("Kručení v břiše (hlad, bolení břicha). Soubory stomach-growl…")]
        public AudioClip[] Stomach = new AudioClip[0];

        [Header("Hlasitost nahrávek (0 až 1, násobí se hlasitostí hry)")]
        [Range(0f, 1f)] public float FireVolume = 0.6f;
        [Range(0f, 1f)] public float RainVolume = 0.55f;
        [Range(0f, 1f)] public float StormVolume = 0.65f;
        [Range(0f, 1f)] public float SeaVolume = 0.45f;
        [Range(0f, 1f)] public float StepsVolume = 0.35f;
        [Range(0f, 1f)] public float ChopVolume = 0.8f;
        [Range(0f, 1f)] public float TreeFallVolume = 1f;
        [Range(0f, 1f)] public float TakeStoneVolume = 0.6f;
        [Range(0f, 1f)] public float StoneWorkVolume = 0.6f;
        [Range(0f, 1f)] public float BuildVolume = 0.7f;
        [Range(0f, 1f)] public float StomachVolume = 0.6f;

        /// <summary>Katalog z Resources (bez souboru prázdný, pak všude zvuky z kódu).</summary>
        public static SoundCatalog Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<SoundCatalog>(Resource);
                    if (_current == null)
                    {
                        _current = CreateInstance<SoundCatalog>();
                    }
                }

                return _current;
            }
        }

        /// <summary>Náhodný zvuk ze seznamu, nebo null (prázdný seznam).</summary>
        public static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            return clips[Random.Range(0, clips.Length)];
        }
    }
}

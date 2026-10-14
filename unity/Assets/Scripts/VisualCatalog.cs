using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Jedna dekorace v katalogu vzhledu: id z decorations.json a její model.</summary>
    [System.Serializable]
    public class DecorationModel
    {
        [Tooltip("Stejné id jako v decorations.json (např. bench).")]
        public string Id;

        public GameObject Prefab;
    }

    /// <summary>
    /// Katalog vzhledu: které vlastní modely (prefaby) hra použije pro stromy, rostliny, stavby a zvířata.
    /// Upravuje se v Unity: soubor Assets/Resources/VisualCatalog.asset, do políček se přetáhnou prefaby.
    /// Prázdné políčko = jednoduchý tvar z kódu (kostka, koule), takže hra funguje i bez modelů.
    /// Návod je v docs/Grafika.md (velikost, natočení, materiály).
    /// </summary>
    [CreateAssetMenu(menuName = "Oohra/Katalog vzhledu", fileName = "VisualCatalog")]
    public class VisualCatalog : ScriptableObject
    {
        /// <summary>Název souboru v Assets/Resources.</summary>
        public const string Resource = "VisualCatalog";

        private static VisualCatalog _current;

        [Header("Stromy (střed kmene dole v počátku, výška asi 5 m)")]
        [Tooltip("Varianty stromů. Každý strom na ostrově dostane jednu podle svého čísla varianty.")]
        public GameObject[] Trees = new GameObject[0];

        [Header("Rostliny podle tvaru druhu (0 až 3), výška asi 0,3 až 0,6 m")]
        [Tooltip("Index = tvar druhu. Model se obarví barvou druhu (části se SpeciesTint, jinak celý), ať jdou druhy poznat.")]
        public GameObject[] Plants = new GameObject[4];

        [Header("Stavby (střed dole v počátku, délka podél osy X)")]
        [Tooltip("Zeď z klacků dlouhá přesně 1 m. Kratší zeď se zmenší podél osy X.")]
        public GameObject Wall;

        [Tooltip("Zeď s otvorem na dveře, 1 m dlouhá, otvor 0,8 m uprostřed.")]
        public GameObject DoorFrame;

        [Tooltip("Křídlo dveří 0,8 m široké, střed v počátku. Pant (otáčení) je na levém kraji, přidá ho hra.")]
        public GameObject Door;

        [Tooltip("Past z klacků, asi 0,6 × 0,6 m.")]
        public GameObject Trap;

        [Tooltip("Koš se zásobami kmene, průměr asi 0,6 m.")]
        public GameObject Stockpile;

        [Header("Dekorace (id stejné jako v server/Oohra.Core/Data/decorations.json)")]
        [Tooltip("Pro každou dekoraci její id a prefab (střed dole uprostřed, velikost podle radius v decorations.json).")]
        public DecorationModel[] Decorations = new DecorationModel[0];

        [Header("Zem: textury dlaždic (prázdné = vygenerovaná ze základní barvy)")]
        public Texture2D GrassTexture;
        public Texture2D SandTexture;

        [Tooltip("Les: tráva s jehličím a listím.")]
        public Texture2D ForestTexture;
        public Texture2D RockTexture;

        [Tooltip("Bažina a dno jezírek.")]
        public Texture2D BogTexture;

        [Tooltip("Po kolika metrech se textura zemi opakuje.")]
        public float GroundTextureSize = 4f;

        [Header("Kameny na skalách (nízké, do 35 cm: přes skálu se chodí)")]
        public GameObject[] Rocks = new GameObject[0];

        [Header("Voda")]
        [Tooltip("Barva hladiny, průhlednost je v alfě.")]
        public Color WaterColor = new Color(0.16f, 0.38f, 0.47f, 0.72f);

        [Tooltip("Normálová mapa vlnek (Texture Type: Normal map). Prázdné = vygenerovaná.")]
        public Texture2D WaterNormalMap;

        [Tooltip("Po kolika metrech se vlnky opakují.")]
        public float WaveSize = 3f;

        [Tooltip("Jak rychle se vlnky posouvají (metry za sekundu).")]
        public float WaveSpeed = 0.08f;

        [Header("Postava hráče (nohy dole v počátku, výška 1,8 m, obličej ve směru osy Z)")]
        [Tooltip("Animator může mít parametry Rychlost (Float, m/s), Spi (Bool) a Mrtvy (Bool). Části se SpeciesTint dostanou barvu generace.")]
        public GameObject Player;

        [Header("Oheň")]
        [Tooltip("Ohniště (kameny, polena), průměr asi 0,7 m, střed dole uprostřed. Použije se i u táborů.")]
        public GameObject Campfire;

        [Tooltip("Plamen, třeba částicový systém. Ukáže se jen, když oheň hoří. Světlo přidá hra.")]
        public GameObject Flame;

        [Header("Zvířata (nohy dole v počátku, hlava ve směru osy Z)")]
        public GameObject Boar;
        public GameObject Rabbit;
        public GameObject Deer;
        public GameObject Wolf;

        /// <summary>
        /// Katalog, který hra používá (z Resources). Když soubor chybí, je prázdný (všude tvary z kódu).
        /// Testy si sem můžou dát vlastní.
        /// </summary>
        public static VisualCatalog Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<VisualCatalog>(Resource);
                    if (_current == null)
                    {
                        _current = CreateInstance<VisualCatalog>();
                    }
                }

                return _current;
            }

            set { _current = value; }
        }

        /// <summary>Strom podle varianty (0 až 1), nebo null.</summary>
        public GameObject Tree(float variant)
        {
            var count = Trees == null ? 0 : Trees.Length;
            if (count == 0)
            {
                return null;
            }

            return Trees[Mathf.Clamp((int)(variant * count), 0, count - 1)];
        }

        /// <summary>Rostlina podle tvaru druhu, nebo null.</summary>
        public GameObject Plant(int shape)
        {
            return Plants != null && shape >= 0 && shape < Plants.Length ? Plants[shape] : null;
        }

        /// <summary>Stavba podle druhu (ohniště jen pro náhled v režimu stavby, postavený oheň kreslí FireViews), nebo null.</summary>
        public GameObject Structure(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.StickWall: return Wall;
                case BuildKind.DoorFrame: return DoorFrame;
                case BuildKind.Door: return Door;
                case BuildKind.Trap: return Trap;
                case BuildKind.Stockpile: return Stockpile;
                case BuildKind.Campfire: return Campfire;
                default: return null;
            }
        }

        /// <summary>Textura vrstvy zemi (GroundLayout.Grass…), nebo null.</summary>
        public Texture2D GroundTexture(int layer)
        {
            switch (layer)
            {
                case GroundLayout.Grass: return GrassTexture;
                case GroundLayout.Sand: return SandTexture;
                case GroundLayout.Forest: return ForestTexture;
                case GroundLayout.Rock: return RockTexture;
                default: return BogTexture;
            }
        }

        /// <summary>Dekorace podle id (decorations.json na serveru), nebo null.</summary>
        public GameObject Decoration(string id)
        {
            if (Decorations == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var decoration in Decorations)
            {
                if (decoration != null && decoration.Id == id)
                {
                    return decoration.Prefab;
                }
            }

            return null;
        }

        /// <summary>Zvíře podle druhu, nebo null.</summary>
        public GameObject Animal(AnimalKind kind)
        {
            switch (kind)
            {
                case AnimalKind.Boar: return Boar;
                case AnimalKind.Rabbit: return Rabbit;
                case AnimalKind.Deer: return Deer;
                default: return Wolf;
            }
        }

        /// <summary>Kvádr, do kterého se vejdou všechny viditelné části modelu (ve světě).</summary>
        public static Bounds BoundsOf(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(model.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        /// <summary>
        /// Vytvoří model z prefabu pod rodičem. Kolize (Collider) odstraní: pohyb a srážky počítá server.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Transform parent, string name)
        {
            var go = Instantiate(prefab, parent, false);
            go.name = name;
            foreach (var collider in go.GetComponentsInChildren<Collider>())
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            return go;
        }
    }
}

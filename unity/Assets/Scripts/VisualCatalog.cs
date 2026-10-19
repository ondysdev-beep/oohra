using System.Collections.Generic;
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

        [Header("Stromy (na středu a velikosti modelu nezáleží, hra je srovná na výšku stromu ze serveru)")]
        [Tooltip("Společné varianty stromů: použijí se pro druh, který nemá vlastní políčko níž.")]
        public GameObject[] Trees = new GameObject[0];

        [Tooltip("Dub (listnatý), 5 až 8 m. Víc modelů = víc variant.")]
        public GameObject[] OakTrees = new GameObject[0];

        [Tooltip("Jehličnan (borovice nebo smrk), 6 až 8 m.")]
        public GameObject[] ConiferTrees = new GameObject[0];

        [Tooltip("Bříza nebo olše, 4 až 7 m.")]
        public GameObject[] BirchTrees = new GameObject[0];

        [Tooltip("Vrba (bažina a břehy), 3 až 5 m.")]
        public GameObject[] WillowTrees = new GameObject[0];

        [Tooltip("Suchý mrtvý strom bez listí, 3 až 6 m.")]
        public GameObject[] DeadTrees = new GameObject[0];

        [Header("Kácení (prázdné = tvar z kódu)")]
        [Tooltip("Pařezy po pokáceném stromu (víc variant = víc políček, každý pařez dostane jednu). Hra je srovná na výšku 0,6 m.")]
        public GameObject[] Stumps = new GameObject[0];

        [Tooltip("Ležící kmen (třeba trunk_6m): jakkoli dlouhý model ležící podél osy X nebo Z. Hra ho natáhne na délku kmene a tloušťku srovná na 0,44 m.")]
        public GameObject Log;

        [Tooltip("Klad (kus kmene 1 m, třeba log_1m) na zemi a na rameni postavy. Hra ho srovná na 1 m délky a tloušťku 0,44 m.")]
        public GameObject LogPiece;

        [Tooltip("Mladý stromek, dokud roste (třeba sapling_01). Hra ho srovná na výšku podle růstu. Prázdné = zmenšený strom druhu.")]
        public GameObject[] Saplings = new GameObject[0];

        [Tooltip("Pochodeň v ruce, nezapálená (třeba item_torch). Hra ji srovná na 0,6 m, plamen, kouř a světlo přidá nahoře sama.")]
        public GameObject Torch;

        [Header("Batohy na zádech postavy (prázdné = kvádr z kódu)")]
        [Tooltip("Malý batoh z lýka. Přední strana (k zádům) míří k -Z modelu, hra ho srovná na výšku 0,4 m.")]
        public GameObject SmallBag;

        [Tooltip("Velký batoh z kůže. Hra ho srovná na výšku 0,6 m.")]
        public GameObject LargeBag;

        [Header("Rostliny podle tvaru druhu: políčka 0 až 3 (víc tvarů druhy nemají)")]
        [Tooltip("Index = tvar druhu. Model se obarví barvou druhu (části se SpeciesTint, jinak celý), ať jdou druhy poznat.")]
        public GameObject[] Plants = new GameObject[4];

        [Tooltip("Výška rostliny v metrech (hra model srovná na tuto výšku, ±15 % podle rostliny).")]
        public float PlantHeight = 0.45f;

        [Header("Tráva jen pro ozdobu (trsy na travnatých dlaždicích, nedá se na ně kliknout)")]
        public GameObject[] Grass = new GameObject[0];

        [Tooltip("Výška trsu trávy v metrech.")]
        public float GrassHeight = 0.3f;

        [Tooltip("Kolik trsů průměrně připadá na jednu travnatou dlaždici (1 m²).")]
        public float GrassPerTile = 1.5f;

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

        [Tooltip("Kamenitá půda skalnaté vrchoviny (mezi skalami).")]
        public Texture2D HighlandTexture;

        [Tooltip("Po kolika metrech se textura zemi opakuje.")]
        public float GroundTextureSize = 4f;

        [Header("Skály: hromady kamenů na skalních dlaždicích (z každého kamene je jeden kámen k vytěžení)")]
        public GameObject[] Rocks = new GameObject[0];

        [Tooltip("Nejmenší a největší kámen v hromadě: jeho největší rozměr v metrech (hra model srovná). Hromada má asi 1 m.")]
        public float RockMinSize = 0.4f;
        public float RockMaxSize = 0.6f;

        [Header("Voda")]
        [Tooltip("Barva hladiny, průhlednost je v alfě.")]
        public Color WaterColor = new Color(0.16f, 0.38f, 0.47f, 0.72f);

        [Tooltip("Normálová mapa vlnek (Texture Type: Normal map). Prázdné = vygenerovaná.")]
        public Texture2D WaterNormalMap;

        [Tooltip("Po kolika metrech se vlnky opakují.")]
        public float WaveSize = 3f;

        [Tooltip("Jak rychle se vlnky posouvají (metry za sekundu).")]
        public float WaveSpeed = 0.08f;

        [Tooltip("Kolik metrů odpovídá jednomu opakování souřadnic textury vody (u IgniteCoders 50, jejich blok vody má 50 m).")]
        public float WaterUvSize = 50f;

        [Tooltip("Vlastní materiál vody (třeba ze Shader Graphu). Prázdné = hladina hry (barva a vlnky výše). S vlastním materiálem se barva, vlnky a jejich posun neberou, vše je v materiálu.")]
        public Material WaterMaterial;

        [Tooltip("O kolik metrů níž se položí vlastní voda (WaterMaterial). Vlastní shadery často dělají vlny nahoru a voda by jinak přetékala přes břeh.")]
        public float CustomWaterDrop = 0.2f;

        [Tooltip("Prefab kamery odrazů pro vlastní vodu (např. IgniteCoders WaterReflectionCamera). Hra jí do pole reflectionPlane dosadí hladinu. Kamera navíc stojí výkon.")]
        public GameObject WaterReflectionCamera;

        [Tooltip("Barva pěny u břehu, alfa = jak je pěna hustá.")]
        public Color FoamColor = new Color(0.95f, 0.97f, 1f, 0.8f);

        [Tooltip("Jak široký je pruh pěny od břehu do vody (m).")]
        public float FoamWidth = 0.45f;

        [Header("Postava hráče (nohy dole v počátku, výška 1,8 m, obličej ve směru osy Z)")]
        [Tooltip("Animator může mít parametry Rychlost (Float, m/s), Spi (Bool) a Mrtvy (Bool). Části se SpeciesTint dostanou barvu generace.")]
        public GameObject Player;

        [Tooltip("Pradávní (první generace): mohutnější, kožešinový plášť, ochra. Prázdné = model Player. Šířku a výšku podle generace i tak upraví hra.")]
        public GameObject PlayerAncient;

        [Tooltip("Třetí generace a další (rok od vzniku světa): tkaný oděv, páska přes hruď, čelenka s pírkem. Prázdné = model Player.")]
        public GameObject PlayerThird;

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

        [Tooltip("Had (creature_snake): délka asi 1 m, leží na zemi, hlava ve směru osy Z. Animator parametr Stav (4 spí stočený, 6 varuje vztyčený, 0 leze, 9 mrtvý).")]
        public GameObject Snake;

        [Header("Nebezpečí")]
        [Tooltip("Barva kalné vody jarní záplavy nad zatopenou souší (průhlednost v alfě).")]
        public Color FloodColor = new Color(0.30f, 0.29f, 0.20f, 0.55f);

        [Tooltip("Vosí hnízdo (creature_wasp_nest): visí na větvi asi 2,6 m nad zemí, velké asi 0,3 × 0,4 m, úchyt nahoře uprostřed.")]
        public GameObject WaspNest;

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

        /// <summary>Strom podle druhu a varianty (0 až 1): z políčka druhu, když je prázdné, ze společného seznamu Trees, jinak null.</summary>
        public GameObject Tree(TreeKind kind, float variant)
        {
            return Pick(TreesOf(kind), variant) ?? Pick(Trees, variant);
        }

        private GameObject[] TreesOf(TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.Oak: return OakTrees;
                case TreeKind.Conifer: return ConiferTrees;
                case TreeKind.Birch: return BirchTrees;
                case TreeKind.Willow: return WillowTrees;
                default: return DeadTrees;
            }
        }

        /// <summary>Pařez podle varianty (0 až 1), nebo null.</summary>
        public GameObject Stump(float variant)
        {
            return Pick(Stumps, variant);
        }

        /// <summary>Mladý stromek podle varianty (0 až 1), nebo null.</summary>
        public GameObject Sapling(float variant)
        {
            return Pick(Saplings, variant);
        }

        /// <summary>Model ze seznamu podle varianty (prázdná políčka se přeskočí), nebo null.</summary>
        private static GameObject Pick(GameObject[] models, float variant)
        {
            var filled = new List<GameObject>();
            foreach (var model in models ?? new GameObject[0])
            {
                // Prázdné políčko není v Unity „opravdové“ null, proto != null.
                if (model != null) filled.Add(model);
            }

            return filled.Count == 0 ? null : filled[Mathf.Clamp((int)(variant * filled.Count), 0, filled.Count - 1)];
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
                case GroundLayout.Bog: return BogTexture;
                case GroundLayout.Highland: return HighlandTexture;
                default: return null;
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
                case AnimalKind.Snake: return Snake;
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
        /// <para>
        /// Model je zabalený do prázdného objektu (ten hra posouvá a otáčí) a srovnaný tak, aby stál dole uprostřed
        /// (center): na tom, kde má prefab střed a kde v něm model leží, pak nezáleží. Volitelně se zvětší nebo
        /// zmenší na výšku height nebo tak, aby jeho největší rozměr byl largest (metry, 0 = nechat).
        /// </para>
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Transform parent, string name, bool center = true, float height = 0f, float largest = 0f)
        {
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            var model = Instantiate(prefab, wrapper.transform, false);
            model.name = prefab.name;
            model.transform.localPosition = Vector3.zero;
            foreach (var collider in model.GetComponentsInChildren<Collider>())
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            if (center || height > 0f || largest > 0f)
            {
                Normalize(wrapper.transform, model.transform, center, height, largest);
            }

            return wrapper;
        }

        /// <summary>Posune model, aby stál dole uprostřed obalu, a nastaví obalu velikost (počítá se v klidu v počátku).</summary>
        private static void Normalize(Transform wrapper, Transform model, bool center, float height, float largest)
        {
            var position = wrapper.position;
            var rotation = wrapper.rotation;
            wrapper.position = Vector3.zero;
            wrapper.rotation = Quaternion.identity;
            var bounds = BoundsOf(model.gameObject);
            if (bounds.size != Vector3.zero)
            {
                var scale = wrapper.lossyScale;
                if (center)
                {
                    var offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    model.localPosition -= new Vector3(offset.x / scale.x, offset.y / scale.y, offset.z / scale.z);
                }

                var factor = height > 0f ? height / bounds.size.y
                    : largest > 0f ? largest / Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
                    : 1f;
                wrapper.localScale *= factor;
            }

            wrapper.position = position;
            wrapper.rotation = rotation;
        }
    }
}

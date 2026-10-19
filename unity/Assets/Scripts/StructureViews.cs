using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Stavby hráčů podle zpráv serveru: zeď (tenká příčka 1 m × 2 m × 10 cm), zeď s otvorem (dva sloupky
    /// a překlad nahoře) a dveře v otvoru, které se samy otevírají (DoorSwing). Stavby, které zakrývají
    /// postavu, zprůhlední (SeeThrough). Stejné modely používá náhled v režimu stavby.
    /// </summary>
    public class StructureViews : MonoBehaviour
    {
        // Rozměry v metrech. Délka, tloušťka a otvor jsou stejné jako na serveru (building.json).
        public const float WallLength = 1.0f;
        public const float WallHeight = 2.0f;

        /// <summary>Strana čtverce střechy a podlahy (building.json roof.size, floor.size).</summary>
        public const float PanelSize = 2f;

        /// <summary>Tloušťka střechy (leží na zdech, spodek ve výšce WallHeight).</summary>
        public const float RoofThickness = 0.14f;

        /// <summary>Výška jednoho podlaží: podlaha patra je nahoře ve výšce zdí a střechy přízemí.</summary>
        public const float LevelHeight = WallHeight + RoofThickness;

        /// <summary>Tloušťka podlahy (na zemi i v patře, vršek podlahy patra je ve výšce LevelHeight).</summary>
        public const float FloorThickness = 0.06f;

        /// <summary>Kde je spodek modelu stavby v podlaží level (podlaha patra leží o svou tloušťku níž).</summary>
        public static float BaseHeight(BuildKind kind, int level)
        {
            return level * LevelHeight - (kind == BuildKind.Floor && level > 0 ? FloorThickness : 0f);
        }
        public const float WallThickness = 0.1f;
        public const float OpeningWidth = 0.8f;
        public const float DoorHeight = 1.9f;
        public static readonly Color WallColor = new Color(0.54f, 0.42f, 0.26f);
        public static readonly Color DoorColor = new Color(0.45f, 0.33f, 0.2f);
        public static readonly Color DecorationColor = new Color(0.5f, 0.4f, 0.3f);

        // Klíče pro SeeThrough: každá stavba má až PartsPerStructure dílů (sloupky, překlad, dveře).
        private const int PartsPerStructure = 8;

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, int> _parts = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _caught = new Dictionary<int, int>();
        private readonly HashSet<int> _current = new HashSet<int>();
        private GameConnection _connection;
        private SeeThrough _seeThrough;
        private StructureView[] _shown;

        public void Init(GameConnection connection, SeeThrough seeThrough)
        {
            _connection = connection;
            _seeThrough = seeThrough;
        }

        /// <summary>Počet staveb ve scéně (pro testy).</summary>
        public int Count
        {
            get { return _views.Count; }
        }

        /// <summary>Dveře ve scéně podle čísla stavby (pro testy), nebo null.</summary>
        public DoorSwing DoorOf(int structureId)
        {
            GameObject view;
            return _views.TryGetValue(structureId, out view) ? view.GetComponentInChildren<DoorSwing>() : null;
        }

        /// <summary>
        /// Model stavby se středem v počátku, otočený podél osy x (úhel 0), zeď dlouhá length. Dveře mají pant jako samostatný
        /// objekt (pro otáčení). Vrací kořen modelu, díly jsou jeho děti.
        /// </summary>
        public static GameObject CreateModel(BuildKind kind, Transform parent, float length = WallLength, string decorationId = null, float radius = 0.3f)
        {
            var root = new GameObject(NameOf(kind));
            root.transform.SetParent(parent, false);
            if (kind == BuildKind.Decoration)
            {
                AddDecoration(root.transform, decorationId, radius);
                return root;
            }

            var prefab = VisualCatalog.Current.Structure(kind);
            if (prefab != null)
            {
                AddPrefab(root.transform, kind, prefab, length);
                return root;
            }

            switch (kind)
            {
                case BuildKind.DoorFrame:
                    var post = (WallLength - OpeningWidth) / 2;
                    Part(root.transform, "Sloupek", new Vector3(-WallLength / 2 + post / 2, WallHeight / 2, 0f), new Vector3(post, WallHeight, WallThickness), WallColor);
                    Part(root.transform, "Sloupek", new Vector3(WallLength / 2 - post / 2, WallHeight / 2, 0f), new Vector3(post, WallHeight, WallThickness), WallColor);
                    Part(root.transform, "Překlad", new Vector3(0f, (DoorHeight + WallHeight) / 2, 0f), new Vector3(OpeningWidth, WallHeight - DoorHeight, WallThickness), WallColor);
                    break;
                case BuildKind.Door:
                    var hinge = new GameObject("Pant");
                    hinge.transform.SetParent(root.transform, false);
                    hinge.transform.localPosition = new Vector3(-OpeningWidth / 2, 0f, 0f);
                    Part(hinge.transform, "Křídlo", new Vector3(OpeningWidth / 2, DoorHeight / 2, 0f), new Vector3(OpeningWidth - 0.02f, DoorHeight - 0.02f, 0.05f), DoorColor);
                    break;
                case BuildKind.Campfire:
                    Part(root.transform, "Ohniště", new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.05f, 0.7f), Palette.Hex("#4a4038"), PrimitiveType.Cylinder);
                    break;
                case BuildKind.Trap:
                    // Rámeček z klacků 60 × 60 cm a šikmá vzpěra.
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Part(root.transform, "Klacek", new Vector3(0f, 0.04f, side * 0.3f), new Vector3(0.66f, 0.06f, 0.06f), WallColor);
                        Part(root.transform, "Klacek", new Vector3(side * 0.3f, 0.04f, 0f), new Vector3(0.06f, 0.06f, 0.66f), WallColor);
                    }

                    var prop = Part(root.transform, "Vzpěra", new Vector3(0f, 0.2f, 0f), new Vector3(0.05f, 0.4f, 0.05f), WallColor);
                    prop.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    break;
                case BuildKind.Post:
                    Part(root.transform, "Sloupek", new Vector3(0f, WallHeight / 2, 0f), new Vector3(0.14f, WallHeight / 2, 0.14f), WallColor, PrimitiveType.Cylinder);
                    break;
                case BuildKind.Floor:
                    // Prkna z klacků: tři pruhy těsně vedle sebe.
                    for (var i = 0; i < 3; i++)
                    {
                        Part(root.transform, "Prkna", new Vector3(0f, FloorThickness / 2, (i - 1) * length / 3), new Vector3(length, FloorThickness, length / 3 - 0.02f), Palette.Hex("#8a6a48"));
                    }

                    break;
                case BuildKind.Ladder:
                    // Žebřík opřený o okraj patra: dvě bočnice a příčle, nakloněný k podlaze (osa X).
                    var tilt = Mathf.Atan2(0.5f, LevelHeight) * Mathf.Rad2Deg;
                    var ladder = new GameObject("Žebřík");
                    ladder.transform.SetParent(root.transform, false);
                    ladder.transform.localRotation = Quaternion.Euler(0f, 0f, -tilt);
                    foreach (var side in new[] { -0.22f, 0.22f })
                    {
                        Part(ladder.transform, "Bočnice", new Vector3(0f, LevelHeight / 2 + 0.1f, side), new Vector3(0.06f, LevelHeight + 0.2f, 0.06f), WallColor);
                    }

                    for (var i = 1; i <= 6; i++)
                    {
                        Part(ladder.transform, "Příčel", new Vector3(0f, i * LevelHeight / 7, 0f), new Vector3(0.04f, 0.04f, 0.44f), WallColor);
                    }

                    break;
                case BuildKind.Roof:
                    Part(root.transform, "Střecha", new Vector3(0f, WallHeight + RoofThickness / 2, 0f), new Vector3(length, RoofThickness, length), Palette.Hex("#6e5a3e"));
                    break;
                case BuildKind.Barricade:
                    // Zátaras: svislé klacky přes otvor a dva šikmé vzpěrné, vysoký asi 1,6 m.
                    for (var i = 0; i < 6; i++)
                    {
                        Part(root.transform, "Klacek", new Vector3(-0.6f + i * 0.24f, 0.8f, 0f), new Vector3(0.07f, 1.6f, 0.07f), WallColor);
                    }

                    foreach (var side in new[] { -1f, 1f })
                    {
                        var brace = Part(root.transform, "Vzpěra", new Vector3(0f, 0.8f, 0.05f), new Vector3(1.5f, 0.07f, 0.07f), WallColor);
                        brace.transform.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
                    }

                    break;
                case BuildKind.Stockpile:
                    // Koš z klacků: nízký válec a tmavé dno uvnitř.
                    Part(root.transform, "Koš", new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.22f, 0.6f), WallColor, PrimitiveType.Cylinder);
                    Part(root.transform, "Dno", new Vector3(0f, 0.445f, 0f), new Vector3(0.5f, 0.005f, 0.5f), Palette.Hex("#3b2d1e"), PrimitiveType.Cylinder);
                    break;
                default:
                    Part(root.transform, "Zeď", new Vector3(0f, WallHeight / 2, 0f), new Vector3(length, WallHeight, WallThickness), WallColor);
                    break;
            }

            return root;
        }

        /// <summary>
        /// Vlastní model z katalogu vzhledu. Zeď se zmenší podél osy X na svou délku. Dveře dostanou pant na levém
        /// kraji otvoru (otáčí jím DoorSwing), model křídla je od pantu posunutý o půl šířky otvoru.
        /// </summary>
        private static void AddPrefab(Transform root, BuildKind kind, GameObject prefab, float length)
        {
            if (kind == BuildKind.Door)
            {
                var hinge = new GameObject("Pant");
                hinge.transform.SetParent(root, false);
                hinge.transform.localPosition = new Vector3(-OpeningWidth / 2, 0f, 0f);
                var leaf = VisualCatalog.Spawn(prefab, hinge.transform, "Křídlo");
                leaf.transform.localPosition = new Vector3(OpeningWidth / 2, 0f, 0f);
                return;
            }

            var model = VisualCatalog.Spawn(prefab, root, NameOf(kind));
            if (kind == BuildKind.StickWall)
            {
                model.transform.localScale = new Vector3(length / WallLength, 1f, 1f);
            }
        }

        /// <summary>
        /// Dekorace: prefab z katalogu vzhledu podle id. Bez prefabu sloupek o poloměru dekorace s kuličkou
        /// navrchu, ať je na mapě vidět, kde stojí.
        /// </summary>
        private static void AddDecoration(Transform root, string decorationId, float radius)
        {
            var prefab = VisualCatalog.Current.Decoration(decorationId);
            if (prefab != null)
            {
                VisualCatalog.Spawn(prefab, root, decorationId);
                return;
            }

            Part(root, "Sloupek", new Vector3(0f, 0.4f, 0f), new Vector3(radius * 2, 0.4f, radius * 2), DecorationColor, PrimitiveType.Cylinder);
            Part(root, "Hlava", new Vector3(0f, 0.9f, 0f), Vector3.one * Mathf.Min(0.3f, radius * 1.2f), Palette.Hex("#c9a35a"), PrimitiveType.Sphere);
        }

        /// <summary>
        /// Natáhne zeď na délku (náhled v režimu stavby, když se zeď dorovnává). Vlastní model zdi se zmáčkne
        /// podél osy X jako při stavbě, tvar z kódu dostane přesný rozměr.
        /// </summary>
        public static void StretchWall(GameObject wall, float length)
        {
            // Vlastní model je v obalu (VisualCatalog.Spawn), ten se zmáčkne podél X. Tvar z kódu dostane přesný rozměr.
            var part = wall.transform.GetChild(0);
            part.localScale = VisualCatalog.Current.Wall != null
                ? new Vector3(length / WallLength, 1f, 1f)
                : new Vector3(length, WallHeight, WallThickness);
        }

        /// <summary>Druh dekorace podle id ze seznamu od serveru, nebo null.</summary>
        private DecorationTypeView DecorationOf(string id)
        {
            foreach (var decoration in _connection.DecorationTypes)
            {
                if (decoration.Id == id)
                {
                    return decoration;
                }
            }

            return null;
        }

        /// <summary>Postaví kořen modelu na místo: střed (x, y) na mapě, úhel v radiánech a výška spodku (podlaží).</summary>
        public static void PlaceModel(Transform root, double x, double y, float angle, float height = 0f)
        {
            root.position = WorldCoords.ToUnity(x, y, height);
            root.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
        }

        public static string NameOf(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.DoorFrame: return "Zeď s otvorem";
                case BuildKind.Door: return "Dveře";
                case BuildKind.Campfire: return "Oheň";
                case BuildKind.Trap: return "Past";
                case BuildKind.Stockpile: return "Zásoby kmene";
                case BuildKind.Barricade: return "Zátaras";
                case BuildKind.Post: return "Sloupek";
                case BuildKind.Floor: return "Podlaha";
                case BuildKind.Roof: return "Střecha";
                case BuildKind.Ladder: return "Žebřík";
                case BuildKind.Decoration: return "Dekorace";
                default: return "Zeď";
            }
        }

        /// <summary>Střechy, které praskají (třesou se, za chvíli spadnou).</summary>
        private readonly HashSet<int> _cracking = new HashSet<int>();

        /// <summary>Kolik střech teď praská (pro testy).</summary>
        public int CrackingCount
        {
            get { return _cracking.Count; }
        }

        private void Update()
        {
            Shake();
            if (_connection == null || ReferenceEquals(_connection.Structures, _shown))
            {
                return;
            }

            _shown = _connection.Structures;
            _current.Clear();
            foreach (var structure in _shown)
            {
                _current.Add(structure.Id);
                int caught;
                if (_views.ContainsKey(structure.Id) && _caught.TryGetValue(structure.Id, out caught) && caught != structure.Caught)
                {
                    // Do pasti se něco chytilo (nebo úlovek někdo vzal): model se postaví znovu.
                    RemoveView(structure.Id);
                }

                if (structure.Cracking)
                {
                    _cracking.Add(structure.Id);
                }
                else if (_cracking.Remove(structure.Id) && _views.ContainsKey(structure.Id))
                {
                    // Střecha zase drží: vrátit ji na místo.
                    _views[structure.Id].transform.localRotation = Quaternion.Euler(0f, structure.Angle * Mathf.Rad2Deg, 0f);
                }

                if (!_views.ContainsKey(structure.Id))
                {
                    _views[structure.Id] = Create(structure);
                    _caught[structure.Id] = structure.Caught;
                }
            }

            var removed = new List<int>();
            foreach (var id in _views.Keys)
            {
                if (!_current.Contains(id))
                {
                    removed.Add(id);
                }
            }

            foreach (var id in removed)
            {
                RemoveView(id);
            }
        }

        /// <summary>Praskající střecha se třese a kolébá.</summary>
        private void Shake()
        {
            if (_shown == null)
            {
                return;
            }

            foreach (var structure in _shown)
            {
                GameObject view;
                if (structure.Cracking && _views.TryGetValue(structure.Id, out view))
                {
                    var wobble = Mathf.Sin(Time.time * 37f + structure.Id) * 1.2f;
                    view.transform.localRotation = Quaternion.Euler(wobble, structure.Angle * Mathf.Rad2Deg, wobble * 0.5f);
                }
            }
        }

        private void RemoveView(int id)
        {
            for (var part = 0; part < _parts[id]; part++)
            {
                _seeThrough.Remove(-(id * PartsPerStructure + part));
            }

            Destroy(_views[id]);
            _views.Remove(id);
            _parts.Remove(id);
            _caught.Remove(id);
        }

        private GameObject Create(StructureView structure)
        {
            var decoration = DecorationOf(structure.Decoration);
            var root = CreateModel(structure.Kind, transform, structure.Length, structure.Decoration, decoration != null ? decoration.Radius : 0.3f);
            root.name = NameOf(structure.Kind) + " " + structure.Id;
            PlaceModel(root.transform, structure.X, structure.Y, structure.Angle, BaseHeight(structure.Kind, structure.Level));

            if (structure.Kind == BuildKind.Door)
            {
                root.transform.Find("Pant").gameObject.AddComponent<DoorSwing>().Init(_connection, structure.X, structure.Y, structure.Angle);
            }

            if (structure.Kind == BuildKind.Trap && structure.Caught >= 0)
            {
                // Úlovek v pasti: hnědý (jelen) nebo šedohnědý (králík) chlupatý kus.
                var deer = structure.Caught == (int)AnimalKind.Deer;
                Part(root.transform, "Úlovek", new Vector3(0f, 0.12f, 0f), deer ? new Vector3(0.45f, 0.3f, 0.6f) : new Vector3(0.25f, 0.18f, 0.32f),
                    deer ? Palette.Hex("#9b6b3c") : Palette.Hex("#a08a6a"), PrimitiveType.Sphere);
            }

            // Vlastní model zprůhlední celý najednou, tvary z kódu po dílech (sloupky, překlad zvlášť).
            // Barvu si každý díl vezme sám ze svého materiálu (color null).
            var parts = VisualCatalog.Current.Structure(structure.Kind) != null
                ? new[] { root }
                : System.Array.ConvertAll(root.GetComponentsInChildren<Renderer>(), r => r.gameObject);
            for (var i = 0; i < parts.Length && i < PartsPerStructure; i++)
            {
                var bounds = VisualCatalog.BoundsOf(parts[i]);
                double x, y;
                WorldCoords.FromUnity(bounds.center, out x, out y);
                _seeThrough.Add(-(structure.Id * PartsPerStructure + i), parts[i], null, x, y,
                    Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.min.y, bounds.max.y);
            }

            _parts[structure.Id] = Mathf.Min(parts.Length, PartsPerStructure);
            Pickable.Attach(root, InteractionTarget.ForStructure(structure.Id, structure.X, structure.Y));
            return root;
        }

        private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 size, Color color, PrimitiveType shape = PrimitiveType.Cube)
        {
            var part = IslandView.Primitive(shape, name, parent, color);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = size;
            return part;
        }
    }
}

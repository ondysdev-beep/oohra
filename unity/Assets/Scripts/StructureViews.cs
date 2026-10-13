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
        public const float WallThickness = 0.1f;
        public const float OpeningWidth = 0.8f;
        public const float DoorHeight = 1.9f;
        public static readonly Color WallColor = new Color(0.54f, 0.42f, 0.26f);
        public static readonly Color DoorColor = new Color(0.45f, 0.33f, 0.2f);

        // Klíče pro SeeThrough: každá stavba má až PartsPerStructure dílů (sloupky, překlad, dveře).
        private const int PartsPerStructure = 8;

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, int> _parts = new Dictionary<int, int>();
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
        /// Model stavby se středem v počátku, otočený podél osy x (úhel 0). Dveře mají pant jako samostatný
        /// objekt (pro otáčení). Vrací kořen modelu, díly jsou jeho děti.
        /// </summary>
        public static GameObject CreateModel(BuildKind kind, Transform parent)
        {
            var root = new GameObject(NameOf(kind));
            root.transform.SetParent(parent, false);
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
                default:
                    Part(root.transform, "Zeď", new Vector3(0f, WallHeight / 2, 0f), new Vector3(WallLength, WallHeight, WallThickness), WallColor);
                    break;
            }

            return root;
        }

        /// <summary>Postaví kořen modelu na místo: střed (x, y) na mapě a úhel v radiánech.</summary>
        public static void PlaceModel(Transform root, double x, double y, float angle)
        {
            root.position = WorldCoords.ToUnity(x, y);
            root.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
        }

        public static string NameOf(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.DoorFrame: return "Zeď s otvorem";
                case BuildKind.Door: return "Dveře";
                case BuildKind.Campfire: return "Oheň";
                default: return "Zeď";
            }
        }

        private void Update()
        {
            if (_connection == null || ReferenceEquals(_connection.Structures, _shown))
            {
                return;
            }

            _shown = _connection.Structures;
            _current.Clear();
            foreach (var structure in _shown)
            {
                _current.Add(structure.Id);
                if (!_views.ContainsKey(structure.Id))
                {
                    _views[structure.Id] = Create(structure);
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
                for (var part = 0; part < _parts[id]; part++)
                {
                    _seeThrough.Remove(-(id * PartsPerStructure + part));
                }

                Destroy(_views[id]);
                _views.Remove(id);
                _parts.Remove(id);
            }
        }

        private GameObject Create(StructureView structure)
        {
            var root = CreateModel(structure.Kind, transform);
            root.name = NameOf(structure.Kind) + " " + structure.Id;
            PlaceModel(root.transform, structure.X, structure.Y, structure.Angle);

            if (structure.Kind == BuildKind.Door)
            {
                root.transform.Find("Pant").gameObject.AddComponent<DoorSwing>().Init(_connection, structure.X, structure.Y, structure.Angle);
            }

            var renderers = root.GetComponentsInChildren<Renderer>();
            for (var i = 0; i < renderers.Length && i < PartsPerStructure; i++)
            {
                var color = structure.Kind == BuildKind.Door ? DoorColor : WallColor;
                var bounds = renderers[i].bounds;
                double x, y;
                WorldCoords.FromUnity(bounds.center, out x, out y);
                _seeThrough.Add(-(structure.Id * PartsPerStructure + i), renderers[i].gameObject, color, x, y,
                    Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.min.y, bounds.max.y);
            }

            _parts[structure.Id] = Mathf.Min(renderers.Length, PartsPerStructure);
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

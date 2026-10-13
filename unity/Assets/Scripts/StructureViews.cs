using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Stavby hráčů podle zpráv serveru. Zeď z klacků je tenká příčka (1 m × 2 m × 10 cm) s libovolným úhlem.
    /// Když stojí mezi postavou a kamerou, zprůhlední (SeeThrough).
    /// </summary>
    public class StructureViews : MonoBehaviour
    {
        // Rozměry zdi v metrech. Délka a tloušťka jsou stejné jako na serveru (building.json).
        public const float WallLength = 1.0f;
        public const float WallHeight = 2.0f;
        public const float WallThickness = 0.1f;
        public static readonly Color WallColor = new Color(0.54f, 0.42f, 0.26f);

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
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

        /// <summary>Zeď v Unity: střed a úhel z mapy. Úhel na mapě (y na jih) odpovídá otočení kolem svislé osy.</summary>
        public static void PlaceWall(Transform wall, double x, double y, float angle)
        {
            wall.position = WorldCoords.ToUnity(x, y, WallHeight / 2);
            wall.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            wall.localScale = new Vector3(WallLength, WallHeight, WallThickness);
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
                _seeThrough.Remove(-id);
                Destroy(_views[id]);
                _views.Remove(id);
            }
        }

        private GameObject Create(StructureView structure)
        {
            var wall = IslandView.Primitive(PrimitiveType.Cube, "Zeď " + structure.Id, transform, WallColor);
            PlaceWall(wall.transform, structure.X, structure.Y, structure.Angle);
            _seeThrough.Add(-structure.Id, wall, WallColor, structure.X, structure.Y, WallLength / 2, 0f, WallHeight);
            return wall;
        }
    }
}

using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Zdi z klacků podle zpráv serveru: kvádr přes celou dlaždici. Když postava stojí za zdí,
    /// zeď zprůhlední (SeeThrough), aby byla postava vidět.
    /// </summary>
    public class WallViews : MonoBehaviour
    {
        private const float Height = 0.7f;
        private static readonly Color WallColor = new Color(0.54f, 0.42f, 0.26f);

        private readonly Dictionary<int, GameObject> _walls = new Dictionary<int, GameObject>();
        private readonly HashSet<int> _current = new HashSet<int>();
        private GameConnection _connection;
        private WorldView _world;
        private SeeThrough _seeThrough;
        private int[] _shown;

        public void Init(GameConnection connection, WorldView world, SeeThrough seeThrough)
        {
            _connection = connection;
            _world = world;
            _seeThrough = seeThrough;
        }

        /// <summary>Počet zdí ve scéně (pro testy).</summary>
        public int Count
        {
            get { return _walls.Count; }
        }

        private void Update()
        {
            if (_connection == null || ReferenceEquals(_connection.Walls, _shown))
            {
                return;
            }

            _shown = _connection.Walls;
            _current.Clear();
            foreach (var tile in _shown)
            {
                _current.Add(tile);
                if (!_walls.ContainsKey(tile))
                {
                    _walls[tile] = Create(tile);
                }
            }

            var removed = new List<int>();
            foreach (var tile in _walls.Keys)
            {
                if (!_current.Contains(tile))
                {
                    removed.Add(tile);
                }
            }

            foreach (var tile in removed)
            {
                _seeThrough.Remove(tile);
                Destroy(_walls[tile]);
                _walls.Remove(tile);
            }
        }

        private GameObject Create(int tile)
        {
            var x = tile % _world.Width + 0.5;
            var y = tile / _world.Width + 0.5;
            var wall = IslandView.Primitive(PrimitiveType.Cube, "Zeď", transform, WallColor);
            wall.transform.position = WorldCoords.ToUnity(x, y, Height / 2);
            wall.transform.localScale = new Vector3(1f, Height, 1f);
            _seeThrough.Add(tile, wall, WallColor, onlyInFront: true);
            return wall;
        }
    }
}

using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oohra.Client
{
    /// <summary>
    /// Jarní záplava: kalná voda nad zatopenou souší podle serveru (seznam dlaždic). Jedna průsvitná plocha těsně nad
    /// zemí, každá dlaždice o kousek přesahuje, ať mezi nimi nejsou škvíry. Barva je v katalogu vzhledu (FloodColor).
    /// Když voda opadne, plocha zmizí.
    /// </summary>
    public class FloodView : MonoBehaviour
    {
        /// <summary>Výška hladiny nad souší (metry).</summary>
        public const float Height = 0.03f;

        private const float Overlap = 0.08f;

        private GameConnection _connection;
        private int[] _shown;
        private MeshFilter _filter;
        private MeshRenderer _renderer;

        public void Init(GameConnection connection)
        {
            _connection = connection;
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            var material = new Material(Palette.Foam) { name = "Záplava" };
            material.SetColor("_BaseColor", Color.white);
            _renderer.sharedMaterial = material;
        }

        /// <summary>Kolik dlaždic je teď vidět zatopených (pro testy).</summary>
        public int TileCount { get; private set; }

        private void Update()
        {
            if (_connection == null || _connection.World == null || ReferenceEquals(_connection.FloodedTiles, _shown))
            {
                return;
            }

            _shown = _connection.FloodedTiles;
            TileCount = _shown.Length;
            if (_filter.sharedMesh != null)
            {
                Destroy(_filter.sharedMesh);
            }

            _filter.sharedMesh = _shown.Length > 0 ? BuildMesh(_shown, _connection.World.Width, VisualCatalog.Current.FloodColor) : null;
            _renderer.enabled = _shown.Length > 0;
        }

        /// <summary>Čtverec nad každou zatopenou dlaždicí (barva i s průhledností ve vrcholech).</summary>
        public static Mesh BuildMesh(int[] tiles, int width, Color color)
        {
            var vertices = new List<Vector3>(tiles.Length * 4);
            var colors = new List<Color>(tiles.Length * 4);
            var triangles = new List<int>(tiles.Length * 6);
            foreach (var tile in tiles)
            {
                var (x, y) = (tile % width, tile / width);
                var start = vertices.Count;
                vertices.Add(WorldCoords.ToUnity(x - Overlap, y - Overlap, Height));
                vertices.Add(WorldCoords.ToUnity(x + 1 + Overlap, y - Overlap, Height));
                vertices.Add(WorldCoords.ToUnity(x + 1 + Overlap, y + 1 + Overlap, Height));
                vertices.Add(WorldCoords.ToUnity(x - Overlap, y + 1 + Overlap, Height));
                for (var k = 0; k < 4; k++)
                {
                    colors.Add(color);
                }

                // Pořadí vrcholů po směru hodinových ručiček při pohledu shora: plocha míří nahoru.
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            var mesh = new Mesh { name = "Záplava", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

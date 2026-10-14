using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oohra.Client
{
    /// <summary>
    /// Pěna u břehu: pruh na hladině podél pobřeží (u souše plná, do vody slábne), který pomalu pulzuje.
    /// Čára břehu je tam, kde podíl souše (GroundLayout.LandFraction) klesne pod polovinu, hledá se po čtverečcích
    /// 25 cm (marching squares). Barva a šířka jsou v katalogu vzhledu.
    /// </summary>
    public class FoamView : MonoBehaviour
    {
        private const float Step = 0.25f;

        private Material _material;
        private Color _color;

        public static FoamView Build(WorldView world, Transform parent)
        {
            var catalog = VisualCatalog.Current;
            var go = new GameObject("Pěna");
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(world, catalog.FoamWidth);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var material = new Material(Palette.Foam) { name = "Pěna" };
            renderer.sharedMaterial = material;

            var view = go.AddComponent<FoamView>();
            view._material = material;
            view._color = catalog.FoamColor;
            material.SetColor("_BaseColor", view._color);
            return view;
        }

        /// <summary>
        /// Síť pěny: za každý kousek čáry břehu jeden obdélník na hladině. Vnitřní hrana (u souše) má alfu 1,
        /// vnější (width metrů do vody) 0. Šířka trochu kolísá, ať pruh nevypadá jako pravítko.
        /// </summary>
        public static Mesh BuildMesh(WorldView world, float width)
        {
            var columns = Mathf.CeilToInt(world.Width / Step) + 1;
            var rows = Mathf.CeilToInt(world.Height / Step) + 1;
            var land = new float[rows, columns];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    land[r, c] = GroundLayout.LandFraction(world, c * Step, r * Step);
                }
            }

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (var r = 0; r < rows - 1; r++)
            {
                for (var c = 0; c < columns - 1; c++)
                {
                    AddCell(land, r, c, width, vertices, colors, triangles);
                }
            }

            var mesh = new Mesh { name = "Pěna" };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Jeden čtvereček: kde přes něj vede čára břehu (podíl souše 0,5), tam přidá kousek pěny.</summary>
        private static void AddCell(float[,] land, int r, int c, float width, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            // Rohy čtverečku: 0 = (c, r), 1 = (c+1, r), 2 = (c+1, r+1), 3 = (c, r+1). Mapa: x = c * Step, y = r * Step.
            var values = new[] { land[r, c], land[r, c + 1], land[r + 1, c + 1], land[r + 1, c] };
            var points = new[] { new Vector2(c, r), new Vector2(c + 1, r), new Vector2(c + 1, r + 1), new Vector2(c, r + 1) };
            var crossings = new List<Vector2>();
            for (var i = 0; i < 4; i++)
            {
                var a = values[i];
                var b = values[(i + 1) % 4];
                if ((a >= 0.5f) != (b >= 0.5f))
                {
                    var t = (0.5f - a) / (b - a);
                    crossings.Add(Vector2.Lerp(points[i], points[(i + 1) % 4], t) * Step);
                }
            }

            if (crossings.Count < 2)
            {
                return;
            }

            // Směr do vody: od rohů se souší k rohům bez ní.
            var landSide = Vector2.zero;
            for (var i = 0; i < 4; i++)
            {
                landSide += points[i] * (values[i] - 0.5f);
            }

            for (var i = 0; i + 1 < crossings.Count; i += 2)
            {
                AddStrip(crossings[i], crossings[i + 1], -landSide, width, r * 31 + c, vertices, colors, triangles);
            }
        }

        private static void AddStrip(Vector2 from, Vector2 to, Vector2 toWater, float width, int seed, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            var along = to - from;
            if (along.sqrMagnitude < 1e-8f)
            {
                return;
            }

            var normal = new Vector2(-along.y, along.x).normalized;
            if (Vector2.Dot(normal, toWater) < 0)
            {
                normal = -normal;
            }

            var reach = width * (0.75f + 0.5f * GroundView.Hash01(seed, 7, 3));
            var start = vertices.Count;
            var height = GroundLayout.WaterLevel + 0.01f;
            vertices.Add(WorldCoords.ToUnity(from.x, from.y, height));
            vertices.Add(WorldCoords.ToUnity(to.x, to.y, height));
            vertices.Add(WorldCoords.ToUnity(to.x + normal.x * reach, to.y + normal.y * reach, height));
            vertices.Add(WorldCoords.ToUnity(from.x + normal.x * reach, from.y + normal.y * reach, height));
            colors.Add(Color.white);
            colors.Add(Color.white);
            colors.Add(new Color(1f, 1f, 1f, 0f));
            colors.Add(new Color(1f, 1f, 1f, 0f));

            // Materiál pěny kreslí obě strany, na směru otočení nezáleží.
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        private void Update()
        {
            if (_material == null)
            {
                return;
            }

            // Příboj: pěna pomalu sílí a slábne.
            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 0.9f);
            _material.SetColor("_BaseColor", new Color(_color.r, _color.g, _color.b, _color.a * pulse));
        }
    }
}

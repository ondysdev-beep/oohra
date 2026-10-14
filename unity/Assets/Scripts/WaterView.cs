using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Vodní hladina: jedna velká průhledná plocha kousek pod souší (moře kolem ostrova i jezírka, jejich dno je níž,
    /// viz GroundLayout). Kolem ostrova je to jemná síť (po 2 m, ať vlastní shader může dělat vlny), dál k obzoru
    /// velké pruhy. Souřadnice textury jsou v metrech (WaterUvSize metrů = 1), takže se textura neroztáhne.
    /// Voda hry má vlnky z normálové mapy, která se pomalu posouvá. Vlastní materiál a kamera odrazů jsou v katalogu.
    /// </summary>
    public class WaterView : MonoBehaviour
    {
        // Jemná síť sahá do vzdálenosti Inner × velikost ostrova, hladina celkem Extent × (moře až k obzoru).
        private const float Inner = 3f;
        private const float Extent = 6f;
        private const float GridStep = 2f;

        // O kolik hladina přesahuje okraj vodní dlaždice (končí pod souší, zespodu není vidět).
        private const float ShoreMargin = 0.5f;
        private const int NormalSize = 64;

        private Material _material;
        private Vector2 _direction = new Vector2(0.8f, 0.6f);
        private float _speed;

        public static WaterView Build(WorldView world, Transform parent)
        {
            var catalog = VisualCatalog.Current;
            var water = new GameObject("Voda");
            water.transform.SetParent(parent);
            water.transform.position = new Vector3(0f, SurfaceLevel(catalog), 0f);
            water.AddComponent<MeshFilter>().sharedMesh = BuildMesh(world, Mathf.Max(1f, catalog.WaterUvSize));
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var view = water.AddComponent<WaterView>();
            if (catalog.WaterMaterial != null)
            {
                // Vlastní voda z katalogu: všechno nastavuje její materiál, hra jen připojí kameru odrazů.
                renderer.sharedMaterial = new Material(catalog.WaterMaterial) { name = "Voda" };
                AddReflectionCamera(catalog.WaterReflectionCamera, water.transform, parent);
                return view;
            }

            var material = new Material(Palette.Water) { name = "Voda" };
            material.SetColor("_BaseColor", catalog.WaterColor);
            material.SetTexture("_BumpMap", catalog.WaterNormalMap != null ? catalog.WaterNormalMap : GeneratedNormalMap());
            material.SetFloat("_BumpScale", 0.35f);
            renderer.sharedMaterial = material;
            view._material = material;
            view._speed = catalog.WaveSpeed;
            var tiling = Mathf.Max(1f, catalog.WaterUvSize) / Mathf.Max(0.5f, catalog.WaveSize);
            material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            return view;
        }

        /// <summary>Výška hladiny: voda hry kousek pod souší, vlastní voda o CustomWaterDrop níž (kvůli vlnám).</summary>
        public static float SurfaceLevel(VisualCatalog catalog)
        {
            return catalog.WaterMaterial != null ? GroundLayout.WaterLevel - Mathf.Max(0f, catalog.CustomWaterDrop) : GroundLayout.WaterLevel;
        }

        /// <summary>
        /// Síť hladiny ve světových souřadnicích (výška 0, objekt je ve výšce hladiny): jemná mřížka kolem ostrova
        /// a čtyři velké pruhy dál. UV = poloha v metrech / uvSize.
        /// </summary>
        public static Mesh BuildMesh(WorldView world, float uvSize)
        {
            var size = Mathf.Max(world.Width, world.Height);
            var center = new Vector2(world.Width / 2f, -world.Height / 2f);
            var inner = size * Inner / 2f;
            var outer = size * Extent / 2f;
            var cells = Mathf.CeilToInt(inner * 2f / GridStep);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var j = 0; j <= cells; j++)
            {
                for (var i = 0; i <= cells; i++)
                {
                    vertices.Add(new Vector3(center.x - inner + i * GridStep, 0f, center.y - inner + j * GridStep));
                }
            }

            for (var j = 0; j < cells; j++)
            {
                for (var i = 0; i < cells; i++)
                {
                    // Jen nad vodou (s okrajem, který skončí pod souší): pod ostrovem hladina není,
                    // vlny vlastního shaderu by ho jinak zaplavily.
                    var a = j * (cells + 1) + i;
                    if (NearWater(world, vertices[a].x, -vertices[a].z, GridStep, ShoreMargin))
                    {
                        AddQuad(triangles, a, a + 1, a + cells + 1, a + cells + 2);
                    }
                }
            }

            var edge = inner;
            // Pruhy k obzoru: sever a jih přes celou šířku, západ a východ mezi nimi.
            AddBand(vertices, triangles, center.x - outer, center.y + edge, center.x + outer, center.y + outer);
            AddBand(vertices, triangles, center.x - outer, center.y - outer, center.x + outer, center.y - edge);
            AddBand(vertices, triangles, center.x - outer, center.y - edge, center.x - edge, center.y + edge);
            AddBand(vertices, triangles, center.x + edge, center.y - edge, center.x + outer, center.y + edge);

            var uvs = new List<Vector2>();
            foreach (var v in vertices)
            {
                uvs.Add(new Vector2(v.x / uvSize, v.z / uvSize));
            }

            var mesh = new Mesh { name = "Hladina" };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Leží ve čtverci (roh x, y na mapě vlevo dole v Unity, strana size, rozšířený o margin) nějaká voda?
        /// Mimo mapu je moře.
        /// </summary>
        private static bool NearWater(WorldView world, float x, float y, float size, float margin)
        {
            for (var ty = Mathf.FloorToInt(y - size - margin); ty <= Mathf.FloorToInt(y + margin); ty++)
            {
                for (var tx = Mathf.FloorToInt(x - margin); tx <= Mathf.FloorToInt(x + size + margin); tx++)
                {
                    if (IsWaterTile(world, tx, ty))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsWaterTile(WorldView world, int tx, int ty)
        {
            return tx < 0 || ty < 0 || tx >= world.Width || ty >= world.Height || GroundLayout.IsWater(world.Tiles[ty * world.Width + tx]);
        }

        /// <summary>Čtverec ze čtyř vrcholů (dole vlevo, dole vpravo, nahoře vlevo, nahoře vpravo), lícem nahoru.</summary>
        private static void AddQuad(List<int> triangles, int bottomLeft, int bottomRight, int topLeft, int topRight)
        {
            triangles.AddRange(new[] { bottomLeft, topLeft, bottomRight, bottomRight, topLeft, topRight });
        }

        private static void AddBand(List<Vector3> vertices, List<int> triangles, float x1, float z1, float x2, float z2)
        {
            var start = vertices.Count;
            vertices.Add(new Vector3(x1, 0f, z1));
            vertices.Add(new Vector3(x2, 0f, z1));
            vertices.Add(new Vector3(x1, 0f, z2));
            vertices.Add(new Vector3(x2, 0f, z2));
            AddQuad(triangles, start, start + 1, start + 2, start + 3);
        }

        /// <summary>
        /// Kamera odrazů pro vlastní vodu (prefab z katalogu). Skript kamery je cizí (není v našem kódu), proto se
        /// hladina dosadí podle jména pole: každé veřejné pole typu Transform jménem reflectionPlane.
        /// </summary>
        private static void AddReflectionCamera(GameObject prefab, Transform water, Transform parent)
        {
            if (prefab == null)
            {
                return;
            }

            if (!HasReflectionPlane(prefab))
            {
                // Do políčka přišlo něco jiného než kamera odrazů (třeba kus vody): nic se nevytvoří.
                Debug.LogWarning("Oohra: WaterReflectionCamera v katalogu vzhledu není kamera odrazů (chybí pole reflectionPlane), použije se voda bez odrazů.");
                return;
            }

            var camera = Instantiate(prefab, parent, false);
            camera.name = "Kamera odrazů vody";
            foreach (var component in camera.GetComponentsInChildren<MonoBehaviour>())
            {
                var field = component.GetType().GetField("reflectionPlane");
                if (field != null && field.FieldType == typeof(Transform))
                {
                    field.SetValue(component, water);
                }
            }
        }

        /// <summary>Má prefab skript s polem reflectionPlane (kamera odrazů)?</summary>
        public static bool HasReflectionPlane(GameObject prefab)
        {
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var field = component != null ? component.GetType().GetField("reflectionPlane") : null;
                if (field != null && field.FieldType == typeof(Transform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Normálová mapa vlnek z navazujícího šumu (lineární, RGB = směr povrchu).</summary>
        public static Texture2D GeneratedNormalMap()
        {
            var heights = new float[NormalSize, NormalSize];
            for (var y = 0; y < NormalSize; y++)
            {
                for (var x = 0; x < NormalSize; x++)
                {
                    heights[y, x] = Mathf.Sin((x + 3 * Mathf.Sin(y * 0.3f)) * Mathf.PI * 2 / 16) * 0.5f
                        + Mathf.Sin((y + 2 * Mathf.Sin(x * 0.4f)) * Mathf.PI * 2 / 21.333f) * 0.5f
                        + GroundView.Hash01(x, y, 3) * 0.15f;
                }
            }

            var texture = new Texture2D(NormalSize, NormalSize, TextureFormat.RGBA32, true, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "Vlnky",
            };
            for (var y = 0; y < NormalSize; y++)
            {
                for (var x = 0; x < NormalSize; x++)
                {
                    var dx = heights[y, (x + 1) % NormalSize] - heights[y, (x + NormalSize - 1) % NormalSize];
                    var dy = heights[(y + 1) % NormalSize, x] - heights[(y + NormalSize - 1) % NormalSize, x];
                    var normal = new Vector3(-dx, -dy, 1f).normalized;
                    texture.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
                }
            }

            texture.Apply();
            return texture;
        }

        private void Update()
        {
            if (_material == null)
            {
                return;
            }

            // URP/Lit posouvá všechny textury podle _BaseMap, takže se hýbe i normálová mapa.
            var offset = _direction * (Time.time * _speed / Mathf.Max(0.5f, VisualCatalog.Current.WaveSize));
            _material.SetTextureOffset("_BaseMap", new Vector2(offset.x % 1f, offset.y % 1f));
        }
    }
}

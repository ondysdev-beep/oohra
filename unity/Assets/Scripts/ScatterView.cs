using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oohra.Client
{
    /// <summary>
    /// Okolí jen pro ozdobu: keře, kapradí, rákos, květiny, houby, kamínky, naplavené dřevo, ležící kmeny a obří
    /// skály na skalních hřbetech. Rozhází je klient ze seedu (čísla dlaždice), takže všichni vidí totéž a server
    /// o nich neví (nepřekáží, nedá se na ně kliknout). Model z katalogu vzhledu, jinak jednoduchý tvar z kódu.
    /// Kreslí se po stovkách naráz (GPU instancing) po čtvercích CellSize × CellSize, jen ty, které kamera vidí.
    /// Hustotu násobí GraphicsQuality.ScatterDensity (projeví se při dalším načtení ostrova).
    /// </summary>
    public class ScatterView : MonoBehaviour
    {
        private const int ChunkSize = 1023;
        private const int CellSize = 16;

        /// <summary>Druh ozdoby: kde roste (šance na dlaždici podle země), jak je velká a jak vypadá bez modelu.</summary>
        private sealed class Kind
        {
            public string Name;
            public GameObject[] Models;
            public float Height;
            public float SizeJitter = 0.3f;
            public System.Func<TileType, bool, float> Chance;
            public System.Func<List<Part>> Fallback;
        }

        /// <summary>Jedna síť jedné části modelu a všechny její kopie (po čtvercích).</summary>
        private sealed class Part
        {
            public Mesh Mesh;
            public int SubMesh;
            public Material Material;
            public Matrix4x4 Local = Matrix4x4.identity;
            public bool Shadows;
            public readonly Dictionary<int, List<Matrix4x4>> Cells = new Dictionary<int, List<Matrix4x4>>();
            public readonly List<KeyValuePair<RenderParams, Matrix4x4[]>> Chunks = new List<KeyValuePair<RenderParams, Matrix4x4[]>>();
        }

        private readonly List<Part> _parts = new List<Part>();
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private float _maxHeight = 1f;
        private WorldView _world;

        /// <summary>Kolik kusů daného druhu leží na ostrově (pro testy).</summary>
        public int CountOf(string kind)
        {
            int count;
            return _counts.TryGetValue(kind, out count) ? count : 0;
        }

        public int Total { get; private set; }

        public static ScatterView Build(WorldView world, Transform parent)
        {
            var go = new GameObject("Okolí");
            go.transform.SetParent(parent);
            var view = go.AddComponent<ScatterView>();
            view._world = world;
            view.Scatter(world, Kinds(), GraphicsQuality.ScatterDensity);
            GraphicsQuality.Changed += view.Rescatter;
            return view;
        }

        /// <summary>Po změně kvality (F2) rozhází ozdoby znovu s novou hustotou.</summary>
        private void Rescatter()
        {
            Clear();
            Scatter(_world, Kinds(), GraphicsQuality.ScatterDensity);
        }

        private void Clear()
        {
            foreach (var part in _parts)
            {
                if (part.Material != null) Destroy(part.Material);
            }

            _parts.Clear();
            _counts.Clear();
            Total = 0;
        }

        private void OnDestroy()
        {
            GraphicsQuality.Changed -= Rescatter;
        }

        /// <summary>Druhy ozdob: šance na dlaždici (podle druhu země a jestli jsou všude kolem skály).</summary>
        private static List<Kind> Kinds()
        {
            var catalog = VisualCatalog.Current;
            return new List<Kind>
            {
                new Kind { Name = "Keř", Models = catalog.Bushes, Height = 1.2f, Chance = (t, ridge) => t == TileType.Forest ? 0.05f : t == TileType.Grass ? 0.015f : t == TileType.Highland ? 0.01f : 0f, Fallback = () => Cluster("#4c6638") },
                new Kind { Name = "Kapradí", Models = catalog.Ferns, Height = 0.7f, Chance = (t, ridge) => t == TileType.Forest ? 0.09f : 0f, Fallback = () => Cluster("#58743c", 0.55f) },
                new Kind { Name = "Rákos", Models = catalog.Reeds, Height = 1.4f, Chance = (t, ridge) => t == TileType.Bog ? 0.18f : t == TileType.Mire ? 0.25f : 0f, Fallback = () => Stalks("#6d6a3c") },
                new Kind { Name = "Květina", Models = catalog.Flowers, Height = 0.35f, Chance = (t, ridge) => t == TileType.Grass ? 0.05f : 0f, Fallback = () => Flower() },
                new Kind { Name = "Houba", Models = catalog.Mushrooms, Height = 0.15f, Chance = (t, ridge) => t == TileType.Forest ? 0.02f : 0f, Fallback = () => Mushroom() },
                new Kind { Name = "Kamínek", Models = catalog.Pebbles, Height = 0.2f, Chance = (t, ridge) => t == TileType.Highland ? 0.08f : t == TileType.Sand ? 0.02f : 0f, Fallback = () => Blob("#8d887d", new Vector3(0.24f, 0.13f, 0.18f), PrimitiveType.Sphere) },
                new Kind { Name = "Naplavené dřevo", Models = catalog.Driftwood, Height = 0.3f, Chance = (t, ridge) => t == TileType.Sand ? 0.008f : 0f, Fallback = () => Log("#b7ad98", 1.6f, 0.14f) },
                new Kind { Name = "Ležící kmen", Models = catalog.FallenLogs, Height = 0.6f, Chance = (t, ridge) => t == TileType.Forest ? 0.005f : 0f, Fallback = () => Log("#4a3a2b", 4f, 0.5f) },
                new Kind { Name = "Obří skála", Models = catalog.BigRocks.Length > 0 ? catalog.BigRocks : catalog.Rocks, Height = 5f, SizeJitter = 0.8f, Chance = (t, ridge) => ridge ? 0.12f : 0f, Fallback = () => Blob("#6f6a60", new Vector3(5f, 4.5f, 6f), PrimitiveType.Cube) },
            };
        }

        private void Scatter(WorldView world, List<Kind> kinds, float density)
        {
            var trees = new HashSet<int>();
            foreach (var tree in world.Trees)
            {
                trees.Add(tree.TileIndex);
            }

            for (var k = 0; k < kinds.Count; k++)
            {
                var kind = kinds[k];
                var models = ModelsOf(kind);
                if (models.Count == 0)
                {
                    continue;
                }

                var count = 0;
                for (var i = 0; i < world.Tiles.Length; i++)
                {
                    var (tx, ty) = (i % world.Width, i / world.Width);
                    var chance = kind.Chance(world.Tiles[i], Ridge(world, tx, ty)) * density;
                    // Kolem kmene stromu malé ozdoby ne (obří skála stojí jen na skále, tam stromy nejsou).
                    if (chance <= 0f || GroundView.Hash01(i, k, 61) >= chance || (trees.Contains(i) && kind.Height < 3f))
                    {
                        continue;
                    }

                    var x = tx + 0.15 + GroundView.Hash01(i, k, 62) * 0.7;
                    var y = ty + 0.15 + GroundView.Hash01(i, k, 63) * 0.7;
                    var turn = Quaternion.Euler(0f, GroundView.Hash01(i, k, 64) * 360f, 0f);
                    var size = 1f + (GroundView.Hash01(i, k, 65) - 0.5f) * 2f * kind.SizeJitter;
                    // Obří skála se zaboří, ať nevisí nad svahem.
                    var sink = kind.Height >= 3f ? kind.Height * size * 0.25f : 0.02f;
                    var place = Matrix4x4.TRS(WorldCoords.ToUnity(x, y, -sink), turn, Vector3.one * size);
                    var model = models[(int)(GroundView.Hash01(i, k, 66) * models.Count) % models.Count];
                    var cell = (int)y / CellSize * 10000 + (int)x / CellSize;
                    foreach (var part in model)
                    {
                        List<Matrix4x4> matrices;
                        if (!part.Cells.TryGetValue(cell, out matrices))
                        {
                            matrices = new List<Matrix4x4>();
                            part.Cells[cell] = matrices;
                        }

                        matrices.Add(place * part.Local);
                    }

                    count++;
                }

                _counts[kind.Name] = count;
                Total += count;
                _maxHeight = Mathf.Max(_maxHeight, kind.Height * (1f + kind.SizeJitter));
                foreach (var model in models)
                {
                    // Stín vrhají jen větší ozdoby (keř, rákos, kmen, obří skála).
                    foreach (var part in model) part.Shadows = kind.Height >= 0.6f;
                    _parts.AddRange(model);
                }
            }

            foreach (var part in _parts)
            {
                foreach (var cell in part.Cells)
                {
                    var parameters = new RenderParams(part.Material)
                    {
                        shadowCastingMode = part.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        receiveShadows = true,
                        worldBounds = CellBounds(cell.Key),
                    };
                    for (var start = 0; start < cell.Value.Count; start += ChunkSize)
                    {
                        part.Chunks.Add(new KeyValuePair<RenderParams, Matrix4x4[]>(parameters, cell.Value.GetRange(start, Mathf.Min(ChunkSize, cell.Value.Count - start)).ToArray()));
                    }
                }

                part.Cells.Clear();
            }
        }

        /// <summary>Skalní hřbet: skála a všude kolem skála (tam stojí obří skály).</summary>
        private static bool Ridge(WorldView world, int x, int y)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (x + dx < 0 || y + dy < 0 || x + dx >= world.Width || y + dy >= world.Height
                        || world.TileAt(x + dx, y + dy) is not (TileType.Rock or TileType.LayeredRock))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Modely druhu (z katalogu srovnané na výšku, jinak tvar z kódu), každý jako seznam částí.</summary>
        private static List<List<Part>> ModelsOf(Kind kind)
        {
            var models = new List<List<Part>>();
            foreach (var prefab in kind.Models ?? new GameObject[0])
            {
                if (prefab != null)
                {
                    models.Add(PartsOf(prefab, kind.Height));
                }
            }

            if (models.Count == 0)
            {
                models.Add(kind.Fallback());
            }

            return models;
        }

        /// <summary>Části modelu z katalogu (síť, materiál s instancováním, poloha v modelu).</summary>
        private static List<Part> PartsOf(GameObject prefab, float height)
        {
            var parts = new List<Part>();
            var model = VisualCatalog.Spawn(prefab, null, "Vzor ozdoby", height: height);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                for (var i = 0; i < filter.sharedMesh.subMeshCount && i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        parts.Add(new Part
                        {
                            Mesh = filter.sharedMesh,
                            SubMesh = i,
                            Material = new Material(materials[i]) { enableInstancing = true, name = materials[i].name + " (okolí)" },
                            Local = filter.transform.localToWorldMatrix,
                        });
                    }
                }
            }

            if (Application.isPlaying) Destroy(model);
            else DestroyImmediate(model);
            return parts;
        }

        // ---------- tvary z kódu (když katalog model nemá) ----------

        private static Part Shape(PrimitiveType type, string color, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
            var material = new Material(Palette.Lit) { enableInstancing = true, name = "Okolí " + color };
            material.SetColor("_BaseColor", Palette.Hex(color));
            return new Part { Mesh = mesh, Material = material, Local = Matrix4x4.TRS(position, rotation, scale) };
        }

        /// <summary>Zploštělá koule nebo kostka (kamínek, obří skála), rozměry shape v metrech.</summary>
        private static List<Part> Blob(string color, Vector3 shape, PrimitiveType type = PrimitiveType.Sphere)
        {
            return new List<Part> { Shape(type, color, new Vector3(0f, shape.y / 2f, 0f), shape, Quaternion.Euler(0f, 0f, type == PrimitiveType.Cube ? 8f : 0f)) };
        }

        /// <summary>Shluk tří zploštělých koulí různé velikosti (keř, kapradí), výška 1 (flat = jak moc zploštělé).</summary>
        private static List<Part> Cluster(string color, float flat = 0.85f)
        {
            return new List<Part>
            {
                Shape(PrimitiveType.Sphere, color, new Vector3(0f, 0.45f * flat, 0f), new Vector3(0.9f, 0.9f * flat, 0.85f), Quaternion.identity),
                Shape(PrimitiveType.Sphere, color, new Vector3(0.35f, 0.32f * flat, 0.15f), new Vector3(0.6f, 0.62f * flat, 0.6f), Quaternion.identity),
                Shape(PrimitiveType.Sphere, color, new Vector3(-0.3f, 0.3f * flat, -0.2f), new Vector3(0.55f, 0.58f * flat, 0.55f), Quaternion.identity),
            };
        }

        /// <summary>Trs stébel (rákos): pět tenkých válců různě vysokých.</summary>
        private static List<Part> Stalks(string color)
        {
            var parts = new List<Part>();
            for (var i = 0; i < 5; i++)
            {
                var angle = i * 1.3f;
                var h = 0.9f + 0.1f * (i % 3);
                parts.Add(Shape(PrimitiveType.Cylinder, color, new Vector3(Mathf.Cos(angle) * 0.12f, h * 0.7f, Mathf.Sin(angle) * 0.12f), new Vector3(0.035f, h * 0.7f, 0.035f), Quaternion.Euler(4f * (i - 2), 0f, 3f * (i - 2))));
            }

            return parts;
        }

        /// <summary>Květina: stonek a barevný kvítek.</summary>
        private static List<Part> Flower()
        {
            return new List<Part>
            {
                Shape(PrimitiveType.Cylinder, "#4f6b33", new Vector3(0f, 0.15f, 0f), new Vector3(0.02f, 0.15f, 0.02f), Quaternion.identity),
                Shape(PrimitiveType.Sphere, "#b98bb8", new Vector3(0f, 0.32f, 0f), new Vector3(0.09f, 0.05f, 0.09f), Quaternion.identity),
            };
        }

        /// <summary>Houba: nožka a klobouk.</summary>
        private static List<Part> Mushroom()
        {
            return new List<Part>
            {
                Shape(PrimitiveType.Cylinder, "#d7cfbf", new Vector3(0f, 0.05f, 0f), new Vector3(0.04f, 0.05f, 0.04f), Quaternion.identity),
                Shape(PrimitiveType.Sphere, "#8a3b2a", new Vector3(0f, 0.11f, 0f), new Vector3(0.13f, 0.06f, 0.13f), Quaternion.identity),
            };
        }

        /// <summary>Ležící kus dřeva (naplavené dřevo, mrtvý kmen) délky length a tloušťky thickness.</summary>
        private static List<Part> Log(string color, float length, float thickness)
        {
            return new List<Part> { Shape(PrimitiveType.Cylinder, color, new Vector3(0f, thickness / 2f, 0f), new Vector3(thickness, length / 2f, thickness), Quaternion.Euler(0f, 0f, 90f)) };
        }

        /// <summary>Hranice čtverce i s kopci (výšky v pár bodech čtverce) a rezervou na výšku ozdob.</summary>
        private Bounds CellBounds(int cell)
        {
            var x = cell % 10000 * CellSize;
            var y = cell / 10000 * CellSize;
            float low = float.MaxValue, high = float.MinValue;
            for (var j = 0; j <= 4; j++)
            {
                for (var i = 0; i <= 4; i++)
                {
                    var g = WorldCoords.Ground(x + i * CellSize / 4f, y + j * CellSize / 4f);
                    low = Mathf.Min(low, g);
                    high = Mathf.Max(high, g);
                }
            }

            var bounds = new Bounds();
            var min = WorldCoords.ToUnityAt(x - 2, y + CellSize + 2, low - 2f);
            var max = WorldCoords.ToUnityAt(x + CellSize + 2, y - 2, high + _maxHeight * 2f + 2f);
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private void Update()
        {
            foreach (var part in _parts)
            {
                foreach (var chunk in part.Chunks)
                {
                    Graphics.RenderMeshInstanced(chunk.Key, part.Mesh, part.SubMesh, chunk.Value);
                }
            }
        }
    }
}

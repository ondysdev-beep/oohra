using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oohra.Client
{
    /// <summary>
    /// Tráva jen pro ozdobu: trsy z katalogu vzhledu (Grass) rozházené po travnatých dlaždicích (v lese řidší).
    /// Nejsou to rostliny, nedá se na ně kliknout a server o nich neví. Trsů jsou tisíce, proto nejsou jako
    /// samostatné objekty, ale kreslí se po stovkách naráz (GPU instancing, Graphics.RenderMeshInstanced).
    /// Trsy jsou rozdělené do čtverců CellSize × CellSize metrů a každý čtverec má své hranice, takže Unity kreslí
    /// jen čtverce, které kamera vidí (na ostrově 384 × 384 m je jich většina mimo obraz).
    /// </summary>
    public class GrassView : MonoBehaviour
    {
        // Kolik trsů jde kreslit jedním voláním (bezpečná hranice pro všechny grafické karty).
        private const int ChunkSize = 1023;

        // Velikost čtverce trávy (m) a jak vysoko tráva sahá (pro hranice čtverce).
        private const int CellSize = 16;
        private const float CellHeight = 3f;

        // V lese je tráva řidší, kolem kmene stromu žádná.
        private const float ForestDensity = 0.4f;
        private const float TrunkClearance = 0.35f;

        private readonly List<Batch> _batches = new List<Batch>();

        /// <summary>Kolik trsů je na ostrově (pro testy).</summary>
        public int Count { get; private set; }

        /// <summary>Kolik dávek (čtverec × část modelu) se kreslí (pro testy).</summary>
        public int ChunkCount
        {
            get
            {
                var count = 0;
                foreach (var batch in _batches) count += batch.Chunks.Count;
                return count;
            }
        }

        /// <summary>Jedna část modelu trávy (síť a materiál) a všechny její kopie na ostrově.</summary>
        private sealed class Batch
        {
            public Mesh Mesh;
            public int SubMesh;
            public RenderParams Params;
            public Matrix4x4 Part;
            public readonly Dictionary<int, List<Matrix4x4>> Cells = new Dictionary<int, List<Matrix4x4>>();
            public readonly List<Chunk> Chunks = new List<Chunk>();
        }

        /// <summary>Kopie jedné části modelu v jednom čtverci (nejvýš ChunkSize), s hranicemi čtverce.</summary>
        private sealed class Chunk
        {
            public RenderParams Params;
            public Matrix4x4[] Matrices;
        }

        /// <summary>Rozhází trávu. Když katalog žádnou trávu nemá, vrátí null.</summary>
        public static GrassView Build(WorldView world, Transform parent)
        {
            var catalog = VisualCatalog.Current;
            var models = new List<List<Batch>>();
            foreach (var prefab in catalog.Grass ?? new GameObject[0])
            {
                if (prefab != null)
                {
                    models.Add(Parts(prefab, catalog.GrassHeight));
                }
            }

            if (models.Count == 0)
            {
                return null;
            }

            var go = new GameObject("Tráva");
            go.transform.SetParent(parent);
            var view = go.AddComponent<GrassView>();
            foreach (var parts in models)
            {
                view._batches.AddRange(parts);
            }

            view.Scatter(world, models, catalog.GrassPerTile);
            return view;
        }

        /// <summary>
        /// Části modelu trávy: model se srovná (dole uprostřed, výška height) a pro každou síť se zapamatuje,
        /// kde v trsu leží. Materiály dostanou zapnuté instancování (kopie, původní soubor se nemění).
        /// </summary>
        private static List<Batch> Parts(GameObject prefab, float height)
        {
            var parts = new List<Batch>();
            var model = VisualCatalog.Spawn(prefab, null, "Vzor trávy", height: height);
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
                    if (materials[i] == null)
                    {
                        continue;
                    }

                    var material = new Material(materials[i]) { enableInstancing = true, name = materials[i].name + " (tráva)" };
                    parts.Add(new Batch
                    {
                        Mesh = filter.sharedMesh,
                        SubMesh = i,
                        Part = filter.transform.localToWorldMatrix,
                        Params = new RenderParams(material)
                        {
                            shadowCastingMode = ShadowCastingMode.Off,
                            receiveShadows = true,
                            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f),
                        },
                    });
                }
            }

            if (Application.isPlaying) Destroy(model);
            else DestroyImmediate(model);
            return parts;
        }

        /// <summary>Trsy po travnatých dlaždicích: počet, místo, natočení a velikost podle čísla dlaždice (pořád stejně).</summary>
        private void Scatter(WorldView world, List<List<Batch>> models, float perTile)
        {
            var trees = new HashSet<int>();
            foreach (var tree in world.Trees)
            {
                trees.Add(tree.TileIndex);
            }

            for (var i = 0; i < world.Tiles.Length; i++)
            {
                var tile = world.Tiles[i];
                if (tile != TileType.Grass && tile != TileType.Forest)
                {
                    continue;
                }

                var density = perTile * (tile == TileType.Forest ? ForestDensity : 1f);
                var count = (int)density + (GroundView.Hash01(i, 0, 21) < density - (int)density ? 1 : 0);
                for (var k = 0; k < count; k++)
                {
                    var x = i % world.Width + 0.05 + GroundView.Hash01(i, k * 4 + 1, 21) * 0.9;
                    var y = i / world.Width + 0.05 + GroundView.Hash01(i, k * 4 + 2, 21) * 0.9;
                    if (trees.Contains(i) && Mathf.Abs((float)(x % 1 - 0.5)) < TrunkClearance && Mathf.Abs((float)(y % 1 - 0.5)) < TrunkClearance)
                    {
                        continue;
                    }

                    var seed = GroundView.Hash01(i, k * 4 + 3, 21);
                    var rotation = Quaternion.Euler(0f, seed * 360f, 0f);
                    var scale = Vector3.one * (0.8f + 0.4f * GroundView.Hash01(i, k * 4 + 4, 21));
                    var place = Matrix4x4.TRS(WorldCoords.ToUnity(x, y), rotation, scale);
                    var cell = (int)y / CellSize * 10000 + (int)x / CellSize;
                    foreach (var part in models[(int)(seed * models.Count) % models.Count])
                    {
                        List<Matrix4x4> matrices;
                        if (!part.Cells.TryGetValue(cell, out matrices))
                        {
                            matrices = new List<Matrix4x4>();
                            part.Cells[cell] = matrices;
                        }

                        matrices.Add(place * part.Part);
                    }

                    Count++;
                }
            }

            foreach (var batch in _batches)
            {
                foreach (var cell in batch.Cells)
                {
                    var bounds = CellBounds(cell.Key);
                    for (var start = 0; start < cell.Value.Count; start += ChunkSize)
                    {
                        var parameters = batch.Params;
                        parameters.worldBounds = bounds;
                        batch.Chunks.Add(new Chunk
                        {
                            Params = parameters,
                            Matrices = cell.Value.GetRange(start, Mathf.Min(ChunkSize, cell.Value.Count - start)).ToArray(),
                        });
                    }
                }

                batch.Cells.Clear();
            }
        }

        /// <summary>Hranice čtverce v Unity (mapa y dolů = Unity -z), s rezervou na velikost trsu.</summary>
        private static Bounds CellBounds(int cell)
        {
            var x = cell % 10000 * CellSize;
            var y = cell / 10000 * CellSize;
            var min = WorldCoords.ToUnity(x - 1, y + CellSize + 1);
            var max = WorldCoords.ToUnity(x + CellSize + 1, y - 1);
            var bounds = new Bounds();
            // Trsy stojí na terénu (kopce): výška čtverce od nejnižšího po nejvyšší roh.
            var low = Mathf.Min(min.y, max.y, WorldCoords.Ground(x, y), WorldCoords.Ground(x + CellSize, y + CellSize));
            var high = Mathf.Max(min.y, max.y, WorldCoords.Ground(x, y), WorldCoords.Ground(x + CellSize, y + CellSize));
            bounds.SetMinMax(new Vector3(min.x, low - 1f, min.z), new Vector3(max.x, high + CellHeight + 2f, max.z));
            return bounds;
        }

        private void Update()
        {
            foreach (var batch in _batches)
            {
                foreach (var chunk in batch.Chunks)
                {
                    Graphics.RenderMeshInstanced(chunk.Params, batch.Mesh, batch.SubMesh, chunk.Matrices);
                }
            }
        }
    }
}

using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Zem ostrova jako Unity Terrain: souš rovně ve výšce 0, dno moře a jezírek pod hladinou, textury dlaždic
    /// se na hranách plynule míchají (GroundLayout). Textury jsou z katalogu vzhledu, prázdné políčko =
    /// textura vygenerovaná ze základní barvy dlaždice. Na skalách leží nízké kameny.
    /// </summary>
    public static class GroundView
    {
        // Vygenerované textury: velikost v pixelech a jak moc kolísá jas.
        private const int GeneratedSize = 64;
        private const float GeneratedVariation = 0.12f;

        // Kameny na skále: na kolika procentech skalních dlaždic leží kámen (velikost je v katalogu vzhledu).
        private const int RockChance = 45;

        public static Terrain Build(WorldView world, Transform parent)
        {
            var catalog = VisualCatalog.Current;
            var data = new TerrainData();
            data.heightmapResolution = GroundLayout.HeightResolution(Mathf.Max(world.Width, world.Height));
            data.size = new Vector3(world.Width, GroundLayout.Depth, world.Height);
            data.SetHeights(0, 0, GroundLayout.Heights(world, data.heightmapResolution));
            data.alphamapResolution = GroundLayout.AlphaResolution(Mathf.Max(world.Width, world.Height));
            data.terrainLayers = Layers(catalog);
            data.SetAlphamaps(0, 0, GroundLayout.Alphamaps(world, data.alphamapResolution));

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Zem";
            go.transform.SetParent(parent);
            // Terrain začíná v rohu: jih-západ mapy je v Unity (0, -výška mapy). Souš (výška 1) je pak v y = 0.
            go.transform.position = new Vector3(0f, -GroundLayout.Depth, -world.Height);
            RemoveCollider(go);

            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = Palette.Terrain;
            terrain.drawTreesAndFoliage = false;
            terrain.basemapDistance = 1000f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildRocks(world, parent, catalog);
            return terrain;
        }

        /// <summary>Vrstvy textur v pořadí GroundLayout (tráva, písek, les, skála, bažina).</summary>
        private static TerrainLayer[] Layers(VisualCatalog catalog)
        {
            var colors = new[]
            {
                Palette.Tile(TileType.Grass, false), Palette.Tile(TileType.Sand, false), Palette.Tile(TileType.Forest, false),
                Palette.Tile(TileType.Rock, false), Palette.Tile(TileType.Bog, false),
            };
            var layers = new TerrainLayer[GroundLayout.LayerCount];
            var tileSize = Mathf.Max(0.5f, catalog.GroundTextureSize);
            for (var i = 0; i < layers.Length; i++)
            {
                // Pozor: prázdné políčko katalogu není v Unity „opravdové“ null, proto != null, ne ??.
                var texture = catalog.GroundTexture(i);
                layers[i] = new TerrainLayer
                {
                    diffuseTexture = texture != null ? texture : GeneratedTexture(colors[i], i),
                    tileSize = new Vector2(tileSize, tileSize),
                    smoothness = 0f,
                };
            }

            return layers;
        }

        /// <summary>Textura ze základní barvy: jemný šum jasu (dvě velikosti skvrn), opakuje se bez švů.</summary>
        public static Texture2D GeneratedTexture(Color color, int seed)
        {
            var texture = new Texture2D(GeneratedSize, GeneratedSize, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "Zem " + seed,
            };
            for (var y = 0; y < GeneratedSize; y++)
            {
                for (var x = 0; x < GeneratedSize; x++)
                {
                    var noise = TileableNoise(x, y, 8, seed) * 0.65f + TileableNoise(x, y, 4, seed + 7) * 0.35f;
                    var light = 1f + (noise - 0.5f) * 2f * GeneratedVariation;
                    texture.SetPixel(x, y, new Color(color.r * light, color.g * light, color.b * light, 1f));
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>Hodnotový šum, který na krajích textury navazuje (mřížka cells × cells se opakuje).</summary>
        private static float TileableNoise(int x, int y, int cells, int seed)
        {
            var cellSize = (float)GeneratedSize / cells;
            var fx = x / cellSize;
            var fy = y / cellSize;
            var x0 = (int)fx;
            var y0 = (int)fy;
            var tx = Smooth(fx - x0);
            var ty = Smooth(fy - y0);
            var a = Mathf.Lerp(Hash01(x0 % cells, y0 % cells, seed), Hash01((x0 + 1) % cells, y0 % cells, seed), tx);
            var b = Mathf.Lerp(Hash01(x0 % cells, (y0 + 1) % cells, seed), Hash01((x0 + 1) % cells, (y0 + 1) % cells, seed), tx);
            return Mathf.Lerp(a, b, ty);
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>Pseudonáhodné číslo 0 až 1 z celých čísel (jen pro vzhled, ne pro pravidla světa).</summary>
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Nízké kameny na skalních dlaždicích (z katalogu, jinak šedé zploštělé koule).</summary>
        private static void BuildRocks(WorldView world, Transform parent, VisualCatalog catalog)
        {
            var rocks = new GameObject("Kameny").transform;
            rocks.SetParent(parent);
            var models = catalog.Rocks ?? new GameObject[0];
            for (var i = 0; i < world.Tiles.Length; i++)
            {
                if (world.Tiles[i] != TileType.Rock || Hash01(i, 0, 11) * 100 >= RockChance)
                {
                    continue;
                }

                var x = i % world.Width + 0.2 + Hash01(i, 1, 11) * 0.6;
                var y = i / world.Width + 0.2 + Hash01(i, 2, 11) * 0.6;
                var size = Mathf.Lerp(catalog.RockMinSize, catalog.RockMaxSize, Hash01(i, 3, 11));
                var rotation = Quaternion.Euler(0f, Hash01(i, 4, 11) * 360f, 0f);
                GameObject rock;
                if (models.Length > 0 && models[i % models.Length] != null)
                {
                    rock = VisualCatalog.Spawn(models[i % models.Length], rocks, "Kámen", largest: size);
                    rock.transform.position = WorldCoords.ToUnity(x, y);
                    rock.transform.rotation = rotation;
                }
                else
                {
                    var grey = Palette.Tile(TileType.Rock, false) * Mathf.Lerp(0.75f, 1.05f, Hash01(i, 5, 11));
                    grey.a = 1f;
                    rock = IslandView.Primitive(PrimitiveType.Sphere, "Kámen", rocks, grey);
                    rock.transform.position = WorldCoords.ToUnity(x, y, size * 0.15f);
                    rock.transform.rotation = rotation;
                    rock.transform.localScale = new Vector3(size, size * 0.55f, size * 0.8f);
                }
            }
        }

        private static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<TerrainCollider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
    }
}

using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Postaví šedou krabici ostrova: zem jako jedna plocha s texturou dlaždic,
    /// stromy, rostliny, tábory a ruiny z jednoduchých tvarů. Kolize počítá Core, ne fyzika Unity.
    /// </summary>
    public static class IslandView
    {
        public static GameObject Build(GameState state)
        {
            var island = state.Island;
            var root = new GameObject("Ostrov (vygenerovaný)");
            BuildSea(island, root.transform);
            BuildGround(island, root.transform);
            BuildTrees(island, root.transform);
            BuildPlants(state, root.transform);
            BuildCamps(island, root.transform);
            BuildRuins(island, root.transform);
            return root;
        }

        /// <summary>Zem: jedna čtyřhranná plocha, každý pixel textury je jedna dlaždice.</summary>
        private static void BuildGround(Island island, Transform parent)
        {
            var texture = new Texture2D(island.Width, island.Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < island.Height; y++)
            {
                for (var x = 0; x < island.Width; x++)
                {
                    var index = island.Index(x, y);
                    // Řádek 0 textury je dole (na jihu), řádek 0 mapy nahoře (na severu).
                    texture.SetPixel(x, island.Height - 1 - y, Palette.Tile(island.Tiles[index], island.Shore[index]));
                }
            }

            texture.Apply();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Zem";
            ground.transform.SetParent(parent);
            ground.transform.position = new Vector3(island.Width / 2f, 0f, -island.Height / 2f);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(island.Width, island.Height, 1f);
            Object.Destroy(ground.GetComponent<Collider>());

            var material = new Material(Palette.Lit);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void BuildSea(Island island, Transform parent)
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sea.name = "Moře";
            sea.transform.SetParent(parent);
            sea.transform.position = new Vector3(island.Width / 2f, -0.02f, -island.Height / 2f);
            sea.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            sea.transform.localScale = new Vector3(island.Width * 6f, island.Height * 6f, 1f);
            Object.Destroy(sea.GetComponent<Collider>());
            Palette.SetColor(sea, Palette.Tile(TileType.Sea, false));
        }

        private static void BuildTrees(Island island, Transform parent)
        {
            var trees = new GameObject("Stromy").transform;
            trees.SetParent(parent);
            var trunkColor = Palette.Hex("#5a4331");
            var crownColors = new[] { Palette.Hex("#4e7d3a"), Palette.Hex("#3d6b30") };

            foreach (var tree in island.Trees)
            {
                var size = 0.8f + (float)tree.Variant * 0.4f;
                var trunk = Primitive(PrimitiveType.Cylinder, "Kmen", trees, trunkColor);
                trunk.transform.position = WorldCoords.ToUnity(tree.X, tree.Y, 0.35f * size);
                trunk.transform.localScale = new Vector3(0.15f, 0.35f * size, 0.15f);

                var crown = Primitive(PrimitiveType.Sphere, "Koruna", trunk.transform.parent, crownColors[tree.Variant > 0.5 ? 0 : 1]);
                crown.transform.position = WorldCoords.ToUnity(tree.X, tree.Y, 0.9f * size);
                crown.transform.localScale = Vector3.one * 0.85f * size;
            }
        }

        /// <summary>
        /// Rostliny: tvar podle druhu (koule, kapsle, kostka, válec), barva listů podle druhu.
        /// Každá má schovaný plod, který PlantViews ukáže, když rostlina plodí.
        /// </summary>
        private static void BuildPlants(GameState state, Transform parent)
        {
            var island = state.Island;
            var root = new GameObject("Rostliny");
            root.transform.SetParent(parent);
            var shapes = new[] { PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cube, PrimitiveType.Cylinder };
            var plants = new GameObject[island.Plants.Count];
            var fruits = new GameObject[island.Plants.Count];

            for (var i = 0; i < island.Plants.Count; i++)
            {
                var plant = island.Plants[i];
                var species = island.Species[plant.SpeciesId];
                var color = Palette.Hsl(species.Hue, species.Saturation, species.Lightness);
                var go = Primitive(shapes[species.Shape], "Rostlina " + plant.Id, root.transform, color);
                go.transform.position = WorldCoords.ToUnity(plant.X, plant.Y, 0.18f);
                go.transform.localScale = Vector3.one * 0.32f;

                var fruit = Primitive(PrimitiveType.Sphere, "Plod", go.transform, color);
                fruit.transform.position = WorldCoords.ToUnity(plant.X + 0.12, plant.Y, 0.42f);
                fruit.transform.localScale = Vector3.one * 0.45f;
                fruit.SetActive(false);

                plants[i] = go;
                fruits[i] = fruit;
            }

            root.AddComponent<PlantViews>().Init(state, plants, fruits);
        }

        private static void BuildCamps(Island island, Transform parent)
        {
            for (var i = 0; i < island.Camps.Count; i++)
            {
                var camp = island.Camps[i];
                var hearth = Primitive(PrimitiveType.Cylinder, "Tábor kmene " + (i + 1), parent, Palette.Hex("#3a2e24"));
                hearth.transform.position = WorldCoords.ToUnity(camp.X + 0.5, camp.Y + 0.5, 0.05f);
                hearth.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);

                var fire = Primitive(PrimitiveType.Sphere, "Oheň", hearth.transform, Palette.Hex("#e8903a"));
                fire.transform.position = WorldCoords.ToUnity(camp.X + 0.5, camp.Y + 0.5, 0.25f);
                fire.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
            }
        }

        private static void BuildRuins(Island island, Transform parent)
        {
            foreach (var ruin in island.RuinSpots)
            {
                var stone = Primitive(PrimitiveType.Cube, "Stará rytina", parent, Palette.Hex("#8b8478"));
                stone.transform.position = WorldCoords.ToUnity(ruin.X, ruin.Y, 0.3f);
                stone.transform.localScale = new Vector3(0.6f, 0.6f, 0.2f);
            }
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            Object.Destroy(go.GetComponent<Collider>());
            Palette.SetColor(go, color);
            return go;
        }
    }
}

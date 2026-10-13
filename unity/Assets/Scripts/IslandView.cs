using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Postaví šedou krabici ostrova z mapy od serveru: zem jako jedna plocha s texturou dlaždic,
    /// stromy, rostliny, tábory a ruiny z jednoduchých tvarů. Kolize počítá server, ne fyzika Unity.
    /// </summary>
    public static class IslandView
    {
        public static GameObject Build(WorldView world, GameConnection connection)
        {
            var root = new GameObject("Ostrov (vygenerovaný)");
            BuildSea(world, root.transform);
            BuildGround(world, root.transform);
            BuildTrees(world, root.transform);
            BuildPlants(world, connection, root.transform);
            BuildCamps(world, root.transform);
            BuildRuins(world, root.transform);
            return root;
        }

        /// <summary>Zem: jedna čtyřhranná plocha, každý pixel textury je jedna dlaždice.</summary>
        private static void BuildGround(WorldView world, Transform parent)
        {
            var texture = new Texture2D(world.Width, world.Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    var index = y * world.Width + x;
                    // Řádek 0 textury je dole (na jihu), řádek 0 mapy nahoře (na severu).
                    texture.SetPixel(x, world.Height - 1 - y, Palette.Tile(world.Tiles[index], world.Shore[index]));
                }
            }

            texture.Apply();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Zem";
            ground.transform.SetParent(parent);
            ground.transform.position = new Vector3(world.Width / 2f, 0f, -world.Height / 2f);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(world.Width, world.Height, 1f);
            Object.Destroy(ground.GetComponent<Collider>());

            var material = new Material(Palette.Lit);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void BuildSea(WorldView world, Transform parent)
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sea.name = "Moře";
            sea.transform.SetParent(parent);
            sea.transform.position = new Vector3(world.Width / 2f, -0.02f, -world.Height / 2f);
            sea.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            sea.transform.localScale = new Vector3(world.Width * 6f, world.Height * 6f, 1f);
            Object.Destroy(sea.GetComponent<Collider>());
            Palette.SetColor(sea, Palette.Tile(TileType.Sea, false));
        }

        private static void BuildTrees(WorldView world, Transform parent)
        {
            var trees = new GameObject("Stromy").transform;
            trees.SetParent(parent);
            var trunkColor = Palette.Hex("#5a4331");
            var crownColors = new[] { Palette.Hex("#4e7d3a"), Palette.Hex("#3d6b30") };

            foreach (var tree in world.Trees)
            {
                var x = tree.TileIndex % world.Width + 0.5;
                var y = tree.TileIndex / world.Width + 0.5;
                var size = 0.8f + tree.Variant * 0.4f;
                var trunk = Primitive(PrimitiveType.Cylinder, "Kmen", trees, trunkColor);
                trunk.transform.position = WorldCoords.ToUnity(x, y, 0.35f * size);
                trunk.transform.localScale = new Vector3(0.15f, 0.35f * size, 0.15f);

                var crown = Primitive(PrimitiveType.Sphere, "Koruna", trees, crownColors[tree.Variant > 0.5f ? 0 : 1]);
                crown.transform.position = WorldCoords.ToUnity(x, y, 0.9f * size);
                crown.transform.localScale = Vector3.one * 0.85f * size;
            }
        }

        /// <summary>
        /// Rostliny: tvar podle druhu (koule, kapsle, kostka, válec), barva listů podle druhu.
        /// Každá má schovaný plod, který PlantViews ukáže, když server řekne, že rostlina plodí.
        /// </summary>
        private static void BuildPlants(WorldView world, GameConnection connection, Transform parent)
        {
            var root = new GameObject("Rostliny");
            root.transform.SetParent(parent);
            var shapes = new[] { PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cube, PrimitiveType.Cylinder };
            var plants = new GameObject[world.Plants.Length];
            var fruits = new GameObject[world.Plants.Length];

            for (var i = 0; i < world.Plants.Length; i++)
            {
                var plant = world.Plants[i];
                var species = world.Species[plant.SpeciesId];
                var color = Palette.Leaf(species);
                var go = Primitive(shapes[species.Shape % shapes.Length], "Rostlina " + i, root.transform, color);
                go.transform.position = WorldCoords.ToUnity(plant.X, plant.Y, 0.18f);
                go.transform.localScale = Vector3.one * 0.32f;

                var fruit = Primitive(PrimitiveType.Sphere, "Plod", go.transform, color);
                fruit.transform.position = WorldCoords.ToUnity(plant.X + 0.12, plant.Y, 0.42f);
                fruit.transform.localScale = Vector3.one * 0.45f;
                fruit.SetActive(false);

                plants[i] = go;
                fruits[i] = fruit;
            }

            root.AddComponent<PlantViews>().Init(world, connection, plants, fruits);
        }

        private static void BuildCamps(WorldView world, Transform parent)
        {
            for (var i = 0; i < world.Camps.Length; i++)
            {
                var camp = world.Camps[i];
                var hearth = Primitive(PrimitiveType.Cylinder, "Tábor kmene " + (i + 1), parent, Palette.Hex("#3a2e24"));
                hearth.transform.position = WorldCoords.ToUnity(camp.X + 0.5, camp.Y + 0.5, 0.05f);
                hearth.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);

                var fire = Primitive(PrimitiveType.Sphere, "Oheň", hearth.transform, Palette.Hex("#e8903a"));
                fire.transform.position = WorldCoords.ToUnity(camp.X + 0.5, camp.Y + 0.5, 0.25f);
                fire.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
            }
        }

        private static void BuildRuins(WorldView world, Transform parent)
        {
            foreach (var ruin in world.Ruins)
            {
                var stone = Primitive(PrimitiveType.Cube, "Stará rytina", parent, Palette.Hex("#8b8478"));
                stone.transform.position = WorldCoords.ToUnity(ruin.X, ruin.Y, 0.3f);
                stone.transform.localScale = new Vector3(0.6f, 0.6f, 0.2f);
            }
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color)
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

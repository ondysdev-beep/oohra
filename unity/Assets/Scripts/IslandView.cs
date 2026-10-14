using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Postaví ostrov z mapy od serveru: zem (GroundView), vodu (WaterView), stromy, rostliny, tábory a ruiny
    /// (vlastní modely z katalogu vzhledu, jinak jednoduché tvary). Kolize počítá server, ne fyzika Unity.
    /// </summary>
    public static class IslandView
    {
        // Výška kmene a průměr koruny průměrného stromu v metrech (1 dlaždice = 1 metr).
        public const float TrunkHeight = 2.8f;
        public const float CrownSize = 2.6f;

        public static GameObject Build(WorldView world, GameConnection connection)
        {
            var root = new GameObject("Ostrov (vygenerovaný)");
            var seeThrough = root.AddComponent<SeeThrough>();
            WaterView.Build(world, root.transform);
            GroundView.Build(world, root.transform);
            FoamView.Build(world, root.transform);
            GrassView.Build(world, root.transform);

            var rocks = new GameObject("Skály");
            rocks.transform.SetParent(root.transform);
            rocks.AddComponent<RockPileViews>().Init(connection, world);
            BuildTrees(world, root.transform, seeThrough);
            BuildPlants(world, connection, root.transform);
            BuildCamps(world, root.transform);
            BuildRuins(world, root.transform);

            var fires = new GameObject("Ohně");
            fires.transform.SetParent(root.transform);
            fires.AddComponent<FireViews>().Init(connection, world);

            var animals = new GameObject("Zvířata");
            animals.transform.SetParent(root.transform);
            animals.AddComponent<AnimalViews>().Init(connection);

            var structures = new GameObject("Stavby");
            structures.transform.SetParent(root.transform);
            structures.AddComponent<StructureViews>().Init(connection, seeThrough);
            return root;
        }

        private static void BuildTrees(WorldView world, Transform parent, SeeThrough crowns)
        {
            var trees = new GameObject("Stromy").transform;
            trees.SetParent(parent);
            var trunkColor = Palette.Hex("#5a4331");
            var crownColors = new[] { Palette.Hex("#4e7d3a"), Palette.Hex("#3d6b30") };

            foreach (var tree in world.Trees)
            {
                var x = tree.TileIndex % world.Width + 0.5;
                var y = tree.TileIndex / world.Width + 0.5;
                // 1 dlaždice = 1 metr. Strom je vysoký asi 5 až 7 m, koruna začíná nad hlavou postavy (1,8 m).
                var size = 0.8f + tree.Variant * 0.4f;
                var prefab = VisualCatalog.Current.Tree(tree.Variant);
                if (prefab != null)
                {
                    AddTreeModel(prefab, trees, crowns, tree.TileIndex, x, y, size);
                    Pickable.Attach(trees.GetChild(trees.childCount - 1).gameObject, TreeTarget(world, tree));
                    continue;
                }

                // Kmen má stejnou tloušťku jako kolize na serveru (treeTrunkRadius 0,12 v movement.json).
                var trunk = Primitive(PrimitiveType.Cylinder, "Kmen", trees, trunkColor);
                trunk.transform.position = WorldCoords.ToUnity(x, y, TrunkHeight / 2 * size);
                trunk.transform.localScale = new Vector3(0.24f, TrunkHeight / 2 * size, 0.24f);

                var color = crownColors[tree.Variant > 0.5f ? 0 : 1];
                var crown = Primitive(PrimitiveType.Sphere, "Koruna", trees, color);
                var crownCenter = (TrunkHeight + CrownSize / 2 - 0.4f) * size;
                var crownRadius = CrownSize / 2 * size;
                crown.transform.position = WorldCoords.ToUnity(x, y, crownCenter);
                crown.transform.localScale = Vector3.one * CrownSize * size;
                crowns.Add(tree.TileIndex, crown, color, x, y, crownRadius, crownCenter - crownRadius, crownCenter + crownRadius);
                Pickable.Attach(trunk, TreeTarget(world, tree));
                Pickable.Attach(crown, TreeTarget(world, tree));
            }
        }

        /// <summary>Strom jako cíl kliknutí: dlaždice stromu (nasbírat klacky).</summary>
        private static InteractionTarget TreeTarget(WorldView world, TreeView tree)
        {
            return InteractionTarget.ForTile(TargetKind.Tree, tree.TileIndex % world.Width, tree.TileIndex / world.Width);
        }

        /// <summary>
        /// Strom z katalogu vzhledu: natočený podle dlaždice (ať nejsou všechny stejně), zmenšený nebo zvětšený
        /// podle varianty. Zprůhlední celý, když zakrývá postavu (koruna začíná zhruba ve 40 % výšky).
        /// </summary>
        private static void AddTreeModel(GameObject prefab, Transform parent, SeeThrough crowns, int tileIndex, double x, double y, float size)
        {
            var model = VisualCatalog.Spawn(prefab, parent, "Strom", height: VisualCatalog.Current.TreeHeight);
            model.transform.position = WorldCoords.ToUnity(x, y);
            model.transform.rotation = Quaternion.Euler(0f, tileIndex * 137 % 360, 0f);
            model.transform.localScale *= size;
            var bounds = VisualCatalog.BoundsOf(model);
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            crowns.Add(tileIndex, model, null, x, y, radius, bounds.max.y * 0.4f, bounds.max.y);
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
                var prefab = VisualCatalog.Current.Plant(species.Shape);
                GameObject go;
                if (prefab != null)
                {
                    // Vlastní model: obarví se barvou druhu, podle ní hráč druhy rozezná.
                    var height = VisualCatalog.Current.PlantHeight * (0.85f + 0.3f * GroundView.Hash01(i, 0, 5));
                    go = VisualCatalog.Spawn(prefab, root.transform, "Rostlina " + i, height: height);
                    go.transform.position = WorldCoords.ToUnity(plant.X, plant.Y);
                    go.transform.rotation = Quaternion.Euler(0f, i * 97 % 360, 0f);
                    SpeciesTint.Apply(go, color);
                }
                else
                {
                    go = Primitive(shapes[species.Shape % shapes.Length], "Rostlina " + i, root.transform, color);
                    go.transform.position = WorldCoords.ToUnity(plant.X, plant.Y, 0.18f);
                    go.transform.localScale = Vector3.one * 0.32f;
                }

                // Plod: tam, kde má model část „Plod“, jinak kousek vedle a nad zemí. Ve světě má 14 cm.
                var spot = FindDeep(go.transform, "Plod");
                var fruit = Primitive(PrimitiveType.Sphere, "Plod", go.transform, color);
                fruit.transform.position = spot != null ? spot.position : WorldCoords.ToUnity(plant.X + 0.12, plant.Y, 0.42f);
                fruit.transform.localScale = Vector3.one * (0.144f / go.transform.lossyScale.x);
                fruit.SetActive(false);

                Pickable.Attach(go, InteractionTarget.ForPlant(i, plant.X, plant.Y));
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
                FireViews.CreateHearth(parent, camp.X + 0.5, camp.Y + 0.5, "Tábor kmene " + (i + 1), Palette.Hex("#3a2e24"));
            }

            // Plameny a světla táborů přidá FireViews.
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

        /// <summary>Část modelu podle jména, hledá se v celém modelu (i hluboko), nebo null.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            // Kolize počítá server. Mimo hru (testy v editoru) jde mazat jen hned.
            if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            Palette.SetColor(go, color);
            return go;
        }
    }
}

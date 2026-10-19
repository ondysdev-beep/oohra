using NUnit.Framework;
using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Katalog vzhledu: vlastní modely místo tvarů z kódu, zprůhlednění a obarvení podle druhu.</summary>
    public class VisualCatalogTests
    {
        [TearDown]
        public void ResetCatalog()
        {
            VisualCatalog.Current = null;
        }

        [Test]
        public void CatalogFileExists()
        {
            Assert.IsNotNull(Resources.Load<VisualCatalog>(VisualCatalog.Resource), "Chybí Assets/Resources/VisualCatalog.asset (menu Oohra → Katalog vzhledu).");
        }

        /// <summary>Každý model v katalogu má viditelné části a materiály s funkčním shaderem (jinak by byl růžový).</summary>
        [Test]
        public void CatalogModelsHaveWorkingMaterials()
        {
            var catalog = Resources.Load<VisualCatalog>(VisualCatalog.Resource);
            var models = new System.Collections.Generic.List<GameObject>();
            models.AddRange(catalog.Trees);
            models.AddRange(catalog.OakTrees);
            models.AddRange(catalog.ConiferTrees);
            models.AddRange(catalog.BirchTrees);
            models.AddRange(catalog.WillowTrees);
            models.AddRange(catalog.DeadTrees);
            models.AddRange(catalog.Stumps);
            models.AddRange(catalog.Saplings);
            models.AddRange(catalog.Plants);
            models.AddRange(new[] { catalog.Wall, catalog.DoorFrame, catalog.Door, catalog.Trap, catalog.Stockpile });
            models.AddRange(new[] { catalog.Boar, catalog.Rabbit, catalog.Deer, catalog.Wolf });
            models.AddRange(catalog.Rocks);
            models.AddRange(catalog.Grass);
            models.AddRange(new[] { catalog.Player, catalog.PlayerAncient, catalog.PlayerThird, catalog.Campfire, catalog.Flame });
            models.AddRange(new[] { catalog.Log, catalog.LogPiece, catalog.Torch, catalog.SmallBag, catalog.LargeBag, catalog.Snake, catalog.WaspNest });
            models.AddRange(new[] { catalog.Barricade, catalog.Post, catalog.Floor, catalog.Roof, catalog.Ladder, catalog.MeatStrip });
            models.AddRange(new[] { catalog.CaveEntrance, catalog.HiddenCaveEntrance, catalog.CaveExit });
            foreach (var decoration in catalog.Decorations)
            {
                models.Add(decoration.Prefab);
            }
            foreach (var model in models)
            {
                if (model == null)
                {
                    continue;
                }

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers, model.name + ": model nemá žádnou viditelnou část.");
                foreach (var renderer in renderers)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, model.name + ": část " + renderer.name + " nemá materiál.");
                        Assert.IsTrue(material.shader.isSupported && material.shader.name != "Hidden/InternalErrorShader",
                            model.name + ": materiál " + material.name + " má nefunkční shader (použij URP Lit).");
                    }
                }
            }
        }

        /// <summary>Stromy z katalogu (podle druhu) mají v materiálu barevnou texturu, jinak by byly jednobarevné.</summary>
        [Test]
        public void CatalogTreesHaveColorTextures()
        {
            var catalog = Resources.Load<VisualCatalog>(VisualCatalog.Resource);
            var trees = new System.Collections.Generic.List<GameObject>();
            trees.AddRange(catalog.OakTrees);
            trees.AddRange(catalog.ConiferTrees);
            trees.AddRange(catalog.BirchTrees);
            trees.AddRange(catalog.WillowTrees);
            trees.AddRange(catalog.DeadTrees);
            foreach (var tree in trees)
            {
                if (tree == null)
                {
                    continue;
                }

                foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material.GetTexture("_BaseMap"), tree.name + ": materiál " + material.name + " nemá Base Map (barevnou texturu).");
                    }
                }
            }
        }

        [Test]
        public void EmptySlotUsesShapesFromCode()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var wall = StructureViews.CreateModel(BuildKind.StickWall, null);

            Assert.IsNotNull(wall.transform.Find("Zeď"));
            Object.DestroyImmediate(wall);
        }

        /// <summary>Had a vosí hnízdo mají svá políčka v katalogu (modely creature_snake a creature_wasp_nest).</summary>
        [Test]
        public void SnakeAndWaspNestHaveSlots()
        {
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            catalog.Snake = new GameObject("had");
            catalog.Wolf = new GameObject("vlk");

            Assert.AreSame(catalog.Snake, catalog.Animal(AnimalKind.Snake));
            Assert.AreSame(catalog.Wolf, catalog.Animal(AnimalKind.Wolf));
            Assert.IsNull(catalog.WaspNest);
            Object.DestroyImmediate(catalog.Snake);
            Object.DestroyImmediate(catalog.Wolf);
        }

        /// <summary>Zeď z katalogu se zkrátí podél osy X, dveře dostanou pant (DoorSwing jím otáčí).</summary>
        [Test]
        public void WallIsStretchedAndDoorGetsHinge()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            catalog.Wall = template;
            catalog.Door = template;
            VisualCatalog.Current = catalog;

            var wall = StructureViews.CreateModel(BuildKind.StickWall, null, 0.5f);
            Assert.AreEqual(0.5f / StructureViews.WallLength, wall.transform.GetChild(0).localScale.x, 1e-5f);

            var door = StructureViews.CreateModel(BuildKind.Door, null);
            var hinge = door.transform.Find("Pant");
            Assert.IsNotNull(hinge);
            Assert.AreEqual(-StructureViews.OpeningWidth / 2, hinge.localPosition.x, 1e-5f);
            Assert.IsNotNull(hinge.Find("Křídlo"));

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(door);
            Object.DestroyImmediate(template);
        }

        /// <summary>
        /// Model posunutý v prefabu (střed jinde, než je model) a jinak velký se srovná: stojí dole uprostřed
        /// a má požadovanou výšku nebo největší rozměr.
        /// </summary>
        [Test]
        public void SpawnCentersAndFitsModel()
        {
            var prefab = new GameObject("Posunutý strom");
            prefab.transform.position = new Vector3(60f, 0f, -50f);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(prefab.transform, false);
            mesh.transform.localPosition = new Vector3(3f, 1f, 2f);
            mesh.transform.localScale = new Vector3(1f, 2f, 1f);

            var tree = VisualCatalog.Spawn(prefab, null, "Strom", height: 5f);
            var bounds = VisualCatalog.BoundsOf(tree);
            Assert.AreEqual(0f, bounds.center.x, 1e-4f);
            Assert.AreEqual(0f, bounds.center.z, 1e-4f);
            Assert.AreEqual(0f, bounds.min.y, 1e-4f);
            Assert.AreEqual(5f, bounds.size.y, 1e-4f);

            var rock = VisualCatalog.Spawn(prefab, null, "Kámen", largest: 0.4f);
            Assert.AreEqual(0.4f, VisualCatalog.BoundsOf(rock).size.y, 1e-4f);

            // Plamen si nechá svůj střed (center: false).
            var flame = VisualCatalog.Spawn(prefab, null, "Plamen", center: false);
            Assert.AreEqual(3f, VisualCatalog.BoundsOf(flame).center.x, 1e-4f);

            Object.DestroyImmediate(tree);
            Object.DestroyImmediate(rock);
            Object.DestroyImmediate(flame);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void TreeVariantPicksModel()
        {
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            Assert.IsNull(catalog.Tree(Oohra.Core.World.TreeKind.Oak, 0.5f));

            var first = new GameObject("A");
            var second = new GameObject("B");
            catalog.Trees = new[] { first, second };
            Assert.AreSame(first, catalog.Tree(Oohra.Core.World.TreeKind.Oak, 0.1f));
            Assert.AreSame(second, catalog.Tree(Oohra.Core.World.TreeKind.Oak, 0.9f));
            Assert.AreSame(second, catalog.Tree(Oohra.Core.World.TreeKind.Oak, 1f));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        /// <summary>Druh s vlastním políčkem použije svůj model, druh bez něj společný seznam Trees.</summary>
        [Test]
        public void TreeSpeciesSlotWinsOverCommonList()
        {
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            var common = new GameObject("Společný");
            var pine = new GameObject("Borovice");
            catalog.Trees = new[] { common };
            catalog.ConiferTrees = new GameObject[] { null, pine };

            Assert.AreSame(pine, catalog.Tree(Oohra.Core.World.TreeKind.Conifer, 0.3f));
            Assert.AreSame(common, catalog.Tree(Oohra.Core.World.TreeKind.Willow, 0.3f));

            Object.DestroyImmediate(common);
            Object.DestroyImmediate(pine);
        }

        /// <summary>Zprůhlednění vlastního modelu nesmí smazat jeho materiál: po zneprůhlednění je zpátky původní.</summary>
        [Test]
        public void FadingKeepsOwnMaterials()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var original = new Material(Palette.Lit) { name = "Kůra" };
            original.SetColor("_BaseColor", Color.red);
            var renderer = model.GetComponent<Renderer>();
            renderer.sharedMaterial = original;
            var fade = new FadeTarget(model, null);

            fade.SetAlpha(0.3f);
            Assert.AreNotSame(original, renderer.sharedMaterial);
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, renderer.sharedMaterial.renderQueue);

            fade.SetAlpha(1f);
            Assert.AreSame(original, renderer.sharedMaterial);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            Assert.AreEqual(Color.red, block.GetColor("_BaseColor"));

            Object.DestroyImmediate(model);
        }

        /// <summary>Rostlina se obarví barvou druhu: jen části se SpeciesTint, když nějaké jsou.</summary>
        [Test]
        public void SpeciesTintColorsMarkedParts()
        {
            var plant = new GameObject("Rostlina");
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.transform.SetParent(plant.transform);
            var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.transform.SetParent(plant.transform);
            leaves.AddComponent<SpeciesTint>();

            SpeciesTint.Apply(plant, Color.green);

            var block = new MaterialPropertyBlock();
            leaves.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.AreEqual(Color.green, block.GetColor("_BaseColor"));
            stem.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.IsTrue(block.isEmpty);

            Object.DestroyImmediate(plant);
        }
    }
}

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
            models.AddRange(catalog.Plants);
            models.AddRange(new[] { catalog.Wall, catalog.DoorFrame, catalog.Door, catalog.Trap, catalog.Stockpile });
            models.AddRange(new[] { catalog.Boar, catalog.Rabbit, catalog.Deer, catalog.Wolf });
            models.AddRange(catalog.Rocks);
            models.AddRange(new[] { catalog.Player, catalog.Campfire, catalog.Flame });
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

        [Test]
        public void EmptySlotUsesShapesFromCode()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var wall = StructureViews.CreateModel(BuildKind.StickWall, null);

            Assert.IsNotNull(wall.transform.Find("Zeď"));
            Object.DestroyImmediate(wall);
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
            Assert.AreEqual(0.5f, wall.transform.GetChild(0).localScale.x, 1e-5f);

            var door = StructureViews.CreateModel(BuildKind.Door, null);
            var hinge = door.transform.Find("Pant");
            Assert.IsNotNull(hinge);
            Assert.AreEqual(-StructureViews.OpeningWidth / 2, hinge.localPosition.x, 1e-5f);
            Assert.IsNotNull(hinge.Find("Křídlo"));

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(door);
            Object.DestroyImmediate(template);
        }

        [Test]
        public void TreeVariantPicksModel()
        {
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            Assert.IsNull(catalog.Tree(0.5f));

            var first = new GameObject("A");
            var second = new GameObject("B");
            catalog.Trees = new[] { first, second };
            Assert.AreSame(first, catalog.Tree(0.1f));
            Assert.AreSame(second, catalog.Tree(0.9f));
            Assert.AreSame(second, catalog.Tree(1f));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
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

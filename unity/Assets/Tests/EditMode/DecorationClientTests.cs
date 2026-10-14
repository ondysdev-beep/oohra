using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Dekorace v klientu: položka nabídky, model z katalogu podle id a sloupek bez modelu.</summary>
    public class DecorationClientTests
    {
        [TearDown]
        public void ResetCatalog()
        {
            VisualCatalog.Current = null;
        }

        [Test]
        public void OptionFromServer()
        {
            var option = BuildOption.ForDecoration(new DecorationTypeView("totem", "Totem", 4, 1, 0.3f, true));

            Assert.AreEqual(BuildKind.Decoration, option.Kind);
            Assert.AreEqual("totem", option.DecorationId);
            Assert.AreEqual("4 klacky a kámen", option.Cost);
            Assert.AreEqual("4 kameny", BuildOption.CostText(0, 4));
            Assert.AreEqual("klacek", BuildOption.CostText(1, 0));
            Assert.AreEqual("6 klacků a 5 kamenů", BuildOption.CostText(6, 5));
        }

        [Test]
        public void ModelByIdOrPost()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            catalog.Decorations = new[] { new DecorationModel { Id = "bench", Prefab = template } };
            VisualCatalog.Current = catalog;

            var bench = StructureViews.CreateModel(BuildKind.Decoration, null, decorationId: "bench");
            Assert.IsNotNull(bench.transform.Find("bench"));

            var totem = StructureViews.CreateModel(BuildKind.Decoration, null, decorationId: "totem", radius: 0.3f);
            Assert.AreEqual(0.6f, totem.transform.Find("Sloupek").localScale.x, 1e-5f);

            Object.DestroyImmediate(bench);
            Object.DestroyImmediate(totem);
            Object.DestroyImmediate(template);
        }

        /// <summary>Náhled zdi v režimu stavby se natáhne i u vlastního modelu (jen podél osy X).</summary>
        [Test]
        public void StretchWallKeepsModelHeight()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.transform.localScale = new Vector3(1f, 2f, 0.1f);
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            catalog.Wall = template;
            VisualCatalog.Current = catalog;

            var wall = StructureViews.CreateModel(BuildKind.StickWall, null);
            StructureViews.StretchWall(wall, 0.25f);

            Assert.AreEqual(new Vector3(0.25f, 2f, 0.1f), wall.transform.GetChild(0).localScale);
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(template);
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Hlídá věci, které v editoru fungují, ale v sestavené hře by chyběly.
    /// </summary>
    public class BuildReadinessTests
    {
        /// <summary>Shader hledaný jen v kódu Unity do hry nepřibalí, proto musí být materiály soubory.</summary>
        [TestCase("OohraLit", "Universal Render Pipeline/Lit")]
        [TestCase("OohraGlow", "Universal Render Pipeline/Unlit")]
        [TestCase("OohraFade", "Universal Render Pipeline/Lit")]
        [TestCase("OohraTerrain", "Universal Render Pipeline/Terrain/Lit")]
        [TestCase("OohraWater", "Universal Render Pipeline/Lit")]
        [TestCase("OohraFoam", "Universal Render Pipeline/Particles/Simple Lit")]
        public void MaterialIsAnAsset(string resource, string shader)
        {
            var material = Resources.Load<Material>(resource);

            Assert.IsNotNull(material, "Chybí Assets/Resources/" + resource + ".mat (menu Oohra → Vytvořit scénu ostrova).");
            Assert.AreEqual(shader, material.shader.name);
            Assert.IsTrue(material.enableInstancing);
        }

        /// <summary>
        /// Průhledná kopie modelu z Meshy (FadeTarget) má klíčová slova průhledný + normálová mapa. Materiál se stejnými
        /// slovy musí být v projektu, jinak sestavení tu verzi shaderu vyhodí a stromy v Oohra.exe nezprůhlední.
        /// </summary>
        [Test]
        public void TransparentModelVariantIsKeptInBuild()
        {
            var keeper = Resources.Load<Material>(Palette.FadeNormalResource);
            Assert.IsNotNull(keeper, "Chybí Assets/Resources/OohraFadeNormal.mat (menu Oohra → Sestavit hru ho vytvoří).");

            var model = VisualCatalog.Current.OakTrees[0].GetComponentInChildren<Renderer>().sharedMaterial;
            var copy = new Material(model);
            Palette.MakeTransparent(copy);
            CollectionAssert.AreEquivalent(copy.shaderKeywords, keeper.shaderKeywords);
        }

        /// <summary>Mlhu zapíná až kód, scéna ji nemá: sestavení nesmí verze shaderů s mlhou vyhodit.</summary>
        [Test]
        public void FogVariantsAreKeptInBuild()
        {
            var settings = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var serialized = new UnityEditor.SerializedObject(settings);
            Assert.AreEqual(1, serialized.FindProperty("m_FogStripping").intValue, "Mlha: Graphics → Fog Modes musí být Custom.");
            Assert.IsTrue(serialized.FindProperty("m_FogKeepLinear").boolValue, "Mlha hry je lineární.");
        }

        /// <summary>Průhledné koruny potřebují materiál, který se kreslí jako průhledný.</summary>
        [Test]
        public void FadeMaterialIsTransparent()
        {
            var material = Resources.Load<Material>("OohraFade");

            Assert.AreEqual(1f, material.GetFloat("_Surface"));
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, material.renderQueue);
            Assert.IsTrue(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
        }
    }
}

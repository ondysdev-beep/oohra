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
        public void MaterialIsAnAsset(string resource, string shader)
        {
            var material = Resources.Load<Material>(resource);

            Assert.IsNotNull(material, "Chybí Assets/Resources/" + resource + ".mat (menu Oohra → Vytvořit scénu ostrova).");
            Assert.AreEqual(shader, material.shader.name);
            Assert.IsTrue(material.enableInstancing);
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

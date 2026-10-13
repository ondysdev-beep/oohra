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
        public void MaterialIsAnAsset(string resource, string shader)
        {
            var material = Resources.Load<Material>(resource);

            Assert.IsNotNull(material, "Chybí Assets/Resources/" + resource + ".mat (menu Oohra → Vytvořit scénu ostrova).");
            Assert.AreEqual(shader, material.shader.name);
            Assert.IsTrue(material.enableInstancing);
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Hlídá věci, které v editoru fungují, ale v sestavené hře by chyběly.
    /// </summary>
    public class BuildReadinessTests
    {
        /// <summary>Shader hledaný jen v kódu Unity do hry nepřibalí, proto musí být materiál soubor.</summary>
        [Test]
        public void LitMaterialIsAnAsset()
        {
            var material = Resources.Load<Material>("OohraLit");

            Assert.IsNotNull(material, "Chybí Assets/Resources/OohraLit.mat (menu Oohra → Vytvořit scénu ostrova).");
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Assert.IsTrue(material.enableInstancing);
        }
    }
}

using System;
using NUnit.Framework;
using Oohra.Core.World;

namespace Oohra.Client.Tests
{
    /// <summary>Každý výsledek akce ze serveru má český text (ne název v kódu).</summary>
    public class TextsTests
    {
        [Test]
        public void EveryMaterialResultHasText()
        {
            foreach (MaterialResult result in Enum.GetValues(typeof(MaterialResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }
        }

        [Test]
        public void EveryBuildResultHasText()
        {
            foreach (BuildResult result in Enum.GetValues(typeof(BuildResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }
        }

        [Test]
        public void EveryFireResultHasText()
        {
            foreach (FireResult result in Enum.GetValues(typeof(FireResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }
        }
    }
}

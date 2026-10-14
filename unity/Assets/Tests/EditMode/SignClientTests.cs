using System;
using NUnit.Framework;
using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Značky v klientu: texty a obrázek kresby.</summary>
    public class SignClientTests
    {
        [Test]
        public void EverySignResultAndTagHasText()
        {
            foreach (SignResult result in Enum.GetValues(typeof(SignResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }

            foreach (CarvingTag tag in Enum.GetValues(typeof(CarvingTag)))
            {
                Assert.AreNotEqual(tag.ToString(), Texts.TagName(tag));
            }
        }

        /// <summary>Řádek 0 kresby je v obrázku nahoře (Unity počítá pixely zdola).</summary>
        [Test]
        public void TextureHasTopRowFirst()
        {
            var bits = SignBits.Empty();
            SignBits.Set(bits, 0, 0, true);

            var texture = SignViews.Texture(bits, Color.black, Color.white);

            Assert.AreEqual(Color.white, texture.GetPixel(0, SignBits.Size - 1));
            Assert.AreEqual(Color.black, texture.GetPixel(0, 0));
            Assert.AreEqual(FilterMode.Point, texture.filterMode);
        }

        [Test]
        public void AgeText()
        {
            Assert.AreEqual("právě teď", Texts.Ago(0.5));
            Assert.AreEqual("před 20 min", Texts.Ago(20));
            Assert.AreEqual("před 3 h", Texts.Ago(180));
            Assert.AreEqual("před 1,5 dny", Texts.Ago(1440 * 1.5));
        }
    }
}

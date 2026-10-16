using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Vzhled Pradávných a Nových (návrh 8.4) a rytina Pradávného (8.6).</summary>
    public class GenerationLookTests
    {
        [Test]
        public void AncientIsBulkyNewAreSlimAndTall()
        {
            var ancient = GenerationLook.For(0);
            var first = GenerationLook.For(1);
            var later = GenerationLook.For(6);

            Assert.Greater(ancient.Width, 1f);
            Assert.Less(ancient.Height, 1f);
            Assert.IsTrue(ancient.Cloak && ancient.Ochre && ancient.BoneBeads);
            Assert.Less(first.Width, 1f);
            Assert.Greater(first.Height, 1f);
            Assert.IsFalse(first.Cloak || first.Headband);
            Assert.Less(later.Width, first.Width);
            Assert.AreNotEqual(ancient.Body, first.Body);
        }

        /// <summary>
        /// Třetí generace vypadá jinak než Noví: tkaný oděv jiné barvy, páska přes hruď, čelenka s pírkem.
        /// Další generace přidají korálky (od 4) a malbu (od 5).
        /// </summary>
        [Test]
        public void ThirdGenerationLooksDifferent()
        {
            var newOnes = GenerationLook.For(1);
            var third = GenerationLook.For(2);

            Assert.IsFalse(newOnes.Headband || newOnes.Sash || newOnes.Feather);
            Assert.IsTrue(third.Headband && third.Sash && third.Feather);
            Assert.AreNotEqual(newOnes.Body, third.Body);
            Assert.AreNotEqual(GenerationLook.For(0).Body, third.Body);
            Assert.AreEqual("3. generace", Texts.GenerationName(2));
            Assert.AreEqual("Nový", Texts.GenerationName(1));
            Assert.AreEqual("Pradávný", Texts.GenerationName(0));
        }

        [Test]
        public void NewGenerationsGainAccessories()
        {
            Assert.IsFalse(GenerationLook.For(1).Headband);
            Assert.IsTrue(GenerationLook.For(2).Headband);
            Assert.IsFalse(GenerationLook.For(3).Beads);
            Assert.IsTrue(GenerationLook.For(4).Beads);
            Assert.IsFalse(GenerationLook.For(4).Paint);
            Assert.IsTrue(GenerationLook.For(5).Paint);
        }

        /// <summary>Postava má rozměry generace. Tvar z kódu (bez modelu v katalogu) má i doplňky.</summary>
        [Test]
        public void FigureHasGenerationShape()
        {
            var parent = new GameObject("Test");
            var figure = GenerationLook.Figure(parent.transform, 0);
            var look = GenerationLook.For(0);

            Assert.AreEqual(GenerationLook.FigureName, figure.name);
            Assert.AreEqual(new Vector3(look.Width, look.Height, look.Width), figure.transform.localScale);
            if (GenerationLook.ModelFor(0) == null)
            {
                Assert.IsNotNull(figure.transform.Find("Plášť"));
                Assert.IsNotNull(figure.transform.Find("Ochra"));
                Assert.IsNull(figure.transform.Find("Čelenka"));
            }

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void AncientSignHasOwnTitle()
        {
            Assert.AreEqual("RYTINA PRADÁVNÉHO", SignHud.Title(Sign(carved: true, byAncient: true)));
            Assert.AreEqual("ZNAČKA PRADÁVNÉHO", SignHud.Title(Sign(carved: false, byAncient: true)));
            Assert.AreEqual("RYTINA", SignHud.Title(Sign(carved: true, byAncient: false)));
        }

        private static SignView Sign(bool carved, bool byAncient)
        {
            return new SignView(1, 0f, 0f, SignBits.Empty(), 1, 0, -1, carved, false, -1, new byte[0], new CarvingTag[0], null, null, byAncient);
        }
    }
}

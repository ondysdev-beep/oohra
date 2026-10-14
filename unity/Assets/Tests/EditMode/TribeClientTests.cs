using System;
using NUnit.Framework;
using Oohra.Core.Language;
using Oohra.Core.Social;

namespace Oohra.Client.Tests
{
    /// <summary>Kmen v klientu: české texty výsledků a gesto pozvání.</summary>
    public class TribeClientTests
    {
        [Test]
        public void EveryTribeResultHasText()
        {
            foreach (TribeResult result in Enum.GetValues(typeof(TribeResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }
        }

        [Test]
        public void InviteGestureHasOwnText()
        {
            Assert.AreEqual("Pozvat do kmene", LanguageTexts.GestureName(Gesture.Invite));
            Assert.AreEqual("zve do kmene", LanguageTexts.GestureBubble(Gesture.Invite));
        }
    }
}

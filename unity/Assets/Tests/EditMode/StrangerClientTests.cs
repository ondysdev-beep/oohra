using System;
using NUnit.Framework;
using Oohra.Core.Language;
using Oohra.Core.Social;
using Oohra.Protocol;

namespace Oohra.Client.Tests
{
    /// <summary>Cizinci v klientu: texty, gesto krčení rameny a dávání věcí.</summary>
    public class StrangerClientTests
    {
        [Test]
        public void EveryStrangerResultHasText()
        {
            foreach (StrangerResult result in Enum.GetValues(typeof(StrangerResult)))
            {
                Assert.IsNotEmpty(Texts.For(result));
            }

            Assert.AreNotEqual(Texts.For(StrangerResult.TooFar), Texts.For(StrangerResult.NotFound));
        }

        [Test]
        public void PuzzledGestureHasOwnText()
        {
            Assert.AreEqual("krčí rameny: ?", LanguageTexts.GestureBubble(Gesture.Puzzled));
            Assert.AreNotEqual(LanguageTexts.GestureBubble(Gesture.Drink), LanguageTexts.GestureBubble(Gesture.Puzzled));
        }

        /// <summary>Dávat jde jen cizinci v nabídce. Po dání batoh přestane nabízet „Dát“.</summary>
        [Test]
        public void GivingNeedsStrangerInMenu()
        {
            var session = new ClientSession(new GameConnection());
            session.StartGiving();
            Assert.AreEqual(-1, session.GiveTo);

            session.Menu = InteractionTarget.ForStranger(3, 10, 10);
            session.StartGiving();
            Assert.AreEqual(3, session.GiveTo);
            Assert.IsTrue(session.BackpackOpen);

            session.Give(0);
            Assert.AreEqual(-1, session.GiveTo);
        }

        [Test]
        public void TribesHaveDifferentColors()
        {
            Assert.AreNotEqual(StrangerViews.TribeColor(0), StrangerViews.TribeColor(1));
        }
    }
}

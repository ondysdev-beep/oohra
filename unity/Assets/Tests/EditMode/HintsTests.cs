using System.Linq;
using NUnit.Framework;
using Oohra.Core.Characters;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Nápovědy a oslavy „poprvé“: každá jen jednou za hráče, F1 přehled kláves.</summary>
    public class HintsTests
    {
        private const string Nickname = "TestNapovedy";

        [SetUp]
        public void Clean()
        {
            PlayerPrefs.DeleteKey("Oohra.Hints." + Nickname);
        }

        [TearDown]
        public void Forget()
        {
            PlayerPrefs.DeleteKey("Oohra.Hints." + Nickname);
        }

        [Test]
        public void TipOnlyOnce()
        {
            var session = new ClientSession(new GameConnection());
            var go = new GameObject("Nápovědy");
            var hints = go.AddComponent<Hints>();
            hints.Init(session, Nickname);

            hints.Tip("thirst");
            hints.Tip("thirst");

            Assert.AreEqual(1, session.Log.Count(l => l.Text == Hints.Tips["thirst"]));
            Assert.IsTrue(hints.Seen("tip.thirst"));

            // Jiný začátek hry (nový objekt) si to pamatuje taky.
            var again = new GameObject("Nápovědy 2").AddComponent<Hints>();
            again.Init(new ClientSession(new GameConnection()), Nickname);
            Assert.IsTrue(again.Seen("tip.thirst"));
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(again.gameObject);
        }

        [Test]
        public void MilestoneShowsBanner()
        {
            var go = new GameObject("Nápovědy");
            var hints = go.AddComponent<Hints>();
            hints.Init(new ClientSession(new GameConnection()), Nickname);

            hints.Milestone("fire");

            Assert.AreEqual("První oheň", hints.Banner);
            Assert.IsTrue(hints.Seen("ms.fire"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TextsExist()
        {
            foreach (var milestone in Hints.Milestones.Values)
            {
                Assert.AreEqual(2, milestone.Length);
                Assert.IsNotEmpty(milestone[0]);
            }

            Assert.IsTrue(Hints.Tips.Values.All(t => t.Length > 10));
            Assert.IsTrue(GameHud.Keys.Any(k => k.Contains("F1")));
        }

        [Test]
        public void ChimeIsShortSound()
        {
            var clip = Chime.Clip();

            Assert.Greater(clip.length, 0.3f);
            Assert.Less(clip.length, 1.5f);
        }
    }
}

using System;
using NUnit.Framework;
using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Tělo a podpis v klientu: texty výsledků a přikreslení znaku do značky.</summary>
    public class CorpseClientTests
    {
        [Test]
        public void EveryCorpseResultHasText()
        {
            foreach (CorpseResult result in Enum.GetValues(typeof(CorpseResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }
        }

        /// <summary>Znak podpisu se do kresby značky přikreslí (co už nakreslené je, zůstane).</summary>
        [Test]
        public void MarkIsAddedToDrawing()
        {
            var go = new GameObject("Značky");
            var hud = go.AddComponent<SignHud>();
            SignBits.Set(hud.Drawing, 0, 0, true);
            var mark = SignBits.Empty();
            SignBits.Set(mark, 5, 6, true);

            hud.AddMark(mark);

            Assert.IsTrue(SignBits.Get(hud.Drawing, 0, 0));
            Assert.IsTrue(SignBits.Get(hud.Drawing, 5, 6));
            Assert.AreEqual(2, SignBits.Count(hud.Drawing));
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}

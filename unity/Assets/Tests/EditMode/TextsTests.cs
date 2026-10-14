using System;
using NUnit.Framework;
using Oohra.Core.Characters;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;

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

        /// <summary>Každá zpráva o těle (kromě smrti, ta má vlastní okno) má český text.</summary>
        [Test]
        public void EveryBodyEventHasText()
        {
            foreach (BodyEvent bodyEvent in Enum.GetValues(typeof(BodyEvent)))
            {
                if (bodyEvent == BodyEvent.Died)
                {
                    continue;
                }

                bool warn;
                var text = Texts.For(new GameEvent(EventKind.Body, (byte)bodyEvent, default(PlantPart), new TasteNote[0], 2, 3), out warn);
                Assert.IsNotNull(text, bodyEvent.ToString());
                Assert.AreNotEqual(bodyEvent.ToString(), text);
            }
        }

        [Test]
        public void SkillLevelUpNamesSkillAndLevel()
        {
            bool warn;
            var text = Texts.For(new GameEvent(EventKind.Body, (byte)BodyEvent.SkillLevelUp, default(PlantPart), new TasteNote[0], (byte)SkillKind.Build, 3), out warn);

            Assert.AreEqual("Po spánku ti věci dávají větší smysl: Stavění 3.", text);
            Assert.IsFalse(warn);
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

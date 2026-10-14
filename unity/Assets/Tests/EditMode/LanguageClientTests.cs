using System;
using NUnit.Framework;
using Oohra.Core.Language;
using Oohra.Protocol;

namespace Oohra.Client.Tests
{
    /// <summary>Panel řeči a zápisník: rozepsané slovo, texty pojmů a popis slova.</summary>
    public class LanguageClientTests
    {
        [Test]
        public void WordHasAtMostThreeSyllables()
        {
            var input = new SpeechInput();
            input.Add(SpeechInput.SyllableOfDigit(1));
            input.Add(SpeechInput.SyllableOfDigit(0));
            input.Add(3);
            input.Add(4);

            CollectionAssert.AreEqual(new byte[] { 0, 9, 3 }, input.ToArray());
            Assert.AreEqual("Gooh-Lom-Mah", input.Text);

            input.RemoveLast();
            Assert.AreEqual(2, input.Count);
            input.Set(new byte[] { 5, 6, 7, 8 });
            CollectionAssert.AreEqual(new byte[] { 5, 6, 7 }, input.ToArray());
        }

        [Test]
        public void EveryConceptContextAndGestureHasText()
        {
            foreach (Concept concept in Enum.GetValues(typeof(Concept)))
            {
                Assert.AreNotEqual(concept.ToString(), LanguageTexts.ConceptName(concept));
            }

            foreach (Gesture gesture in Enum.GetValues(typeof(Gesture)))
            {
                Assert.AreNotEqual(gesture.ToString(), LanguageTexts.GestureName(gesture));
            }

            Assert.AreEqual(Words.ContextCount, Enum.GetValues(typeof(SpeechContext)).Length);
        }

        /// <summary>Popis slova: kolikrát a kde zaznělo, a odhad významu jen u slova bez zapsaného významu.</summary>
        [Test]
        public void EntryDescriptionGuessesMeaning()
        {
            var contexts = new int[Words.ContextCount];
            contexts[(int)SpeechContext.Eating] = 2;
            contexts[(int)SpeechContext.AtFire] = 1;
            var entry = new WordEntryView(new byte[] { 1, 2 }, 3, contexts, null, false);

            Assert.AreEqual("slyšeno 3× · u jídla ×2, u ohně ×1 · asi: jídlo", LanguageTexts.Describe(entry));

            var known = new WordEntryView(new byte[] { 1, 2 }, 3, contexts, Concept.Fire, true);
            Assert.AreEqual("Starý hlas · slyšeno 3× · u jídla ×2, u ohně ×1", LanguageTexts.Describe(known));
        }
    }
}

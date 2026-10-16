using System.Linq;
using NUnit.Framework;
using Oohra.Core.Language;

namespace Oohra.Client.Tests
{
    /// <summary>Zvuk slabik: každá slabika zní jinak, nálada mění hlas, slovo je slabiky za sebou.</summary>
    public class SyllableVoiceTests
    {
        [Test]
        public void EverySyllableHasOwnSound()
        {
            var sounds = Enumerable.Range(0, Words.Syllables.Length)
                .Select(i => SyllableVoice.SyllableSamples((byte)i, Tone.Calm, 0, 1))
                .ToList();

            foreach (var sound in sounds)
            {
                Assert.Greater(sound.Length, 1000);
                Assert.Greater(sound.Max(), 0.01f);
                Assert.LessOrEqual(sound.Max(s => System.Math.Abs(s)), 1f);
            }

            Assert.AreEqual(sounds.Count, sounds.Select(s => s.Length + "/" + s.Sum()).Distinct().Count());
        }

        [Test]
        public void ToneChangesVoice()
        {
            var calm = SyllableVoice.SyllableSamples(3, Tone.Calm, 0, 1);
            var alarm = SyllableVoice.SyllableSamples(3, Tone.Alarm, 0, 1);

            Assert.AreNotEqual(calm.Sum(), alarm.Sum());
        }

        [Test]
        public void WordIsSyllablesInRow()
        {
            var one = SyllableVoice.WordClip(new byte[] { 1 }, Tone.Calm);
            var two = SyllableVoice.WordClip(new byte[] { 1, 4 }, Tone.Calm);

            Assert.Greater(two.samples, one.samples);
            Assert.AreEqual(1, two.channels);
        }
    }
}

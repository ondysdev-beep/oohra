using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Zvuky okolí složené v kódu: oheň, krok, divočák, vlk.</summary>
    public class AmbienceTests
    {
        [Test]
        public void SoundsHaveSensibleLengthAndVolume()
        {
            Check(Ambience.Crackle(), 1.5f, 2.5f);
            Check(Ambience.Step(), 0.03f, 0.2f);
            Check(Ambience.Grunt(), 0.2f, 1f);
            Check(Ambience.Howl(), 1f, 3f);
        }

        private static void Check(AudioClip clip, float minSeconds, float maxSeconds)
        {
            Assert.That(clip.length, Is.InRange(minSeconds, maxSeconds), clip.name);
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            Assert.Greater(samples.Max(s => Mathf.Abs(s)), 0.005f, clip.name);
            Assert.LessOrEqual(samples.Max(s => Mathf.Abs(s)), 1f, clip.name);
        }
    }
}

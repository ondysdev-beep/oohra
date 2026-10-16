using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Krátké zazvonění (oslava „poprvé“, prototyp sfx.chime): dva tóny nahoru, složené v kódu.</summary>
    public static class Chime
    {
        private const int SampleRate = 22050;
        private static AudioClip _clip;

        public static void Play()
        {
            var camera = Camera.main;
            if (!Application.isPlaying || camera == null || SyllableVoice.Volume <= 0f)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(Clip(), camera.transform.position, SyllableVoice.Volume * 0.6f);
        }

        public static AudioClip Clip()
        {
            if (_clip != null)
            {
                return _clip;
            }

            var count = (int)(0.7f * SampleRate);
            var samples = new float[count];
            for (var n = 0; n < count; n++)
            {
                var t = (float)n / SampleRate;
                var pitch = t < 0.18f ? 660f : 990f;
                var start = t < 0.18f ? 0f : 0.18f;
                var decay = Mathf.Exp(-(t - start) * 6f);
                samples[n] = 0.3f * decay * (Mathf.Sin(2 * Mathf.PI * pitch * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * pitch * t));
            }

            _clip = AudioClip.Create("Zazvonění", count, 1, SampleRate, false);
            _clip.SetData(samples, 0);
            return _clip;
        }
    }
}

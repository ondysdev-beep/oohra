using System.Collections.Generic;
using Oohra.Core.Language;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Zvuk slov ze slabik (návrh 9.2: zvuk slabik je stejný pro všechny). Hra nemá nahrávky, slabiky se skládají
    /// v kódu: souhláska (šum, bzučení) a samohláska z hlasu s formanty (a, e, i, o, u). Nálada mění výšku hlasu:
    /// klidně hluboko, poplach vysoko a stoupavě, radost výš s úsměvem. Slovo je slabiky za sebou s krátkou pauzou.
    /// </summary>
    public static class SyllableVoice
    {
        private const int SampleRate = 22050;
        private const float Gap = 0.05f;

        private static readonly Dictionary<long, float[]> Cache = new Dictionary<long, float[]>();

        /// <summary>Hlasitost řeči 0 až 1.</summary>
        public static float Volume { get; set; } = 0.8f;

        // Hlásky slabik Gooh, Arh, Oo, Mah, Tek, Rin, Ush, Dak, Hee, Lom (Words.Syllables).
        private static readonly string[] Sounds = { "g-oo", "a-r-h", "uu", "m-a-h", "t-e-k", "r-i-n", "u-sh", "d-a-k", "h-ii", "l-o-m" };

        /// <summary>Řekne slovo: u postavy ve světě (at), jinak u kamery.</summary>
        public static void Say(IReadOnlyList<byte> word, Tone tone, Vector3? at = null)
        {
            // Mimo hru (testy v editoru) se nehraje, Unity tam zvuky „na jedno použití“ neumí.
            if (!Application.isPlaying || Volume <= 0f || word == null || word.Count == 0)
            {
                return;
            }

            var clip = WordClip(word, tone);
            var camera = Camera.main;
            var position = at ?? (camera != null ? camera.transform.position : Vector3.zero);
            AudioSource.PlayClipAtPoint(clip, position, Volume);
        }

        /// <summary>Zvuk celého slova (pro Say a testy).</summary>
        public static AudioClip WordClip(IReadOnlyList<byte> word, Tone tone)
        {
            var samples = new List<float>();
            for (var i = 0; i < word.Count; i++)
            {
                samples.AddRange(SyllableSamples(word[i], tone, i, word.Count));
                samples.AddRange(new float[(int)(Gap * SampleRate)]);
            }

            var clip = AudioClip.Create("Slovo " + Words.Text(word), samples.Count, 1, SampleRate, false);
            clip.SetData(samples.ToArray(), 0);
            return clip;
        }

        /// <summary>Vzorky jedné slabiky (v paměti, každá slabika a nálada se počítá jen jednou).</summary>
        public static float[] SyllableSamples(byte syllable, Tone tone, int index, int count)
        {
            // Poslední slabika slova klesá (klid) nebo stoupá (poplach, radost), jako věta.
            var last = index == count - 1;
            var key = ((long)syllable << 8) | ((long)tone << 4) | (last ? 1L : 0L);
            float[] cached;
            if (Cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var samples = new List<float>();
            var seed = 1234 + syllable * 97;
            foreach (var sound in Sounds[syllable % Sounds.Length].Split('-'))
            {
                samples.AddRange(Phone(sound, tone, last, ref seed));
            }

            cached = samples.ToArray();
            Cache[key] = cached;
            return cached;
        }

        private static float[] Phone(string sound, Tone tone, bool last, ref int seed)
        {
            switch (sound)
            {
                case "t": return Noise(0.03f, 0.35f, ref seed);
                case "k": return Noise(0.035f, 0.3f, ref seed);
                case "d": return Noise(0.025f, 0.25f, ref seed);
                case "g": return Noise(0.03f, 0.2f, ref seed);
                case "h": return Noise(0.08f, 0.12f, ref seed);
                case "sh": return Noise(0.12f, 0.2f, ref seed);
                case "m": return Voiced(0.08f, 250f, 1000f, tone, last, 0.5f);
                case "n": return Voiced(0.07f, 250f, 1600f, tone, last, 0.5f);
                case "l": return Voiced(0.07f, 350f, 1100f, tone, last, 0.6f);
                case "r": return Voiced(0.07f, 450f, 1250f, tone, last, 0.6f, trill: true);
                case "a": return Voiced(0.17f, 730f, 1090f, tone, last, 1f);
                case "e": return Voiced(0.16f, 530f, 1840f, tone, last, 1f);
                case "i": return Voiced(0.15f, 300f, 2250f, tone, last, 1f);
                case "ii": return Voiced(0.26f, 300f, 2250f, tone, last, 1f);
                case "o": return Voiced(0.17f, 570f, 840f, tone, last, 1f);
                case "oo": return Voiced(0.26f, 570f, 840f, tone, last, 1f);
                case "u": return Voiced(0.16f, 320f, 870f, tone, last, 1f);
                default: return Voiced(0.28f, 320f, 870f, tone, last, 1f);
            }
        }

        /// <summary>Hlas: harmonické základního tónu zesílené kolem dvou formantů (barva samohlásky).</summary>
        private static float[] Voiced(float seconds, float f1, float f2, Tone tone, bool last, float loudness, bool trill = false)
        {
            var count = (int)(seconds * SampleRate);
            var result = new float[count];
            var basePitch = tone == Tone.Alarm ? 205f : tone == Tone.Joy ? 175f : 125f;
            var phase = 0.0;
            for (var n = 0; n < count; n++)
            {
                var u = (float)n / count;
                var glide = last ? (tone == Tone.Calm ? -0.15f : 0.2f) * u : 0f;
                var pitch = basePitch * (1f + glide);
                phase += 2 * Mathf.PI * pitch / SampleRate;
                var value = 0.0;
                for (var h = 1; h * pitch < 3500f; h++)
                {
                    var f = h * pitch;
                    var gain = 1.0 / (1 + Sq((f - f1) / 110f)) + 0.7 / (1 + Sq((f - f2) / 140f));
                    value += gain * System.Math.Sin(h * phase) / h;
                }

                var envelope = Mathf.Min(1f, u * 12f) * Mathf.Min(1f, (1f - u) * 6f);
                var buzz = trill ? 0.6f + 0.4f * Mathf.Sin(n * 2 * Mathf.PI * 28f / SampleRate) : 1f;
                result[n] = (float)value * 0.35f * envelope * loudness * buzz;
            }

            return result;
        }

        /// <summary>Šum souhlásky (t, k, h, š): krátký výbuch nebo sykot.</summary>
        private static float[] Noise(float seconds, float loudness, ref int seed)
        {
            var count = (int)(seconds * SampleRate);
            var result = new float[count];
            var previous = 0f;
            for (var n = 0; n < count; n++)
            {
                // Jednoduchý generátor šumu (zvuk není svět, determinismus tu nevadí, ale je stejný pokaždé).
                seed = seed * 1103515245 + 12345;
                var white = ((seed >> 16) & 0x7fff) / 16384f - 1f;
                var high = white - previous * 0.6f;
                previous = white;
                var u = (float)n / count;
                result[n] = high * loudness * Mathf.Min(1f, u * 20f) * (1f - u);
            }

            return result;
        }

        private static double Sq(double x)
        {
            return x * x;
        }
    }
}

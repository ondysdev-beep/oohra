using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Nahrávka, která hraje pořád dokola (oheň, déšť, moře). Konec a začátek nahrávky na sebe nenavazují a MP3
    /// má na začátku kousek ticha, proto hrají dva zdroje: před koncem se nahrávka pustí znovu a oba se prolnou
    /// (Crossfade). Hlasitost řídí Volume, poloha je poloha objektu (u ohně se přesouvá k nejbližšímu ohni).
    /// </summary>
    public class LoopSound : MonoBehaviour
    {
        /// <summary>Jak dlouho se konec a nový začátek prolínají (sekundy).</summary>
        public const float Crossfade = 2f;

        // Začátek nahrávky, který se přeskočí (ticho na začátku MP3).
        private const float SkipStart = 0.1f;

        private AudioSource _current;
        private AudioSource _next;
        private float _fadeStarted = -1f;

        /// <summary>Hlasitost 0 až 1 (už i s hlasitostí hry).</summary>
        public float Volume { get; set; }

        public AudioClip Clip { get; private set; }

        public bool Playing { get; private set; }

        /// <summary>
        /// Smyčka na novém objektu pod rodičem. spatial = 0 zní všude stejně (déšť), 1 z místa (oheň: slyšet
        /// od minDistance plně, do maxDistance slábne).
        /// </summary>
        public static LoopSound Create(Transform parent, string name, AudioClip clip, float spatial, float minDistance = 1f, float maxDistance = 20f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var loop = go.AddComponent<LoopSound>();
            loop.Clip = clip;
            loop._current = loop.Source(spatial, minDistance, maxDistance);
            loop._next = loop.Source(spatial, minDistance, maxDistance);
            return loop;
        }

        private AudioSource Source(float spatial, float minDistance, float maxDistance)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = Clip;
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = spatial;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = 0f;
            return source;
        }

        /// <summary>Začne hrát z náhodného místa nahrávky (pokaždé jinak, ať to nezní stejně).</summary>
        public void Play()
        {
            if (Playing || Clip == null)
            {
                return;
            }

            Playing = true;
            _fadeStarted = -1f;
            _current.time = Mathf.Max(SkipStart, Random.Range(0f, Mathf.Max(0f, Clip.length - 3 * Crossfade)));
            _current.volume = Volume;
            _current.Play();
        }

        public void Stop()
        {
            Playing = false;
            _fadeStarted = -1f;
            _current.Stop();
            _next.Stop();
        }

        private void Update()
        {
            if (!Playing)
            {
                return;
            }

            if (_fadeStarted < 0f && (_current.time >= Clip.length - Crossfade || !_current.isPlaying))
            {
                // Před koncem nahrávky: druhý zdroj začne od začátku a hlasitost se přelije.
                _fadeStarted = Time.unscaledTime;
                _next.time = SkipStart;
                _next.volume = 0f;
                _next.Play();
            }

            if (_fadeStarted < 0f)
            {
                _current.volume = Volume;
                return;
            }

            var t = Mathf.Clamp01((Time.unscaledTime - _fadeStarted) / Crossfade);
            _current.volume = Volume * (1f - t);
            _next.volume = Volume * t;
            if (t >= 1f)
            {
                _current.Stop();
                var swap = _current;
                _current = _next;
                _next = swap;
                _fadeStarted = -1f;
            }
        }
    }
}

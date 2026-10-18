using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Počasí ze serveru (snímek nese druh, sílu a vítr): mlha (Unity fog podle vzdálenosti kamery), padající déšť
    /// (částice kolem postavy), bouřka (blesky a hrom) a zvuk deště. Změny se prolínají pomalu, ne skokem.
    /// Zatažení (Overcast) a blesk (Flash) čte DayNightLight a podle nich ztlumí nebo rozsvítí slunce.
    /// </summary>
    public class WeatherView : MonoBehaviour
    {
        // Kolik metrů za postavou ještě něco vidíš (dál všechno pohltí šero nebo mlha).
        private const float ClearSight = 55f;
        private const float RainSight = 26f;
        private const float StormSight = 18f;
        private const float FogSight = 6f;

        // Jak rychle se počasí prolíná (podíl za sekundu).
        private const float Blend = 0.25f;
        private const float RainPerSecond = 2600f;
        private const float RainArea = 26f;

        private static readonly Color NightFog = new Color(0.025f, 0.03f, 0.045f);
        private static readonly Color DayHaze = new Color(0.36f, 0.39f, 0.42f);
        private static readonly Color DayFog = new Color(0.43f, 0.46f, 0.48f);
        private static readonly Color RainColor = new Color(0.7f, 0.75f, 0.85f, 0.35f);

        private GameConnection _connection;
        private ClientClock _clock;
        private PlayerViews _players;
        private Camera _camera;
        private ParticleSystem _rain;
        private AudioSource _rainSound;
        private float _nextLightning;
        private float _thunderAt = -1f;
        private float _flashStarted = -10f;

        /// <summary>Hustota mlhy 0 až 1 (prolnutá).</summary>
        public float FogAmount { get; private set; }

        /// <summary>Síla deště 0 až 1 (i v bouřce).</summary>
        public float RainAmount { get; private set; }

        public float StormAmount { get; private set; }

        public float Wind { get; private set; }

        /// <summary>Vítr právě teď 0 až 1 (kouř z ohňů a pochodní se po něm sklání).</summary>
        public static float CurrentWind { get; private set; }

        /// <summary>Jak moc je zataženo (0 jasno, 1 bouřka): slunce svítí méně.</summary>
        public float Overcast
        {
            get { return Mathf.Max(FogAmount * 0.8f, RainAmount * 0.75f, StormAmount * 0.95f); }
        }

        /// <summary>Záblesk blesku 0 až 1 (krátce po úderu).</summary>
        public float Flash { get; private set; }

        /// <summary>Kolik kapek deště právě je ve vzduchu (pro testy).</summary>
        public int RainParticles
        {
            get { return _rain != null ? _rain.particleCount : 0; }
        }

        public void Init(GameConnection connection, ClientClock clock, PlayerViews players, Camera camera)
        {
            _connection = connection;
            _clock = clock;
            _players = players;
            _camera = camera;
            _rain = CreateRain();
            _rainSound = gameObject.AddComponent<AudioSource>();
            _rainSound.clip = Ambience.Rain();
            _rainSound.loop = true;
            _rainSound.spatialBlend = 0f;
            _rainSound.volume = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            _nextLightning = Time.time + 5f;
        }

        private void Update()
        {
            if (_connection == null || _connection.Snapshot == null)
            {
                return;
            }

            BlendTowards(_connection.Snapshot);
            UpdateFog();
            UpdateRain();
            UpdateLightning();
        }

        private void BlendTowards(Snapshot snapshot)
        {
            var intensity = snapshot.WeatherIntensity;
            var fog = snapshot.Weather == WeatherKind.Fog ? intensity : 0f;
            var rain = snapshot.Weather == WeatherKind.Rain || snapshot.Weather == WeatherKind.Storm ? intensity : 0f;
            var storm = snapshot.Weather == WeatherKind.Storm ? intensity : 0f;
            var step = Mathf.Min(1f, Blend * Time.deltaTime);
            FogAmount = Mathf.Lerp(FogAmount, fog, step);
            RainAmount = Mathf.Lerp(RainAmount, rain, step);
            StormAmount = Mathf.Lerp(StormAmount, storm, step);
            Wind = Mathf.Lerp(Wind, snapshot.Wind, step);
            CurrentWind = Wind;
        }

        /// <summary>
        /// Mlha začíná kousek za postavou (při husté mlze už u ní) a končí po tolika metrech, kolik je vidět.
        /// Barva je v noci skoro černá, ve dne šedá. Pozadí kamery má stejnou barvu, takže obzor zmizí v šeru.
        /// </summary>
        private void UpdateFog()
        {
            var cameraDistance = 18f;
            if (_camera != null && _players != null && _players.Self != null)
            {
                cameraDistance = Vector3.Distance(_camera.transform.position, _players.Self.position);
            }

            var sight = Mathf.Lerp(ClearSight, RainSight, RainAmount);
            sight = Mathf.Lerp(sight, StormSight, StormAmount);
            sight = Mathf.Lerp(sight, FogSight, FogAmount);
            RenderSettings.fogStartDistance = Mathf.Max(0f, cameraDistance + sight * 0.15f - 5f * FogAmount);
            RenderSettings.fogEndDistance = cameraDistance + sight;

            var daylight = _clock != null ? (float)_clock.Daylight : 1f;
            var day = Color.Lerp(DayHaze, DayFog, FogAmount) * (1f - 0.35f * Mathf.Max(RainAmount, StormAmount));
            var color = Color.Lerp(NightFog, day, daylight);
            color = Color.Lerp(color, new Color(0.75f, 0.8f, 0.95f), Flash * 0.6f);
            RenderSettings.fogColor = color;
            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = color;
            }
        }

        /// <summary>Kapky padají v ploše kolem postavy, vítr je šikmí. Zvuk deště sílí s deštěm.</summary>
        private void UpdateRain()
        {
            if (_players != null && _players.Self != null)
            {
                _rain.transform.position = _players.Self.position + Vector3.up * 14f;
            }

            var emission = _rain.emission;
            emission.rateOverTime = RainPerSecond * RainAmount * (1f + StormAmount * 0.6f);
            var velocity = _rain.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(Wind * 6f);

            var volume = SyllableVoice.Volume * 0.55f * Mathf.Clamp01(RainAmount * 1.3f) * (DreamView.Active ? 0f : 1f);
            _rainSound.volume = volume;
            if (volume > 0.001f && !_rainSound.isPlaying)
            {
                _rainSound.Play();
            }
            else if (volume <= 0.001f && _rainSound.isPlaying)
            {
                _rainSound.Stop();
            }
        }

        /// <summary>V bouřce občas blýskne (dvojitý záblesk) a za chvíli zahřmí, podle vzdálenosti blesku.</summary>
        private void UpdateLightning()
        {
            if (StormAmount > 0.35f && Time.time >= _nextLightning)
            {
                _nextLightning = Time.time + Random.Range(7f, 22f) / StormAmount;
                _flashStarted = Time.time;
                _thunderAt = Time.time + Random.Range(0.4f, 3f);
            }

            var t = Time.time - _flashStarted;
            Flash = t < 0.08f ? 1f : t < 0.16f ? 0.2f : t < 0.26f ? 0.8f : Mathf.Max(0f, 0.8f - (t - 0.26f) * 3f);

            if (_thunderAt > 0f && Time.time >= _thunderAt)
            {
                _thunderAt = -1f;
                if (SyllableVoice.Volume > 0f && Application.isPlaying)
                {
                    _rainSound.PlayOneShot(Ambience.Thunder(), SyllableVoice.Volume * Random.Range(0.6f, 1f));
                }
            }
        }

        private ParticleSystem CreateRain()
        {
            var go = new GameObject("Déšť");
            go.transform.SetParent(transform);
            var rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.035f;
            main.startColor = RainColor;
            main.maxParticles = 8000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(RainArea, 0.1f, RainArea);

            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-15f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var emission = rain.emission;
            emission.rateOverTime = 0f;

            var renderer = ParticleLook.Setup(rain, ParticleLook.Streak);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
            rain.Play();
            return rain;
        }
    }
}

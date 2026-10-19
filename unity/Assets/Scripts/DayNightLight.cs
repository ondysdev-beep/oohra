using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Slunce podle herních hodin: ráno vychází, v noci je skoro úplná tma a jen slabé modravé světlo (čas posílá server).
    /// Svět je ponurý: i ve dne je světlo tlumené a studené, v mlze a dešti ještě víc (WeatherView.Overcast),
    /// blesk ho na okamžik rozsvítí. Ve tmě je vidět hlavně kolem ohňů a pochodní.
    /// </summary>
    public class DayNightLight : MonoBehaviour
    {
        [SerializeField] private float _dayIntensity = 0.95f;
        [SerializeField] private float _nightIntensity = 0.012f;
        [SerializeField] private float _dayAmbient = 0.75f;
        [SerializeField] private float _nightAmbient = 0.045f;

        private static readonly Color NightColor = new Color(0.5f, 0.6f, 1f);
        private static readonly Color DayColor = new Color(0.9f, 0.92f, 0.95f);
        private static readonly Color FlashColor = new Color(0.8f, 0.85f, 1f);

        private ClientClock _clock;
        private Light _sun;
        private WeatherView _weather;

        public void Init(ClientClock clock, Light sun, WeatherView weather = null)
        {
            _clock = clock;
            _sun = sun;
            _weather = weather;
        }

        private void Update()
        {
            if (_clock == null || _sun == null)
            {
                return;
            }

            // 6:00 slunce na východním obzoru, 12:00 nahoře, 18:00 na západě.
            var angle = (float)((_clock.Hour - 6) / 24 * 360);
            _sun.transform.rotation = Quaternion.Euler(angle, -30f, 0f);
            var daylight = (float)_clock.Daylight;
            var overcast = _weather != null ? _weather.Overcast : 0f;
            var flash = _weather != null ? _weather.Flash : 0f;
            // V jeskyni slunce nesvítí a okolí je skoro černé: vidět je jen u ohně, pochodně a východu.
            var cave = _weather != null ? _weather.CaveAmount : 0f;
            _sun.intensity = (Mathf.Lerp(_nightIntensity, _dayIntensity, daylight) * (1f - 0.7f * overcast) + flash * 1.6f) * (1f - cave);
            RenderSettings.ambientIntensity = Mathf.Lerp(Mathf.Lerp(_nightAmbient, _dayAmbient, daylight) * (1f - 0.35f * overcast) + flash * 0.7f, _nightAmbient, cave);
            // Pod mrakem a v mlze je světlo rozptýlené: stíny skoro zmizí.
            _sun.shadowStrength = 1f - 0.85f * overcast;
            var color = Color.Lerp(NightColor, DayColor, daylight);
            _sun.color = Color.Lerp(Color.Lerp(color, new Color(0.7f, 0.74f, 0.8f), overcast), FlashColor, flash);
        }
    }
}

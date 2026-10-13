using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Slunce podle herních hodin: ráno vychází, v noci je tma (čas posílá server).</summary>
    public class DayNightLight : MonoBehaviour
    {
        [SerializeField] private float _dayIntensity = 1.2f;
        [SerializeField] private float _nightIntensity = 0.08f;

        private ClientClock _clock;
        private Light _sun;

        public void Init(ClientClock clock, Light sun)
        {
            _clock = clock;
            _sun = sun;
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
            _sun.intensity = Mathf.Lerp(_nightIntensity, _dayIntensity, daylight);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, 1f, daylight);
        }
    }
}

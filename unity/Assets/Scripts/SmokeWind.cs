using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Kouř se sklání po větru (vítr posílá server s počasím, WeatherView.CurrentWind).</summary>
    public class SmokeWind : MonoBehaviour
    {
        private ParticleSystem _smoke;

        private void Awake()
        {
            _smoke = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            var force = _smoke.forceOverLifetime;
            force.x = new ParticleSystem.MinMaxCurve(0.15f + WeatherView.CurrentWind * 1.4f);
            force.y = new ParticleSystem.MinMaxCurve(0f);
            force.z = new ParticleSystem.MinMaxCurve(0f);
        }
    }
}

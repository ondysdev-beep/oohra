using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Částice kolem postavy, jen pro náladu: cáry mlhy u země (při mlze hustě, ráno, v noci a u bažiny slabě),
    /// v noci světlušky (ne v dešti a v jeskyni) a v lese padající listí (ve dne). Pohybují se s větrem.
    /// </summary>
    public class AmbientParticles : MonoBehaviour
    {
        // Jak velké okolí kolem postavy částice pokrývají (metry).
        private const float MistArea = 46f;
        private const float BugArea = 22f;

        private PlayerViews _players;
        private WeatherView _weather;
        private ClientClock _clock;
        private GameConnection _connection;
        private ParticleSystem _mist;
        private ParticleSystem _fireflies;
        private ParticleSystem _leaves;

        /// <summary>Kolik cárů mlhy přibývá za sekundu (pro testy).</summary>
        public float MistRate { get; private set; }

        public float FireflyRate { get; private set; }

        public void Init(GameConnection connection, PlayerViews players, WeatherView weather, ClientClock clock)
        {
            _connection = connection;
            _players = players;
            _weather = weather;
            _clock = clock;
            _mist = Create("Mlha u země", MistArea, 2f, 400, new Color(0.72f, 0.76f, 0.8f, 0.16f), new Vector2(5f, 11f), new Vector2(12f, 18f), 0.2f);
            _fireflies = Create("Světlušky", BugArea, 2.5f, 120, new Color(1.6f, 1.9f, 0.7f, 0.9f), new Vector2(0.05f, 0.09f), new Vector2(4f, 7f), 0.5f);
            _leaves = Create("Listí", BugArea, 8f, 80, new Color(0.45f, 0.38f, 0.2f, 0.85f), new Vector2(0.06f, 0.12f), new Vector2(5f, 8f), 0.6f);
            var leafFall = _leaves.velocityOverLifetime;
            leafFall.enabled = true;
            leafFall.space = ParticleSystemSimulationSpace.World;
            leafFall.y = new ParticleSystem.MinMaxCurve(-0.9f, -0.5f);
            leafFall.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            leafFall.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        /// <summary>Částice v krabici kolem postavy (strana area, výška height nad zemí), měkké skvrny.</summary>
        private ParticleSystem Create(string name, float area, float height, int max, Color color, Vector2 size, Vector2 life, float noise)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area, height, area);
            shape.position = new Vector3(0f, height / 2f, 0f);

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var drift = system.noise;
            drift.enabled = true;
            drift.strength = noise;
            drift.frequency = 0.3f;

            ParticleLook.Setup(system, ParticleLook.Soft);
            system.Play();
            return system;
        }

        private void Update()
        {
            if (_players == null || _players.Self == null || _weather == null)
            {
                return;
            }

            var self = _players.Self.position;
            var ground = new Vector3(self.x, self.y - PlayerViews.BodyHeight / 2f, self.z);
            _mist.transform.position = ground;
            _fireflies.transform.position = ground;
            _leaves.transform.position = ground;

            double x, y;
            WorldCoords.FromUnity(self, out x, out y);
            var daylight = _clock != null ? (float)_clock.Daylight : 1f;
            var night = 1f - daylight;
            var outside = 1f - _weather.CaveAmount;
            var dream = DreamView.Active ? 0f : 1f;

            // Mlha: hustá v mlze, slabá v noci a za šera, víc u bažiny a vody.
            var dusk = Mathf.Clamp01(1f - Mathf.Abs(daylight - 0.35f) * 3f);
            var marsh = NearMarsh(x, y) ? 0.5f : 0f;
            MistRate = (_weather.FogAmount * 40f + (night * 0.35f + dusk * 0.4f + marsh) * 8f) * outside * dream;
            SetRate(_mist, MistRate);

            FireflyRate = (night > 0.6f && _weather.RainAmount < 0.1f ? 14f : 0f) * outside * dream;
            SetRate(_fireflies, FireflyRate);

            var forest = InForest(x, y);
            SetRate(_leaves, forest && daylight > 0.3f ? 5f * (1f + WeatherView.CurrentWind * 2f) * outside * dream : 0f);

            // Vítr unáší mlhu a listí.
            var wind = WeatherView.CurrentWind;
            var force = _mist.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(wind * 0.25f);
            force.y = new ParticleSystem.MinMaxCurve(0f);
            force.z = new ParticleSystem.MinMaxCurve(0f);
        }

        private static void SetRate(ParticleSystem system, float rate)
        {
            var emission = system.emission;
            emission.rateOverTime = rate;
        }

        private bool NearMarsh(double x, double y)
        {
            var world = _connection != null ? _connection.World : null;
            if (world == null)
            {
                return false;
            }

            for (var dy = -6; dy <= 6; dy += 3)
            {
                for (var dx = -6; dx <= 6; dx += 3)
                {
                    var (tx, ty) = ((int)x + dx, (int)y + dy);
                    if (tx >= 0 && ty >= 0 && tx < world.Width && ty < world.Height
                        && world.TileAt(tx, ty) is TileType.Bog or TileType.Mire or TileType.Pond)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool InForest(double x, double y)
        {
            var world = _connection != null ? _connection.World : null;
            var (tx, ty) = ((int)x, (int)y);
            return world != null && tx >= 0 && ty >= 0 && tx < world.Width && ty < world.Height && world.TileAt(tx, ty) == TileType.Forest;
        }
    }
}

using System.Collections.Generic;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Ohně ve scéně podle zpráv serveru: ohniště, plamen a oranžové světlo, které mihotá.
    /// Vyhaslý oheň je jen popel (bez plamene a světla). Ohniště táborů hoří pořád.
    /// </summary>
    public class FireViews : MonoBehaviour
    {
        private const float LightRange = 8f;
        private const float LightIntensity = 2.5f;
        private static readonly Color LightColor = new Color(1f, 0.62f, 0.3f);

        private readonly Dictionary<int, FireView> _shown = new Dictionary<int, FireView>();
        private readonly Dictionary<int, GameObject> _flames = new Dictionary<int, GameObject>();
        private readonly List<Light> _lights = new List<Light>();
        private GameConnection _connection;

        public void Init(GameConnection connection, WorldView world)
        {
            _connection = connection;
            foreach (var camp in world.Camps)
            {
                CreateFlame(transform, camp.X + 0.5, camp.Y + 0.5).name = "Oheň tábora";
            }
        }

        /// <summary>Počet ohňů ve scéně, které právě hoří (pro testy).</summary>
        public int BurningCount
        {
            get
            {
                var count = 0;
                foreach (var light in _lights)
                {
                    if (light.isActiveAndEnabled) count++;
                }

                return count;
            }
        }

        private void Update()
        {
            if (_connection == null)
            {
                return;
            }

            foreach (var fire in _connection.Fires)
            {
                FireView shown;
                if (!_shown.TryGetValue(fire.Id, out shown) || shown.Burning != fire.Burning)
                {
                    Show(fire);
                }
            }

            Flicker();
        }

        private void Show(FireView fire)
        {
            GameObject flame;
            if (!_flames.TryGetValue(fire.Id, out flame))
            {
                var hearth = IslandView.Primitive(PrimitiveType.Cylinder, "Oheň " + fire.Id, transform, Palette.Hex("#4a4038"));
                hearth.transform.position = WorldCoords.ToUnity(fire.TileX + 0.5, fire.TileY + 0.5, 0.04f);
                hearth.transform.localScale = new Vector3(0.7f, 0.04f, 0.7f);
                flame = CreateFlame(transform, fire.TileX + 0.5, fire.TileY + 0.5);
                _flames[fire.Id] = flame;
            }

            flame.SetActive(fire.Burning);
            _shown[fire.Id] = fire;
        }

        /// <summary>Plamen (svítí sám) a nad ním bodové světlo, které osvětlí okolí.</summary>
        private GameObject CreateFlame(Transform parent, double x, double y)
        {
            var flame = IslandView.Primitive(PrimitiveType.Sphere, "Plamen", parent, Color.white);
            Palette.SetGlow(flame, Palette.Hex("#f2a03a"));
            flame.transform.position = WorldCoords.ToUnity(x, y, 0.22f);
            flame.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);

            var light = new GameObject("Světlo ohně").AddComponent<Light>();
            light.transform.SetParent(flame.transform);
            light.transform.position = WorldCoords.ToUnity(x, y, 1.3f);
            light.type = LightType.Point;
            light.color = LightColor;
            light.range = LightRange;
            light.intensity = LightIntensity;
            light.shadows = LightShadows.None;
            _lights.Add(light);
            return flame;
        }

        /// <summary>Světla hořících ohňů mírně kolísají, každé jinak.</summary>
        private void Flicker()
        {
            for (var i = 0; i < _lights.Count; i++)
            {
                var t = Time.time + i * 1.7f;
                _lights[i].intensity = LightIntensity * (1f + 0.08f * Mathf.Sin(t * 9f) + 0.05f * Mathf.Sin(t * 23f));
            }
        }
    }
}

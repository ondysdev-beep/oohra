using System.Collections.Generic;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Koruny stromů a zdi blízko vlastní postavy (hlavně mezi postavou a kamerou) plynule zprůhlední,
    /// aby hráč viděl, kde stojí. Když postava odejde, zase zneprůhlední. Na každé dlaždici je nejvýš jedna věc.
    /// Kolize počítá server (Movement v Core), tohle je jen vzhled.
    /// </summary>
    public class SeeThrough : MonoBehaviour
    {
        private const float FadeRadius = 2.6f;
        // Výška, ve které koruny postavu zakrývají (střed koruny, v metrech).
        private const float CoverHeight = 3.2f;
        private const float FadedAlpha = 0.28f;
        private const float FadeSpeed = 5f;

        private readonly Dictionary<int, Crown> _crowns = new Dictionary<int, Crown>();
        private readonly List<Crown> _fading = new List<Crown>();
        private int _width;
        private PlayerViews _players;
        private Camera _camera;

        /// <summary>Koruna jednoho stromu: renderer, původní barva a současná průhlednost.</summary>
        private sealed class Crown
        {
            public Renderer Renderer;
            public Color Color;
            public float Alpha = 1f;
            public bool Near;
            public bool OnlyInFront;
        }

        public void Init(int worldWidth)
        {
            _width = worldWidth;
        }

        /// <summary>
        /// Přidá věc na dlaždici. onlyInFront: zprůhlední jen mezi postavou a kamerou (zdi, ať je vidět,
        /// co hráč postavil), jinak v celém okolí postavy (koruny).
        /// </summary>
        public void Add(int tileIndex, GameObject crown, Color color, bool onlyInFront = false)
        {
            _crowns[tileIndex] = new Crown { Renderer = crown.GetComponent<Renderer>(), Color = color, OnlyInFront = onlyInFront };
        }

        /// <summary>Věc na dlaždici zmizela (rozebraná zeď).</summary>
        public void Remove(int tileIndex)
        {
            Crown crown;
            if (_crowns.TryGetValue(tileIndex, out crown))
            {
                _fading.Remove(crown);
                _crowns.Remove(tileIndex);
            }
        }

        /// <summary>Začne sledovat vlastní postavu (volá se, až existují hráči a kamera).</summary>
        public void Follow(PlayerViews players, Camera camera)
        {
            _players = players;
            _camera = camera;
        }

        /// <summary>Kolik korun je teď průhledných (pro testy).</summary>
        public int FadedCount
        {
            get { return _fading.Count; }
        }

        private void Update()
        {
            if (_players == null || _players.Self == null || _camera == null)
            {
                return;
            }

            MarkNear(FadeCenter(), ToCamera());
            for (var i = _fading.Count - 1; i >= 0; i--)
            {
                var crown = _fading[i];
                var target = crown.Near ? FadedAlpha : 1f;
                crown.Alpha = Mathf.MoveTowards(crown.Alpha, target, FadeSpeed * Time.deltaTime);
                Apply(crown);
                if (!crown.Near && crown.Alpha >= 1f)
                {
                    _fading.RemoveAt(i);
                }
            }
        }

        /// <summary>Střed oblasti: postava, posunutá kousek ke kameře (tam koruny postavu zakrývají).</summary>
        private Vector3 FadeCenter()
        {
            // Čím níž je kamera skloněná, tím dál ke kameře stojí koruny, které postavu zakrývají.
            var pitch = Mathf.Clamp(_camera.transform.eulerAngles.x, 15f, 89f) * Mathf.Deg2Rad;
            return _players.Self.position + ToCamera() * (CoverHeight / Mathf.Tan(pitch));
        }

        /// <summary>Směr od postavy ke kameře po zemi (jednotkový).</summary>
        private Vector3 ToCamera()
        {
            var toCamera = _camera.transform.position - _players.Self.position;
            toCamera.y = 0;
            return toCamera.normalized;
        }

        private void MarkNear(Vector3 center, Vector3 toCamera)
        {
            foreach (var crown in _fading)
            {
                crown.Near = false;
            }

            var cx = Mathf.FloorToInt(center.x);
            var cy = Mathf.FloorToInt(-center.z);
            var r = Mathf.CeilToInt(FadeRadius);
            for (var y = cy - r; y <= cy + r; y++)
            {
                for (var x = cx - r; x <= cx + r; x++)
                {
                    Crown crown;
                    if (!_crowns.TryGetValue(y * _width + x, out crown))
                    {
                        continue;
                    }

                    var dx = x + 0.5f - center.x;
                    var dy = y + 0.5f + center.z;
                    if (dx * dx + dy * dy > FadeRadius * FadeRadius || (crown.OnlyInFront && !InFront(x, y, toCamera)))
                    {
                        continue;
                    }

                    if (!_fading.Contains(crown))
                    {
                        _fading.Add(crown);
                    }

                    crown.Near = true;
                }
            }
        }

        /// <summary>Dlaždice leží mezi postavou a kamerou (ne vedle ani za postavou).</summary>
        private bool InFront(int x, int y, Vector3 toCamera)
        {
            var player = _players.Self.position;
            var offset = new Vector3(x + 0.5f - player.x, 0f, -(y + 0.5f) - player.z);
            var along = Vector3.Dot(offset, toCamera);
            var aside = (offset - toCamera * along).magnitude;
            return along > 0.3f && aside < 1.2f;
        }

        /// <summary>Průhledná koruna má průhledný materiál, úplně neprůhledná zase obyčejný (kvůli stínům a pořadí kreslení).</summary>
        private static void Apply(Crown crown)
        {
            var opaque = crown.Alpha >= 1f;
            crown.Renderer.sharedMaterial = opaque ? Palette.Lit : Palette.Fade;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(crown.Color.r, crown.Color.g, crown.Color.b, crown.Alpha));
            crown.Renderer.SetPropertyBlock(block);
        }
    }
}

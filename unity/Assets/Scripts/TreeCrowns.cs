using System.Collections.Generic;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Koruny stromů blízko vlastní postavy (hlavně mezi postavou a kamerou) plynule zprůhlední,
    /// aby hráč viděl, kde stojí. Když postava odejde, koruny zase zneprůhlední.
    /// Kolize s kmenem počítá server (Movement v Core), tohle je jen vzhled.
    /// </summary>
    public class TreeCrowns : MonoBehaviour
    {
        private const float FadeRadius = 2.3f;
        private const float TowardCamera = 1.2f;
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
        }

        public void Init(int worldWidth)
        {
            _width = worldWidth;
        }

        public void Add(int tileIndex, GameObject crown, Color color)
        {
            _crowns[tileIndex] = new Crown { Renderer = crown.GetComponent<Renderer>(), Color = color };
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

            MarkNear(FadeCenter());
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
            var toCamera = _camera.transform.position - _players.Self.position;
            toCamera.y = 0;
            return _players.Self.position + toCamera.normalized * TowardCamera;
        }

        private void MarkNear(Vector3 center)
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
                    if (dx * dx + dy * dy > FadeRadius * FadeRadius)
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

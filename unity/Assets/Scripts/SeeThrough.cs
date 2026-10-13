using System.Collections.Generic;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Koruny stromů a stavby blízko vlastní postavy (hlavně mezi postavou a kamerou) plynule zprůhlední,
    /// aby hráč viděl, kde stojí. Když postava odejde, zase zneprůhlední.
    /// Kolize počítá server (Movement v Core), tohle je jen vzhled.
    /// </summary>
    public class SeeThrough : MonoBehaviour
    {
        private const float FadeRadius = 2.6f;

        // Výška, ve které koruny postavu zakrývají (střed koruny, v metrech).
        private const float CoverHeight = 3.2f;
        private const float FadedAlpha = 0.28f;
        private const float FadeSpeed = 5f;

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Dictionary<Vector2Int, List<Entry>> _grid = new Dictionary<Vector2Int, List<Entry>>();
        private readonly List<Entry> _fading = new List<Entry>();
        private PlayerViews _players;
        private Camera _camera;

        /// <summary>Jedna věc, která umí zprůhlednět: renderer, barva, poloha na mapě a současná průhlednost.</summary>
        private sealed class Entry
        {
            public Renderer Renderer;
            public Color Color;
            public Vector2 Position;
            public Vector2Int Cell;
            public float Alpha = 1f;
            public bool Near;
            public bool OnlyInFront;
        }

        /// <summary>
        /// Přidá věc pod klíčem (strom: číslo dlaždice, stavba: záporné číslo stavby) s polohou na mapě (x, y).
        /// onlyInFront: zprůhlední jen mezi postavou a kamerou (stavby, ať je vidět, co hráč postavil),
        /// jinak v celém okolí postavy (koruny).
        /// </summary>
        public void Add(int key, GameObject go, Color color, double x, double y, bool onlyInFront = false)
        {
            Remove(key);
            var entry = new Entry
            {
                Renderer = go.GetComponent<Renderer>(),
                Color = color,
                Position = new Vector2((float)x, (float)y),
                Cell = new Vector2Int(Mathf.FloorToInt((float)x), Mathf.FloorToInt((float)y)),
                OnlyInFront = onlyInFront,
            };
            _entries[key] = entry;
            List<Entry> cell;
            if (!_grid.TryGetValue(entry.Cell, out cell))
            {
                cell = new List<Entry>();
                _grid[entry.Cell] = cell;
            }

            cell.Add(entry);
        }

        /// <summary>Věc zmizela (rozebraná stavba).</summary>
        public void Remove(int key)
        {
            Entry entry;
            if (_entries.TryGetValue(key, out entry))
            {
                _fading.Remove(entry);
                _grid[entry.Cell].Remove(entry);
                _entries.Remove(key);
            }
        }

        /// <summary>Začne sledovat vlastní postavu (volá se, až existují hráči a kamera).</summary>
        public void Follow(PlayerViews players, Camera camera)
        {
            _players = players;
            _camera = camera;
        }

        /// <summary>Kolik věcí je teď průhledných (pro testy).</summary>
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
                var entry = _fading[i];
                var target = entry.Near ? FadedAlpha : 1f;
                entry.Alpha = Mathf.MoveTowards(entry.Alpha, target, FadeSpeed * Time.deltaTime);
                Apply(entry);
                if (!entry.Near && entry.Alpha >= 1f)
                {
                    _fading.RemoveAt(i);
                }
            }
        }

        /// <summary>Střed oblasti: postava, posunutá ke kameře (tam koruny postavu zakrývají).</summary>
        private Vector2 FadeCenter()
        {
            // Čím níž je kamera skloněná, tím dál ke kameře stojí koruny, které postavu zakrývají.
            var pitch = Mathf.Clamp(_camera.transform.eulerAngles.x, 15f, 89f) * Mathf.Deg2Rad;
            return PlayerOnMap() + ToCamera() * (CoverHeight / Mathf.Tan(pitch));
        }

        /// <summary>Poloha postavy na mapě (x, y; y roste na jih).</summary>
        private Vector2 PlayerOnMap()
        {
            var position = _players.Self.position;
            return new Vector2(position.x, -position.z);
        }

        /// <summary>Směr od postavy ke kameře na mapě (jednotkový).</summary>
        private Vector2 ToCamera()
        {
            var toCamera = _camera.transform.position - _players.Self.position;
            return new Vector2(toCamera.x, -toCamera.z).normalized;
        }

        private void MarkNear(Vector2 center, Vector2 toCamera)
        {
            foreach (var entry in _fading)
            {
                entry.Near = false;
            }

            var r = Mathf.CeilToInt(FadeRadius) + 1;
            var cx = Mathf.FloorToInt(center.x);
            var cy = Mathf.FloorToInt(center.y);
            for (var y = cy - r; y <= cy + r; y++)
            {
                for (var x = cx - r; x <= cx + r; x++)
                {
                    List<Entry> cell;
                    if (!_grid.TryGetValue(new Vector2Int(x, y), out cell))
                    {
                        continue;
                    }

                    foreach (var entry in cell)
                    {
                        if ((entry.Position - center).sqrMagnitude > FadeRadius * FadeRadius
                            || (entry.OnlyInFront && !InFront(entry.Position, toCamera)))
                        {
                            continue;
                        }

                        if (!_fading.Contains(entry))
                        {
                            _fading.Add(entry);
                        }

                        entry.Near = true;
                    }
                }
            }
        }

        /// <summary>Věc leží mezi postavou a kamerou (ne vedle ani za postavou).</summary>
        private bool InFront(Vector2 position, Vector2 toCamera)
        {
            var offset = position - PlayerOnMap();
            var along = Vector2.Dot(offset, toCamera);
            var aside = (offset - toCamera * along).magnitude;
            return along > 0.3f && aside < 1.2f;
        }

        /// <summary>Průhledná věc má průhledný materiál, úplně neprůhledná zase obyčejný (kvůli stínům a pořadí kreslení).</summary>
        private static void Apply(Entry entry)
        {
            var opaque = entry.Alpha >= 1f;
            entry.Renderer.sharedMaterial = opaque ? Palette.Lit : Palette.Fade;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(entry.Color.r, entry.Color.g, entry.Color.b, entry.Alpha));
            entry.Renderer.SetPropertyBlock(block);
        }
    }
}

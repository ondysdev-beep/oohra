using System.Collections.Generic;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Věci, které zakrývají vlastní postavu před kamerou (koruny stromů, stavby), plynule zprůhlední.
    /// Když postavu přestanou zakrývat, zase zneprůhlední. Kolize počítá server, tohle je jen vzhled.
    /// <para>
    /// Jak se pozná zakrytí: paprsky z kamery na postavu (od nohou po hlavu) stoupají od postavy ke kameře
    /// pod úhlem sklonu kamery. Věc zakrývá, když leží ve směru ke kameře a v její vzdálenosti
    /// paprsky procházejí výškou, ve které věc je (od spodku po vršek). Funguje pro každý sklon i otočení kamery.
    /// </para>
    /// </summary>
    public class SeeThrough : MonoBehaviour
    {
        private const float FadedAlpha = 0.28f;
        private const float FadeSpeed = 5f;

        // Část postavy, která má být vidět (od nohou po hlavu), a rezerva do stran.
        private const float FeetHeight = 0.2f;
        private const float SideClearance = 0.5f;

        // Jak blízko u kamery (metry) musí koruna být, aby zprůhledněla, i když nestojí mezi kamerou a postavou.
        private const float CameraClearance = 2f;

        // Nejdál, kde se zakrývající věci hledají (při nízkém sklonu kamery).
        private const float MaxSearch = 30f;

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Dictionary<Vector2Int, List<Entry>> _grid = new Dictionary<Vector2Int, List<Entry>>();
        private readonly List<Entry> _fading = new List<Entry>();
        private float _tallest;
        private PlayerViews _players;
        private Camera _camera;

        /// <summary>Jedna věc, která umí zprůhlednět: kde je, jak je široká a v jaké výšce.</summary>
        private sealed class Entry
        {
            public FadeTarget Target;
            public Vector2 Position;
            public Vector2Int Cell;
            public float Radius;
            public float Bottom;
            public float Top;
            public float Alpha = 1f;
            public bool Near;

            // Kmen stromu pod korunou (poloměr, 0 = bez kmene) a kde začíná (zem).
            public float TrunkRadius;
            public float Base;

            // Kamera je v koruně: schová se úplně, ne jen zprůhlední.
            public bool AroundCamera;
        }

        /// <summary>
        /// Přidá věc pod klíčem (strom: číslo dlaždice, stavba: záporné číslo stavby). (x, y) je střed na mapě,
        /// radius polovina šířky, bottom a top výška spodku a vršku v metrech. color: barva tvaru z kódu,
        /// null = vlastní model z katalogu vzhledu (zachová své materiály).
        /// </summary>
        public void Add(int key, GameObject go, Color? color, double x, double y, float radius, float bottom, float top, float trunkRadius = 0f, float trunkBase = 0f)
        {
            Remove(key);
            var entry = new Entry
            {
                TrunkRadius = trunkRadius,
                Base = trunkBase,
                Target = new FadeTarget(go, color),
                Position = new Vector2((float)x, (float)y),
                Cell = new Vector2Int(Mathf.FloorToInt((float)x), Mathf.FloorToInt((float)y)),
                Radius = radius,
                Bottom = bottom,
                Top = top,
            };
            _entries[key] = entry;
            _tallest = Mathf.Max(_tallest, top);
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

            MarkCovering();
            for (var i = _fading.Count - 1; i >= 0; i--)
            {
                var entry = _fading[i];
                var target = entry.Near ? (entry.AroundCamera ? 0f : FadedAlpha) : 1f;
                entry.Alpha = Mathf.MoveTowards(entry.Alpha, target, FadeSpeed * Time.deltaTime);
                Apply(entry);
                if (!entry.Near && entry.Alpha >= 1f)
                {
                    _fading.RemoveAt(i);
                }
            }
        }

        private void MarkCovering()
        {
            foreach (var entry in _fading)
            {
                entry.Near = false;
                entry.AroundCamera = false;
            }

            var player = new Vector2(_players.Self.position.x, -_players.Self.position.z);
            // Výšky se počítají od podlahy, na které postava stojí (v patře od podlahy patra, ne od země).
            var floor = _players.Self.position.y - PlayerViews.BodyHeight / 2;
            var toCameraUnity = _camera.transform.position - _players.Self.position;
            var toCamera = new Vector2(toCameraUnity.x, -toCameraUnity.z);
            var horizontal = toCamera.magnitude;
            toCamera = horizontal > 0.001f ? toCamera / horizontal : Vector2.up;

            // Jak rychle paprsky ke kameře stoupají (výška na metr vodorovně).
            var rise = horizontal > 0.001f ? toCameraUnity.y / horizontal : 1000f;
            // Hledá se až ke kameře a kousek za ni (koruny kolem kamery).
            var reach = Mathf.Min(MaxSearch, Mathf.Max(horizontal + 10f, Mathf.Max(0f, _tallest - floor - FeetHeight) / Mathf.Max(rise, 0.05f) + 3f));
            var r = Mathf.CeilToInt(reach);
            var cx = Mathf.FloorToInt(player.x);
            var cy = Mathf.FloorToInt(player.y);
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
                        var around = AroundCamera(entry.Position, entry.Radius, entry.Bottom - floor, entry.Top - floor, player + toCamera * horizontal, toCameraUnity.y);
                        // Koruna, nebo kmen pod ní (kamera za postavou se dívá kolem kmenů, ne přes koruny).
                        var trunk = entry.TrunkRadius > 0f && Covers(entry.Position, entry.TrunkRadius, entry.Base - floor, entry.Bottom - floor, player, toCamera, rise);
                        if (!around && !trunk && !Covers(entry.Position, entry.Radius, entry.Bottom - floor, entry.Top - floor, player, toCamera, rise))
                        {
                            continue;
                        }

                        entry.AroundCamera = around;

                        if (!_fading.Contains(entry))
                        {
                            _fading.Add(entry);
                        }

                        entry.Near = true;
                    }
                }
            }
        }

        /// <summary>
        /// Věc (střed na mapě, polovina šířky, výška spodku a vršku) leží mezi kamerou a postavou tak,
        /// že zakrývá aspoň část postavy. toCamera: směr od postavy ke kameře na mapě (jednotkový),
        /// rise: o kolik metrů paprsek ke kameře stoupne na metr vodorovně.
        /// </summary>
        public static bool Covers(Vector2 position, float radius, float bottom, float top, Vector2 player, Vector2 toCamera, float rise)
        {
            var offset = position - player;
            var along = Vector2.Dot(offset, toCamera);
            var aside = (offset - toCamera * along).magnitude;
            if (aside > radius + SideClearance)
            {
                return false;
            }

            // Věc u země (stavba) musí stát před postavou. Koruna nad hlavou zakrývá i přímo nad postavou.
            if (bottom < PlayerViews.BodyHeight && along < 0.2f)
            {
                return false;
            }

            var near = Mathf.Max(0f, along - radius);
            var far = along + radius;
            if (far <= 0f)
            {
                return false;
            }

            // Paprsky od nohou a od hlavy: ve vzdálenosti věci musí aspoň jeden projít její výškou.
            var lowest = FeetHeight + near * rise;
            var highest = PlayerViews.BodyHeight + far * rise;
            return lowest <= top && highest >= bottom;
        }

        /// <summary>
        /// Kamera je v koruně nebo těsně u ní (vysoké stromy 15 až 25 m: kamera je níž než jejich vršky). Taková
        /// koruna by zakryla kus obrazu, i když nestojí přímo mezi kamerou a postavou.
        /// </summary>
        public static bool AroundCamera(Vector2 position, float radius, float bottom, float top, Vector2 camera, float cameraHeight)
        {
            return (position - camera).magnitude < radius + CameraClearance && cameraHeight > bottom - CameraClearance && cameraHeight < top + CameraClearance;
        }

        /// <summary>Průhledná věc má průhledný materiál, úplně neprůhledná zase obyčejný (kvůli stínům a pořadí kreslení).</summary>
        private static void Apply(Entry entry)
        {
            entry.Target.SetAlpha(entry.Alpha);
        }
    }
}

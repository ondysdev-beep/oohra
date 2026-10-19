using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Převod mezi souřadnicemi hry (dlaždice x, y; y roste na jih) a Unity (x, výška, z; z roste na sever).
    /// Mapa pak ze shora vypadá stejně jako v prototypu a v TestClientu (řádek 0 nahoře).
    /// Výška se počítá od terénu (kopce, Terrain): height = kolik metrů nad zemí v tom místě.
    /// </summary>
    public static class WorldCoords
    {
        /// <summary>Svět od serveru s výškami terénu (bez něj je zem rovná ve výšce 0).</summary>
        public static WorldView Terrain { get; set; }

        /// <summary>Výška terénu v bodě (x, y) na mapě (metry nad hladinou).</summary>
        public static float Ground(double x, double y)
        {
            return Terrain != null ? Terrain.ElevationAt(x, y) : 0f;
        }

        /// <summary>Bod v Unity height metrů nad zemí v místě (x, y).</summary>
        public static Vector3 ToUnity(double x, double y, float height = 0f)
        {
            return new Vector3((float)x, Ground(x, y) + height, -(float)y);
        }

        /// <summary>Bod v Unity v pevné výšce (nezávisle na terénu), třeba hladina vody.</summary>
        public static Vector3 ToUnityAt(double x, double y, float absoluteHeight)
        {
            return new Vector3((float)x, absoluteHeight, -(float)y);
        }

        public static void FromUnity(Vector3 position, out double x, out double y)
        {
            x = position.x;
            y = -position.z;
        }

        /// <summary>
        /// Kam paprsek (třeba od kurzoru) dopadne na terén: postupuje po kouscích, až je pod zemí, a pak upřesní.
        /// Vrací false, když zem netrefí (míří do nebe).
        /// </summary>
        public static bool RaycastGround(Ray ray, out Vector3 hit, float maxDistance = 300f)
        {
            const float step = 0.5f;
            var previous = 0f;
            for (var t = step; t <= maxDistance; t += step)
            {
                if (Below(ray.GetPoint(t)))
                {
                    var (low, high) = (previous, t);
                    for (var i = 0; i < 12; i++)
                    {
                        var mid = (low + high) / 2;
                        if (Below(ray.GetPoint(mid))) high = mid;
                        else low = mid;
                    }

                    hit = ray.GetPoint(high);
                    return true;
                }

                previous = t;
            }

            hit = Vector3.zero;
            return false;
        }

        private static bool Below(Vector3 point)
        {
            double x, y;
            FromUnity(point, out x, out y);
            return point.y <= Ground(x, y);
        }
    }
}

using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Převod mezi souřadnicemi hry (dlaždice x, y; y roste na jih) a Unity (x, výška, z; z roste na sever).
    /// Mapa pak ze shora vypadá stejně jako v prototypu a v TestClientu (řádek 0 nahoře).
    /// </summary>
    public static class WorldCoords
    {
        public static Vector3 ToUnity(double x, double y, float height = 0f)
        {
            return new Vector3((float)x, height, -(float)y);
        }

        public static void FromUnity(Vector3 position, out double x, out double y)
        {
            x = position.x;
            y = -position.z;
        }
    }
}

using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Mřížka 12 × 12 bodů v rozhraní (SignBits): kreslení myší (levé tlačítko kreslí, pravé maže, jde i tažením)
    /// a vykreslení. Používá ji kreslení značky i znak podpisu.
    /// </summary>
    public sealed class BitGrid
    {
        private bool _paintValue;

        public static float Size(float cell)
        {
            return cell * SignBits.Size;
        }

        /// <summary>Nakreslí mřížku a zpracuje myš. Vrací true, když se kresba změnila.</summary>
        public bool Edit(Rect grid, byte[] bits, Color background, Color ink)
        {
            var changed = Paint(grid, bits);
            Draw(grid, bits, background, ink);
            return changed;
        }

        /// <summary>Jen vykreslí (třeba znak podpisu na značce nebo u těla).</summary>
        public static void Draw(Rect grid, byte[] bits, Color background, Color ink)
        {
            var cell = grid.width / SignBits.Size;
            var old = GUI.color;
            for (var cy = 0; cy < SignBits.Size; cy++)
            {
                for (var cx = 0; cx < SignBits.Size; cx++)
                {
                    GUI.color = SignBits.Get(bits, cx, cy) ? ink : background;
                    GUI.DrawTexture(new Rect(grid.x + cx * cell, grid.y + cy * cell, Mathf.Max(1f, cell - 1), Mathf.Max(1f, cell - 1)), Texture2D.whiteTexture);
                }
            }

            GUI.color = old;
        }

        private bool Paint(Rect grid, byte[] bits)
        {
            var e = Event.current;
            if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || !grid.Contains(e.mousePosition))
            {
                return false;
            }

            if (e.type == EventType.MouseDown)
            {
                _paintValue = e.button == 0;
            }

            var cell = grid.width / SignBits.Size;
            var cx = Mathf.Clamp((int)((e.mousePosition.x - grid.x) / cell), 0, SignBits.Size - 1);
            var cy = Mathf.Clamp((int)((e.mousePosition.y - grid.y) / cell), 0, SignBits.Size - 1);
            SignBits.Set(bits, cx, cy, _paintValue);
            e.Use();
            return true;
        }
    }
}

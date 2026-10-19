using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Čáry stavební mřížky kolem kurzoru v režimu stavby (políčka 2 × 2 m plánu, ke kterému se díl přichytí,
    /// nebo nového plánu). Slabě svítí, ať jsou vidět i v noci, a ke krajům blednou.
    /// </summary>
    public class GridLines : MonoBehaviour
    {
        // Kolik políček na každou stranu od kurzoru.
        private const int Reach = 3;
        private const float Height = 0.03f;
        private static readonly Color LineColor = new Color(0.85f, 0.9f, 1f, 0.35f);

        private LineRenderer[] _lines;

        /// <summary>Čáry jsou vidět (pro testy).</summary>
        public bool Visible
        {
            get { return _lines != null && _lines[0].enabled; }
        }

        private void Awake()
        {
            var count = (2 * Reach + 1) * 2;
            _lines = new LineRenderer[count];
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Čára mřížky");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.widthMultiplier = 0.04f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = Palette.Glow;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", LineColor);
                line.SetPropertyBlock(block);
                line.enabled = false;
                _lines[i] = line;
            }
        }

        /// <summary>Ukáže mřížku plánu kolem bodu (x, y) na mapě, ve výšce height (podlaží).</summary>
        public void Show(GridPlan plan, double x, double y, float height)
        {
            var (u, v) = BuildGrid.ToLocal(plan, x, y);
            var (cu, cv) = (Mathf.Round((float)u), Mathf.Round((float)v));
            var index = 0;
            for (var k = -Reach; k <= Reach; k++)
            {
                SetLine(_lines[index++], plan, cu - Reach, cv + k, cu + Reach, cv + k, height);
                SetLine(_lines[index++], plan, cu + k, cv - Reach, cu + k, cv + Reach, height);
            }
        }

        public void Hide()
        {
            if (_lines == null)
            {
                return;
            }

            foreach (var line in _lines)
            {
                line.enabled = false;
            }
        }

        private static void SetLine(LineRenderer line, GridPlan plan, float u1, float v1, float u2, float v2, float height)
        {
            var (x1, y1) = BuildGrid.ToWorld(plan, u1, v1);
            var (x2, y2) = BuildGrid.ToWorld(plan, u2, v2);
            line.SetPosition(0, WorldCoords.ToUnity(x1, y1, height + Height));
            line.SetPosition(1, WorldCoords.ToUnity(x2, y2, height + Height));
            line.enabled = true;
        }
    }
}

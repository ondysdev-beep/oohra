using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Svítící kruh na zemi pod vybranou věcí (rostlina, strom, skála, voda, oheň, stavba, později zvíře),
    /// aby hráč viděl, na co kliknul. Kruh jemně pulzuje.
    /// </summary>
    public class SelectionRing : MonoBehaviour
    {
        private const int Points = 48;
        private const float Height = 0.04f;
        private static readonly Color RingColor = new Color(1f, 0.85f, 0.35f);

        private ClientSession _session;
        private LineRenderer _line;

        /// <summary>Kruh je právě vidět (pro testy).</summary>
        public bool Visible
        {
            get { return _line != null && _line.enabled; }
        }

        public void Init(ClientSession session)
        {
            _session = session;
            _line = gameObject.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = false;
            _line.positionCount = Points;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.sharedMaterial = Palette.Glow;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", RingColor);
            _line.SetPropertyBlock(block);
            for (var i = 0; i < Points; i++)
            {
                var angle = i * Mathf.PI * 2 / Points;
                _line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            _line.enabled = false;
        }

        /// <summary>Poloměr kruhu podle toho, co je vybrané (v metrech).</summary>
        public static float RadiusFor(TargetKind kind)
        {
            switch (kind)
            {
                case TargetKind.Plant: return 0.35f;
                case TargetKind.Fire: return 0.5f;
                case TargetKind.Structure: return 0.7f;
                case TargetKind.Animal: return 0.7f;
                default: return 0.55f;
            }
        }

        private void Update()
        {
            var target = _session == null ? null : _session.Selected;
            _line.enabled = target != null;
            if (target == null)
            {
                return;
            }

            var pulse = 1f + 0.06f * Mathf.Sin(Time.time * 5f);
            var radius = RadiusFor(target.Kind) * pulse;
            var position = _session.PositionOf(target);
            transform.position = WorldCoords.ToUnity(position.x, position.y, Height);
            transform.localScale = new Vector3(radius, 1f, radius);
            _line.widthMultiplier = 0.06f;
        }
    }
}

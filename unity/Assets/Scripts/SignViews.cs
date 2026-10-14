using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Značky podle zpráv serveru (prototyp: signs on ground a ruins). Kresba v hlíně leží na zemi (tmavá hlína,
    /// světlé body), rytina na skále je šedá s tmavými body, stará rytina je vytesaná do kamene ruiny z obou stran.
    /// </summary>
    public class SignViews : MonoBehaviour
    {
        public const float GroundSize = 0.6f;

        public static readonly Color Clay = new Color(0.24f, 0.18f, 0.12f);
        public static readonly Color ClayInk = Palette.Hex("#e8d9bf");
        public static readonly Color Stone = Palette.Hex("#8b8478");
        public static readonly Color StoneInk = Palette.Hex("#3a342c");

        // Kámen ruiny z IslandView.BuildRuins: střed 0,3 m nad zemí, 0,6 × 0,6 × 0,2 m.
        private const float RuinHeight = 0.3f;
        private const float RuinFace = 0.5f;
        private const float RuinHalfDepth = 0.1f;

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private GameConnection _connection;
        private SignView[] _shown;

        public void Init(GameConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Počet značek ve scéně (pro testy).</summary>
        public int Count
        {
            get { return _views.Count; }
        }

        /// <summary>Obrázek kresby 12 × 12 (ostré body, bez rozmazání). Řádek 0 kresby je nahoře.</summary>
        public static Texture2D Texture(byte[] bits, Color background, Color ink)
        {
            var texture = new Texture2D(SignBits.Size, SignBits.Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (var y = 0; y < SignBits.Size; y++)
            {
                for (var x = 0; x < SignBits.Size; x++)
                {
                    texture.SetPixel(x, SignBits.Size - 1 - y, SignBits.Get(bits, x, y) ? ink : background);
                }
            }

            texture.Apply();
            return texture;
        }

        private void Update()
        {
            if (_connection == null || ReferenceEquals(_connection.Signs, _shown))
            {
                return;
            }

            _shown = _connection.Signs;
            var current = new HashSet<int>();
            foreach (var sign in _shown)
            {
                current.Add(sign.Id);
                if (!_views.ContainsKey(sign.Id))
                {
                    _views[sign.Id] = Create(sign);
                }
            }

            var removed = new List<int>();
            foreach (var id in _views.Keys)
            {
                if (!current.Contains(id)) removed.Add(id);
            }

            foreach (var id in removed)
            {
                Destroy(_views[id]);
                _views.Remove(id);
            }
        }

        private GameObject Create(SignView sign)
        {
            var root = new GameObject((sign.Ancient ? "Stará rytina " : "Značka ") + sign.Id);
            root.transform.SetParent(transform, false);
            root.transform.position = WorldCoords.ToUnity(sign.X, sign.Y);
            var texture = sign.Carved ? Texture(sign.Bits, Stone, StoneInk) : Texture(sign.Bits, Clay, ClayInk);
            if (sign.Ancient)
            {
                // Na přední a zadní straně kamene ruiny.
                Face(root.transform, texture, new Vector3(0f, RuinHeight, -RuinHalfDepth - 0.005f), Quaternion.identity, RuinFace);
                Face(root.transform, texture, new Vector3(0f, RuinHeight, RuinHalfDepth + 0.005f), Quaternion.Euler(0f, 180f, 0f), RuinFace);
            }
            else
            {
                Face(root.transform, texture, new Vector3(0f, 0.015f, 0f), Quaternion.Euler(90f, 0f, 0f), GroundSize);
            }

            Pickable.Attach(root, InteractionTarget.ForSign(sign.Id, sign.X, sign.Y));

            return root;
        }

        private static void Face(Transform parent, Texture2D texture, Vector3 position, Quaternion rotation, float size)
        {
            var face = IslandView.Primitive(PrimitiveType.Quad, "Kresba", parent, Color.white);
            face.transform.localPosition = position;
            face.transform.localRotation = rotation;
            face.transform.localScale = new Vector3(size, size, 1f);
            var renderer = face.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", texture);
            renderer.SetPropertyBlock(block);
        }
    }
}

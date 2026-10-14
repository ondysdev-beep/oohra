using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Vodní hladina: jedna velká průhledná lesklá plocha kousek pod souší (moře kolem ostrova i jezírka,
    /// jejich dno je níž, viz GroundLayout). Vlnky dělá normálová mapa, která se pomalu posouvá.
    /// Barva a vlnky jsou v katalogu vzhledu.
    /// </summary>
    public class WaterView : MonoBehaviour
    {
        // Hladina přesahuje ostrov do všech stran (moře až k obzoru).
        private const float Extent = 6f;
        private const int NormalSize = 64;

        private Material _material;
        private Vector2 _direction = new Vector2(0.8f, 0.6f);
        private float _speed;
        private float _tiling;

        public static WaterView Build(WorldView world, Transform parent)
        {
            var catalog = VisualCatalog.Current;
            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "Voda";
            water.transform.SetParent(parent);
            water.transform.position = new Vector3(world.Width / 2f, GroundLayout.WaterLevel, -world.Height / 2f);
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var size = Mathf.Max(world.Width, world.Height) * Extent;
            water.transform.localScale = new Vector3(size, size, 1f);
            if (Application.isPlaying) Destroy(water.GetComponent<Collider>());
            else DestroyImmediate(water.GetComponent<Collider>());

            var material = new Material(Palette.Water) { name = "Voda" };
            material.SetColor("_BaseColor", catalog.WaterColor);
            material.SetTexture("_BumpMap", catalog.WaterNormalMap != null ? catalog.WaterNormalMap : GeneratedNormalMap());
            material.SetFloat("_BumpScale", 0.35f);
            water.GetComponent<Renderer>().sharedMaterial = material;
            water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var view = water.AddComponent<WaterView>();
            view._material = material;
            view._speed = catalog.WaveSpeed;
            view._tiling = size / Mathf.Max(0.5f, catalog.WaveSize);
            material.SetTextureScale("_BaseMap", new Vector2(view._tiling, view._tiling));
            return view;
        }

        /// <summary>Normálová mapa vlnek z navazujícího šumu (lineární, RGB = směr povrchu).</summary>
        public static Texture2D GeneratedNormalMap()
        {
            var heights = new float[NormalSize, NormalSize];
            for (var y = 0; y < NormalSize; y++)
            {
                for (var x = 0; x < NormalSize; x++)
                {
                    heights[y, x] = Mathf.Sin((x + 3 * Mathf.Sin(y * 0.3f)) * Mathf.PI * 2 / 16) * 0.5f
                        + Mathf.Sin((y + 2 * Mathf.Sin(x * 0.4f)) * Mathf.PI * 2 / 21.333f) * 0.5f
                        + GroundView.Hash01(x, y, 3) * 0.15f;
                }
            }

            var texture = new Texture2D(NormalSize, NormalSize, TextureFormat.RGBA32, true, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "Vlnky",
            };
            for (var y = 0; y < NormalSize; y++)
            {
                for (var x = 0; x < NormalSize; x++)
                {
                    var dx = heights[y, (x + 1) % NormalSize] - heights[y, (x + NormalSize - 1) % NormalSize];
                    var dy = heights[(y + 1) % NormalSize, x] - heights[(y + NormalSize - 1) % NormalSize, x];
                    var normal = new Vector3(-dx, -dy, 1f).normalized;
                    texture.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
                }
            }

            texture.Apply();
            return texture;
        }

        private void Update()
        {
            if (_material == null)
            {
                return;
            }

            // URP/Lit posouvá všechny textury podle _BaseMap, takže se hýbe i normálová mapa.
            var offset = _direction * (Time.time * _speed / Mathf.Max(0.5f, VisualCatalog.Current.WaveSize));
            _material.SetTextureOffset("_BaseMap", new Vector2(offset.x % 1f, offset.y % 1f));
        }
    }
}

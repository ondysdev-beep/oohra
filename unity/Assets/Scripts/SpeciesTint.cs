using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Značka pro vlastní model rostliny (katalog vzhledu): tahle část (listy) se obarví barvou druhu.
    /// Když model žádnou takovou část nemá, obarví se celý. Textura by měla být spíš šedá, barva se s ní násobí.
    /// </summary>
    public class SpeciesTint : MonoBehaviour
    {
        /// <summary>Obarví model: části se SpeciesTint, a když žádné nejsou, všechny části.</summary>
        public static void Apply(GameObject model, Color color)
        {
            var marked = model.GetComponentsInChildren<SpeciesTint>();
            var renderers = marked.Length > 0
                ? System.Array.ConvertAll(marked, m => m.GetComponent<Renderer>())
                : model.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}

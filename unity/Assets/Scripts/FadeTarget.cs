using System.Collections.Generic;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Jeden objekt, který umí zprůhlednět (SeeThrough). Dva druhy:
    /// <list type="bullet">
    /// <item>tvar z kódu (kostka, koule) s barvou: střídá sdílené materiály OohraLit a OohraFade,</item>
    /// <item>vlastní model z katalogu vzhledu (barva null): zachová své materiály a textury, při zprůhlednění
    /// dostane jejich průhledné kopie a po zneprůhlednění zase své původní.</item>
    /// </list>
    /// </summary>
    public sealed class FadeTarget
    {
        private static readonly Dictionary<Material, Material> _transparentCopies = new Dictionary<Material, Material>();

        private readonly Renderer[] _renderers;
        private readonly Material[][] _originals;
        private readonly Color[][] _baseColors;
        private readonly Color? _color;

        /// <param name="color">Barva tvaru z kódu, nebo null u vlastního modelu (zprůhlední všechny jeho části).</param>
        public FadeTarget(GameObject go, Color? color)
        {
            _color = color;
            _renderers = color.HasValue ? new[] { go.GetComponent<Renderer>() } : go.GetComponentsInChildren<Renderer>();
            _originals = new Material[_renderers.Length][];
            _baseColors = new Color[_renderers.Length][];
            for (var i = 0; i < _renderers.Length; i++)
            {
                _originals[i] = _renderers[i].sharedMaterials;
                _baseColors[i] = new Color[_originals[i].Length];
                for (var j = 0; j < _originals[i].Length; j++)
                {
                    _baseColors[i][j] = BaseColor(_renderers[i], j, _originals[i][j]);
                }
            }
        }

        /// <summary>Nastaví průhlednost: 1 = neprůhledný (původní vzhled), menší = průhlednější.</summary>
        public void SetAlpha(float alpha)
        {
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_color.HasValue)
                {
                    SetPrimitive(_renderers[i], _color.Value, alpha);
                }
                else
                {
                    SetModel(_renderers[i], _originals[i], _baseColors[i], alpha);
                }
            }
        }

        private static void SetPrimitive(Renderer renderer, Color color, float alpha)
        {
            renderer.sharedMaterial = alpha >= 1f ? Palette.Lit : Palette.Fade;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            renderer.SetPropertyBlock(block);
        }

        private static void SetModel(Renderer renderer, Material[] originals, Color[] colors, float alpha)
        {
            var opaque = alpha >= 1f;
            if (opaque)
            {
                renderer.sharedMaterials = originals;
            }
            else
            {
                var faded = new Material[originals.Length];
                for (var j = 0; j < originals.Length; j++)
                {
                    faded[j] = TransparentCopy(originals[j]);
                }

                renderer.sharedMaterials = faded;
            }

            for (var j = 0; j < originals.Length; j++)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(colors[j].r, colors[j].g, colors[j].b, opaque ? colors[j].a : alpha));
                renderer.SetPropertyBlock(block, j);
            }
        }

        /// <summary>Barva části: obarvení z kódu (rostliny podle druhu), jinak barva materiálu.</summary>
        private static Color BaseColor(Renderer renderer, int index, Material original)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, index);
            if (block.isEmpty)
            {
                renderer.GetPropertyBlock(block);
            }

            if (!block.isEmpty && block.HasColor("_BaseColor"))
            {
                return block.GetColor("_BaseColor");
            }

            return original != null && original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white;
        }

        /// <summary>Průhledná kopie materiálu (jedna pro každý materiál, sdílí se).</summary>
        private static Material TransparentCopy(Material original)
        {
            if (original == null)
            {
                return Palette.Fade;
            }

            Material copy;
            if (!_transparentCopies.TryGetValue(original, out copy) || copy == null)
            {
                copy = new Material(original) { name = original.name + " (průhledný)" };
                Palette.MakeTransparent(copy);
                _transparentCopies[original] = copy;
            }

            return copy;
        }
    }
}

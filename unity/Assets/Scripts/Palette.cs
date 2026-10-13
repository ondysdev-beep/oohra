using Oohra.Core.World;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>Barvy a materiály šedé krabice. Barvy dlaždic jsou z prototypu (COL v part3.js).</summary>
    public static class Palette
    {
        /// <summary>
        /// Cesta k materiálu v Assets/Resources. Materiál musí být soubor v projektu: shader, který se
        /// hledá jen v kódu (Shader.Find), Unity do sestavené hry nepřibalí a objekty by byly růžové.
        /// Vytváří ho menu Oohra → Vytvořit scénu ostrova (a sestavení hry).
        /// </summary>
        public const string LitResource = "OohraLit";

        /// <summary>Materiál URP/Unlit pro věci, které svítí samy (plamen). Ve tmě nezčernají.</summary>
        public const string GlowResource = "OohraGlow";

        private static Material _lit;
        private static Material _glow;

        /// <summary>Sdílený materiál URP/Lit. Barvu jednotlivých objektů nastavuje SetColor.</summary>
        public static Material Lit
        {
            get { return Load(ref _lit, LitResource); }
        }

        /// <summary>Sdílený materiál URP/Unlit (plamen ohně). Barvu nastavuje SetGlow.</summary>
        public static Material Glow
        {
            get { return Load(ref _glow, GlowResource); }
        }

        private static Material Load(ref Material cache, string resource)
        {
            if (cache == null)
            {
                cache = Resources.Load<Material>(resource);
                if (cache == null)
                {
                    throw new MissingReferenceException("Chybí materiál Assets/Resources/" + resource + ".mat (menu Oohra → Vytvořit scénu ostrova).");
                }
            }

            return cache;
        }

        /// <summary>Obarví objekt materiálem, který svítí sám (nezávisle na světle).</summary>
        public static void SetGlow(GameObject go, Color color)
        {
            SetColor(go, color);
            go.GetComponent<Renderer>().sharedMaterial = Glow;
        }

        public static Color Tile(TileType tile, bool shore)
        {
            switch (tile)
            {
                case TileType.Sea: return shore ? Hex("#3a7282") : Hex("#2b5463");
                case TileType.Sand: return Hex("#cdb88a");
                case TileType.Grass: return Hex("#86a85a");
                case TileType.Forest: return Hex("#6f9449");
                case TileType.Rock: return Hex("#8b8478");
                case TileType.Bog: return Hex("#5d7048");
                case TileType.Pond: return Hex("#3f7f90");
                default: return Color.magenta;
            }
        }

        /// <summary>Nastaví barvu objektu bez vytváření nového materiálu.</summary>
        public static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = Lit;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Barva materiálu v batohu a ve světě.</summary>
        public static Color Material(Oohra.Core.Items.ItemKind kind)
        {
            return kind == Oohra.Core.Items.ItemKind.Stick ? Hex("#8a6a42") : Hex("#9a948a");
        }

        /// <summary>Barva listů druhu rostliny.</summary>
        public static Color Leaf(Oohra.Protocol.SpeciesView species)
        {
            return Hsl(species.Hue, species.Saturation, species.Lightness);
        }

        /// <summary>Barva z HSL (odstín 0 až 360, sytost a světlost 0 až 100), jak ji používá prototyp.</summary>
        public static Color Hsl(double hue, double saturation, double lightness)
        {
            var h = (float)(((hue % 360) + 360) % 360 / 360.0);
            var s = (float)(saturation / 100.0);
            var l = (float)(lightness / 100.0);
            var q = l < 0.5f ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            return new Color(HueToRgb(p, q, h + 1f / 3), HueToRgb(p, q, h), HueToRgb(p, q, h - 1f / 3));
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6) return p + (q - p) * 6 * t;
            if (t < 1f / 2) return q;
            if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
            return p;
        }
    }
}

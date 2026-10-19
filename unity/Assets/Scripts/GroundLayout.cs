using System.Collections.Generic;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Výpočty pro zem (Unity Terrain): výšky (souš rovně ve výšce 0, dno moře a jezírek pod hladinou)
    /// a míchání textur (každá dlaždice svou texturou, na hranách plynulý přechod).
    /// <para>
    /// Pobřeží není schodovité: bod je souš, když v kroužku kolem něj (poloměr ShoreRadius) převládá souš.
    /// Tím se rohy dlaždic zakulatí, ale kde postava smí stát (aspoň 28 cm od vody), zůstane souš.
    /// </para>
    /// Jen počítá pole čísel, Terrain z nich staví GroundView, pěnu FoamView.
    /// </summary>
    public static class GroundLayout
    {
        // Vrstvy textur zemi (pořadí = pořadí v Terrain).
        public const int Grass = 0;
        public const int Sand = 1;
        public const int Forest = 2;
        public const int Rock = 3;
        public const int Bog = 4;
        public const int Highland = 5;
        public const int LayerCount = 6;

        /// <summary>Nejhlubší místo (hluboké moře) pod úrovní souše, v metrech.</summary>
        public const float Depth = 1f;

        /// <summary>Výška vodní hladiny (kousek pod souší, ať břeh není mokrý).</summary>
        public const float WaterLevel = -0.06f;

        /// <summary>
        /// Poloměr kroužku pro zakulacení pobřeží (m). Musí zůstat pod 0,95 m, jinak by se pod vodu dostal
        /// i roh souše, kde postava (poloměr 0,28 m) smí stát.
        /// </summary>
        public const float ShoreRadius = 0.6f;

        // Poloměr kroužku pro míchání textur (m): přechod mezi dlaždicemi je asi dvakrát tak široký.
        private const float BlendRadius = 0.7f;

        // Kolik bodů výšek a textur připadá na metr.
        private const int SamplesPerMeter = 4;

        private static readonly Vector2[] _shoreDisc = Disc(ShoreRadius);
        private static readonly Vector2[] _blendDisc = Disc(BlendRadius);
        private static readonly Vector2[] _corners =
        {
            new Vector2(-ShoreRadius, -ShoreRadius), new Vector2(ShoreRadius, -ShoreRadius),
            new Vector2(-ShoreRadius, ShoreRadius), new Vector2(ShoreRadius, ShoreRadius),
        };

        /// <summary>Textura dlaždice: moře a brod mají písečné dno, jezírko bahnité, vrstvená skála je zatím skála.</summary>
        public static int LayerOf(TileType tile)
        {
            switch (tile)
            {
                case TileType.Sand:
                case TileType.Sea:
                case TileType.Ford:
                case TileType.Shallows:
                    return Sand;
                case TileType.Forest: return Forest;
                case TileType.Rock:
                case TileType.LayeredRock:
                case TileType.CaveFloor:
                case TileType.CaveWall:
                    return Rock;
                case TileType.Highland: return Highland;
                case TileType.Bog:
                case TileType.Mire:
                case TileType.Pond:
                    return Bog;
                default: return Grass;
            }
        }

        /// <summary>Hloubka dna pod souší (m): moře u břehu mělčí, jezírko mělké, brod po kotníky, souš 0.</summary>
        public static float DepthOf(TileType tile, bool shore)
        {
            switch (tile)
            {
                case TileType.Sea: return shore ? 0.55f : Depth;
                case TileType.Pond: return 0.6f;
                case TileType.Ford: return 0.15f;
                case TileType.Shallows: return 0.3f;
                // Močál: stojatá voda kousek nad bahnem.
                case TileType.Mire: return 0.12f;
                default: return 0f;
            }
        }

        /// <summary>Nejvyšší místo terénu (metry nad hladinou) s rezervou: Terrain musí být tak vysoký.</summary>
        public static float MaxElevation(WorldView world)
        {
            var max = 0f;
            foreach (var e in world.Elevation)
            {
                if (e > max) max = e;
            }

            return max + 1f;
        }

        public static bool IsWater(TileType tile)
        {
            return tile == TileType.Sea || tile == TileType.Pond || tile == TileType.Ford || tile == TileType.Mire || tile == TileType.Shallows;
        }

        /// <summary>Počet bodů výšek na stranu: 2^n + 1, asi čtyři na metr (jak chce Terrain).</summary>
        public static int HeightResolution(int size)
        {
            return PowerOfTwo(size, 32) + 1;
        }

        /// <summary>Počet bodů míchání textur na stranu: 2^n, asi čtyři na metr.</summary>
        public static int AlphaResolution(int size)
        {
            return PowerOfTwo(size, 16);
        }

        /// <summary>
        /// Jak moc je bod (x, y) na mapě souš (0 až 1): plynulý přechod mezi středy sousedních dlaždic (souš 1, voda 0).
        /// Bod je na souši, když je to aspoň polovina. Šikmé pobřeží je tak rovná čára, ne schody po dlaždicích,
        /// a roh ostrůvku se zakulatí jen kousek (kde postava smí stát, zůstane souš). Mimo mapu je moře.
        /// </summary>
        public static float LandFraction(WorldView world, double x, double y)
        {
            var (cx, cy) = (System.Math.Floor(x - 0.5), System.Math.Floor(y - 0.5));
            var (fx, fy) = ((float)(x - 0.5 - cx), (float)(y - 0.5 - cy));
            var top = Mathf.Lerp(Land(world, cx, cy), Land(world, cx + 1, cy), fx);
            var bottom = Mathf.Lerp(Land(world, cx, cy + 1), Land(world, cx + 1, cy + 1), fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        private static float Land(WorldView world, double tileX, double tileY)
        {
            return IsWaterAt(world, tileX + 0.5, tileY + 0.5) ? 0f : 1f;
        }

        /// <summary>
        /// Výšky pro Terrain.SetHeights: [řádek od jihu, sloupec od západu], 0 = nejhlubší dno, 1 = souš.
        /// Kde převládá souš, je rovně. Jinak dno klesá od břehu do hloubky okolních vodních dlaždic.
        /// </summary>
        public static float[,] Heights(WorldView world, int resolution)
        {
            var heights = new float[resolution, resolution];
            var range = Depth + MaxElevation(world);
            for (var row = 0; row < resolution; row++)
            {
                for (var column = 0; column < resolution; column++)
                {
                    var x = column * (double)world.Width / (resolution - 1);
                    var y = world.Height - row * (double)world.Height / (resolution - 1);
                    // Pod vodou dno (záporně), na souši výška terénu (kopce), obojí od nejhlubšího dna.
                    var depth = DepthAt(world, x, y);
                    var level = depth > 0f ? -depth : world.ElevationAt(x, y);
                    heights[row, column] = (level + Depth) / range;
                }
            }

            return heights;
        }

        /// <summary>
        /// Míchání textur pro Terrain.SetAlphamaps: [řádek od jihu, sloupec od západu, vrstva], součet vrstev 1.
        /// Každá vrstva má takový podíl, jaký podíl kroužku kolem bodu pokrývají její dlaždice.
        /// </summary>
        public static float[,,] Alphamaps(WorldView world, int resolution)
        {
            var maps = new float[resolution, resolution, LayerCount];
            for (var row = 0; row < resolution; row++)
            {
                for (var column = 0; column < resolution; column++)
                {
                    var x = (column + 0.5) * world.Width / resolution;
                    var y = world.Height - (row + 0.5) * world.Height / resolution;
                    var center = LayerOf(TileAt(world, x, y));
                    if (SameLayerAround(world, x, y, center))
                    {
                        maps[row, column, center] = 1f;
                        continue;
                    }

                    foreach (var offset in _blendDisc)
                    {
                        maps[row, column, LayerOf(TileAt(world, x + offset.x, y + offset.y))] += 1f / _blendDisc.Length;
                    }
                }
            }

            return maps;
        }

        private static float DepthAt(WorldView world, double x, double y)
        {
            var land = LandFraction(world, x, y);
            if (land >= 0.5f)
            {
                return 0f;
            }

            // Hloubka plynule mezi středy vodních dlaždic (mělčina přechází do hlubokého moře bez schodů),
            // u břehu (souš těsně pod polovinou) mělčí.
            var water = WaterDepth(world, x, y);
            return water * Mathf.SmoothStep(0f, 1f, (0.5f - land) * 2f);
        }

        /// <summary>Hloubka vody v bodě: plynule mezi středy okolních vodních dlaždic (souš se nepočítá).</summary>
        private static float WaterDepth(WorldView world, double x, double y)
        {
            var (cx, cy) = (System.Math.Floor(x - 0.5), System.Math.Floor(y - 0.5));
            var (fx, fy) = ((float)(x - 0.5 - cx), (float)(y - 0.5 - cy));
            float total = 0f, weight = 0f;
            for (var j = 0; j <= 1; j++)
            {
                for (var i = 0; i <= 1; i++)
                {
                    var depth = DepthOfPoint(world, cx + i + 0.5, cy + j + 0.5);
                    var w = (i == 0 ? 1 - fx : fx) * (j == 0 ? 1 - fy : fy);
                    if (depth > 0f)
                    {
                        total += depth * w;
                        weight += w;
                    }
                }
            }

            return weight > 0f ? total / weight : Depth;
        }

        private static bool SameDepthAtCorners(WorldView world, double x, double y)
        {
            var first = DepthOfPoint(world, x + _corners[0].x, y + _corners[0].y);
            for (var i = 1; i < _corners.Length; i++)
            {
                if (DepthOfPoint(world, x + _corners[i].x, y + _corners[i].y) != first)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameLayerAround(WorldView world, double x, double y, int layer)
        {
            for (var i = 0; i < 4; i++)
            {
                if (LayerOf(TileAt(world, x + (i % 2 == 0 ? -BlendRadius : BlendRadius), y + (i < 2 ? -BlendRadius : BlendRadius))) != layer)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsWaterAt(WorldView world, double x, double y)
        {
            var tx = (int)System.Math.Floor(x);
            var ty = (int)System.Math.Floor(y);
            return tx < 0 || ty < 0 || tx >= world.Width || ty >= world.Height || IsWater(world.Tiles[ty * world.Width + tx]);
        }

        private static float DepthOfPoint(WorldView world, double x, double y)
        {
            var tx = (int)System.Math.Floor(x);
            var ty = (int)System.Math.Floor(y);
            if (tx < 0 || ty < 0 || tx >= world.Width || ty >= world.Height)
            {
                return Depth;
            }

            var index = ty * world.Width + tx;
            return DepthOf(world.Tiles[index], world.Shore[index]);
        }

        private static TileType TileAt(WorldView world, double x, double y)
        {
            var tx = Mathf.Clamp((int)System.Math.Floor(x), 0, world.Width - 1);
            var ty = Mathf.Clamp((int)System.Math.Floor(y), 0, world.Height - 1);
            return world.TileAt(tx, ty);
        }

        /// <summary>
        /// Body v kroužku o poloměru radius (mřížka 8 × 8, jen ty uvnitř kruhu). Mřížka je souměrná a žádný bod
        /// neleží přesně na ose, takže na rovné hraně dlaždic vyjde přesně polovina.
        /// </summary>
        private static Vector2[] Disc(float radius)
        {
            var points = new List<Vector2>();
            for (var i = 0; i < 8; i++)
            {
                for (var j = 0; j < 8; j++)
                {
                    var point = new Vector2((i - 3.5f) / 3.5f * radius, (j - 3.5f) / 3.5f * radius);
                    if (point.magnitude <= radius + 1e-4f)
                    {
                        points.Add(point);
                    }
                }
            }

            return points.ToArray();
        }

        private static int PowerOfTwo(int size, int minimum)
        {
            var resolution = minimum;
            while (resolution < SamplesPerMeter * size && resolution < 4096)
            {
                resolution *= 2;
            }

            return resolution;
        }
    }
}

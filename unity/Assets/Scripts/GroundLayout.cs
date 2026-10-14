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
        public const int LayerCount = 5;

        /// <summary>Nejhlubší místo (hluboké moře) pod úrovní souše, v metrech.</summary>
        public const float Depth = 0.8f;

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

        /// <summary>Textura dlaždice: moře má písečné dno, jezírko bahnité.</summary>
        public static int LayerOf(TileType tile)
        {
            switch (tile)
            {
                case TileType.Sand:
                case TileType.Sea:
                    return Sand;
                case TileType.Forest: return Forest;
                case TileType.Rock: return Rock;
                case TileType.Bog:
                case TileType.Pond:
                    return Bog;
                default: return Grass;
            }
        }

        /// <summary>Hloubka dna pod souší (m): moře u břehu mělčí, jezírko mělké, souš 0.</summary>
        public static float DepthOf(TileType tile, bool shore)
        {
            switch (tile)
            {
                case TileType.Sea: return shore ? 0.35f : Depth;
                case TileType.Pond: return 0.45f;
                default: return 0f;
            }
        }

        public static bool IsWater(TileType tile)
        {
            return tile == TileType.Sea || tile == TileType.Pond;
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
        /// Kolik kroužku kolem bodu (x, y) na mapě je souš (0 až 1). Mimo mapu je moře.
        /// Bod je na souši, když je to aspoň polovina.
        /// </summary>
        public static float LandFraction(WorldView world, double x, double y)
        {
            var corners = 0;
            for (var i = 0; i < 4; i++)
            {
                if (!IsWaterAt(world, x + (i % 2 == 0 ? -ShoreRadius : ShoreRadius), y + (i < 2 ? -ShoreRadius : ShoreRadius)))
                {
                    corners++;
                }
            }

            // Kroužek leží nejvýš na 2 × 2 dlaždicích a ty jsou v rozích: když se rohy shodují, výsledek je jasný.
            if (corners == 0 || corners == 4)
            {
                return corners / 4f;
            }

            var land = 0;
            foreach (var offset in _shoreDisc)
            {
                if (!IsWaterAt(world, x + offset.x, y + offset.y))
                {
                    land++;
                }
            }

            return (float)land / _shoreDisc.Length;
        }

        /// <summary>
        /// Výšky pro Terrain.SetHeights: [řádek od jihu, sloupec od západu], 0 = nejhlubší dno, 1 = souš.
        /// Kde převládá souš, je rovně. Jinak dno klesá od břehu do hloubky okolních vodních dlaždic.
        /// </summary>
        public static float[,] Heights(WorldView world, int resolution)
        {
            var heights = new float[resolution, resolution];
            for (var row = 0; row < resolution; row++)
            {
                for (var column = 0; column < resolution; column++)
                {
                    var x = column * (double)world.Width / (resolution - 1);
                    var y = world.Height - row * (double)world.Height / (resolution - 1);
                    heights[row, column] = 1f - DepthAt(world, x, y) / Depth;
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

            // Hloubka okolních vodních dlaždic (na volné vodě stačí rohy kroužku, u břehu celý kroužek),
            // u břehu (souš těsně pod polovinou) mělčí.
            var samples = land > 0f || !SameDepthAtCorners(world, x, y) ? _shoreDisc : _corners;
            var total = 0f;
            var count = 0;
            foreach (var offset in samples)
            {
                var depth = DepthOfPoint(world, x + offset.x, y + offset.y);
                if (depth > 0f)
                {
                    total += depth;
                    count++;
                }
            }

            var water = count > 0 ? total / count : Depth;
            return water * Mathf.SmoothStep(0f, 1f, (0.5f - land) * 2f);
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

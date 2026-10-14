using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Výpočty pro zem (Unity Terrain): výšky (souš rovně ve výšce 0, dno moře a jezírek pod hladinou)
    /// a míchání textur (každá dlaždice svou texturou, na hranách plynulý přechod).
    /// Jen počítá pole čísel, Terrain z nich staví GroundView.
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

        // Jak daleko od bodu se hledá souš: bod je na souši, když se do ní dotkne (břehy zůstanou rovné).
        private const double ShoreReach = 0.45;

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

        /// <summary>Počet bodů výšek na stranu: 2^n + 1, asi dva na metr (jak chce Terrain).</summary>
        public static int HeightResolution(int size)
        {
            var resolution = 32;
            while (resolution < 2 * size && resolution < 4096)
            {
                resolution *= 2;
            }

            return resolution + 1;
        }

        /// <summary>Počet bodů míchání textur na stranu: 2^n, asi dva na metr.</summary>
        public static int AlphaResolution(int size)
        {
            var resolution = 16;
            while (resolution < 2 * size && resolution < 4096)
            {
                resolution *= 2;
            }

            return resolution;
        }

        /// <summary>
        /// Výšky pro Terrain.SetHeights: [řádek od jihu, sloupec od západu], 0 = nejhlubší dno, 1 = souš.
        /// Bod je na souši, když se kterákoli dlaždice do 45 cm od něj nedá přebrodit (souš zůstane celá rovná),
        /// jinak má průměrnou hloubku okolních vodních dlaždic (dno se ke břehu zvedá).
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
        /// Každý bod dostane texturu své dlaždice, pak se sousední body zprůměrují (měkký přechod asi 1 m).
        /// </summary>
        public static float[,,] Alphamaps(WorldView world, int resolution)
        {
            var layers = new int[resolution, resolution];
            for (var row = 0; row < resolution; row++)
            {
                for (var column = 0; column < resolution; column++)
                {
                    var x = (column + 0.5) * world.Width / resolution;
                    var y = world.Height - (row + 0.5) * world.Height / resolution;
                    layers[row, column] = LayerOf(TileAt(world, x, y));
                }
            }

            var maps = new float[resolution, resolution, LayerCount];
            for (var row = 0; row < resolution; row++)
            {
                for (var column = 0; column < resolution; column++)
                {
                    var count = 0;
                    for (var dr = -1; dr <= 1; dr++)
                    {
                        for (var dc = -1; dc <= 1; dc++)
                        {
                            var r = Mathf.Clamp(row + dr, 0, resolution - 1);
                            var c = Mathf.Clamp(column + dc, 0, resolution - 1);
                            maps[row, column, layers[r, c]] += 1f;
                            count++;
                        }
                    }

                    for (var layer = 0; layer < LayerCount; layer++)
                    {
                        maps[row, column, layer] /= count;
                    }
                }
            }

            return maps;
        }

        private static float DepthAt(WorldView world, double x, double y)
        {
            var total = 0f;
            for (var i = 0; i < 4; i++)
            {
                var sx = x + (i % 2 == 0 ? -ShoreReach : ShoreReach);
                var sy = y + (i < 2 ? -ShoreReach : ShoreReach);
                var depth = DepthOfPoint(world, sx, sy);
                if (depth <= 0f)
                {
                    return 0f;
                }

                total += depth;
            }

            return total / 4;
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
    }
}

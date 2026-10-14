using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Zem: výšky (souš rovně, dno pod vodou), míchání textur dlaždic a vygenerované textury.</summary>
    public class GroundTests
    {
        /// <summary>Ostrůvek 2 × 2 trávy uprostřed moře 6 × 6 (moře u břehu je mělčí).</summary>
        private static WorldView Islet()
        {
            var tiles = new TileType[36];
            var shore = new bool[36];
            for (var i = 0; i < tiles.Length; i++)
            {
                var x = i % 6;
                var y = i / 6;
                var land = x >= 2 && x <= 3 && y >= 2 && y <= 3;
                tiles[i] = land ? TileType.Grass : TileType.Sea;
                shore[i] = !land && x >= 1 && x <= 4 && y >= 1 && y <= 4;
            }

            return new WorldView { Width = 6, Height = 6, Tiles = tiles, Shore = shore };
        }

        [Test]
        public void ResolutionsFitTerrain()
        {
            Assert.AreEqual(257, GroundLayout.HeightResolution(128));
            Assert.AreEqual(257, GroundLayout.HeightResolution(84));
            Assert.AreEqual(33, GroundLayout.HeightResolution(6));
            Assert.AreEqual(256, GroundLayout.AlphaResolution(128));
            Assert.AreEqual(16, GroundLayout.AlphaResolution(6));
        }

        [Test]
        public void TileLayers()
        {
            Assert.AreEqual(GroundLayout.Grass, GroundLayout.LayerOf(TileType.Grass));
            Assert.AreEqual(GroundLayout.Sand, GroundLayout.LayerOf(TileType.Sea));
            Assert.AreEqual(GroundLayout.Rock, GroundLayout.LayerOf(TileType.Rock));
            Assert.AreEqual(GroundLayout.Bog, GroundLayout.LayerOf(TileType.Pond));
        }

        /// <summary>Souš je rovně (1), u kraje mapy hluboké moře (0), mělčina mezi tím.</summary>
        [Test]
        public void LandIsFlatAndSeaIsDeep()
        {
            var world = Islet();
            var resolution = GroundLayout.HeightResolution(6);
            var heights = GroundLayout.Heights(world, resolution);

            // Střed mapy je souš. Řádky jdou od jihu: řádek 0 je spodní okraj mapy.
            var middle = (resolution - 1) / 2;
            Assert.AreEqual(1f, heights[middle, middle]);
            Assert.AreEqual(0f, heights[0, 0]);

            // Bod v mělčině kolem ostrůvku je pod vodou, ale mělčeji než volné moře.
            var column = (int)(1.5 / 6 * (resolution - 1));
            var shallow = heights[middle, column];
            Assert.Greater(shallow, 0f);
            Assert.Less(shallow, 1f);
        }

        [Test]
        public void AlphamapsSumToOne()
        {
            var world = Islet();
            var resolution = GroundLayout.AlphaResolution(6);
            var maps = GroundLayout.Alphamaps(world, resolution);

            for (var row = 0; row < resolution; row++)
            {
                for (var column = 0; column < resolution; column++)
                {
                    var sum = 0f;
                    for (var layer = 0; layer < GroundLayout.LayerCount; layer++)
                    {
                        sum += maps[row, column, layer];
                    }

                    Assert.AreEqual(1f, sum, 1e-5f);
                }
            }

            // Uprostřed ostrůvku je čistá tráva, v rohu čistý písek (dno moře).
            Assert.AreEqual(1f, maps[resolution / 2, resolution / 2, GroundLayout.Grass], 1e-5f);
            Assert.AreEqual(1f, maps[0, 0, GroundLayout.Sand], 1e-5f);
        }

        [Test]
        public void GeneratedTexturesAreUsable()
        {
            var texture = GroundView.GeneratedTexture(Color.green, 1);
            Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapMode);

            var normals = WaterView.GeneratedNormalMap();
            var pixel = normals.GetPixel(10, 10);
            var normal = new Vector3(pixel.r * 2 - 1, pixel.g * 2 - 1, pixel.b * 2 - 1);
            Assert.AreEqual(1f, normal.magnitude, 0.03f);
            Assert.Greater(normal.z, 0.5f);
        }
    }
}

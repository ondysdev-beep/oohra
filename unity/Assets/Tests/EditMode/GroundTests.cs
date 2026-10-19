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
            Assert.AreEqual(513, GroundLayout.HeightResolution(128));
            Assert.AreEqual(513, GroundLayout.HeightResolution(84));
            Assert.AreEqual(33, GroundLayout.HeightResolution(6));
            Assert.AreEqual(512, GroundLayout.AlphaResolution(128));
            Assert.AreEqual(32, GroundLayout.AlphaResolution(6));
        }

        [Test]
        public void TileLayers()
        {
            Assert.AreEqual(GroundLayout.Grass, GroundLayout.LayerOf(TileType.Grass));
            Assert.AreEqual(GroundLayout.Sand, GroundLayout.LayerOf(TileType.Sea));
            Assert.AreEqual(GroundLayout.Rock, GroundLayout.LayerOf(TileType.Rock));
            Assert.AreEqual(GroundLayout.Bog, GroundLayout.LayerOf(TileType.Pond));
        }

        /// <summary>Souš bez kopců je ve výšce hladiny (nad nejhlubším dnem o Depth), u kraje mapy hluboké moře (0), mělčina mezi tím.</summary>
        [Test]
        public void LandIsFlatAndSeaIsDeep()
        {
            var world = Islet();
            var resolution = GroundLayout.HeightResolution(6);
            var heights = GroundLayout.Heights(world, resolution);

            // Střed mapy je souš. Řádky jdou od jihu: řádek 0 je spodní okraj mapy.
            var middle = (resolution - 1) / 2;
            var land = GroundLayout.Depth / (GroundLayout.Depth + GroundLayout.MaxElevation(world));
            Assert.AreEqual(land, heights[middle, middle], 1e-5f);
            Assert.AreEqual(0f, heights[0, 0]);

            // Bod v mělčině kolem ostrůvku je pod vodou, ale mělčeji než volné moře.
            var column = (int)(1.5 / 6 * (resolution - 1));
            var shallow = heights[middle, column];
            Assert.Greater(shallow, 0f);
            Assert.Less(shallow, land);
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

        /// <summary>
        /// Zakulacené pobřeží: roh ostrůvku je pod vodou, ale kde postava (poloměr 0,28 m) smí stát,
        /// zůstane souš. Rovná hrana zůstane na hraně dlaždic.
        /// </summary>
        [Test]
        public void ShoreIsRoundedButStandingPlacesStayDry()
        {
            var world = Islet();

            // Ostrůvek je x 2..4, y 2..4. Roh (2, 2) je zakulacený pod vodu.
            Assert.Less(GroundLayout.LandFraction(world, 2.05, 2.05), 0.5f);
            // Nejkrajnější místo, kde postava v rohu stojí: 28 cm od obou hran.
            Assert.GreaterOrEqual(GroundLayout.LandFraction(world, 2.28, 2.28), 0.5f);
            // Uprostřed rovné hrany je čára břehu přesně na hraně dlaždic.
            Assert.AreEqual(0.5f, GroundLayout.LandFraction(world, 2.0, 3.0), 0.08f);
            Assert.AreEqual(1f, GroundLayout.LandFraction(world, 3.0, 3.0));
            Assert.AreEqual(0f, GroundLayout.LandFraction(world, 0.5, 0.5));
        }

        /// <summary>Pěna leží na hladině kolem celého ostrůvku: u břehu plná, do vody slábne.</summary>
        [Test]
        public void FoamRingsTheIslet()
        {
            var mesh = FoamView.BuildMesh(Islet(), 0.45f);

            Assert.Greater(mesh.vertexCount, 40);
            var vertices = mesh.vertices;
            var colors = mesh.colors;
            for (var i = 0; i < vertices.Length; i++)
            {
                Assert.AreEqual(GroundLayout.WaterLevel + 0.01f, vertices[i].y, 1e-5f);
                Assert.AreEqual(i % 4 < 2 ? 1f : 0f, colors[i].a);
            }

            // Pěna obklopuje ostrůvek ze všech stran (střed ostrůvku je v Unity na x 3, z -3).
            Assert.Less(mesh.bounds.min.x, 2f);
            Assert.Greater(mesh.bounds.max.x, 4f);
            Assert.Less(mesh.bounds.min.z, -4f);
            Assert.Greater(mesh.bounds.max.z, -2f);
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

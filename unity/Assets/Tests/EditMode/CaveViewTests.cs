using System;
using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Jeskyně v Unity: stěny kolem podlahy, poznání, že je postava v jeskyni, texty a dosah vchodu.</summary>
    public class CaveViewTests
    {
        /// <summary>Malá mapa 6 × 6: dole tráva, nahoře skála jeskyně s podlahou 2 × 1 uprostřed.</summary>
        private static WorldView World()
        {
            var tiles = new TileType[36];
            for (var i = 0; i < 36; i++)
            {
                tiles[i] = i / 6 < 3 ? TileType.CaveWall : TileType.Grass;
            }

            tiles[1 * 6 + 2] = TileType.CaveFloor;
            tiles[1 * 6 + 3] = TileType.CaveFloor;
            return new WorldView { Width = 6, Height = 6, Tiles = tiles, Shore = new bool[36] };
        }

        [Test]
        public void WallsSurroundFloorWithTopsUpAndSidesInside()
        {
            var mesh = CaveView.BuildMesh(World());

            Assert.Greater(mesh.vertexCount, 0);
            Assert.AreEqual(CaveView.WallHeight, mesh.bounds.max.y, 0.001f);
            Assert.AreEqual(0f, mesh.bounds.min.y, 0.001f);
            // Vršek každého bloku míří nahoru (první čtyři vrcholy jsou vršek prvního bloku).
            Assert.Greater(mesh.normals[0].y, 0.9f);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void KnowsWhenPlaceIsInCave()
        {
            var world = World();

            Assert.IsTrue(CaveView.IsCave(world, 2.5, 1.5));
            Assert.IsFalse(CaveView.IsCave(world, 2.5, 5.5));
        }

        [Test]
        public void CaveTextsAndReach()
        {
            foreach (CaveResult result in Enum.GetValues(typeof(CaveResult)))
            {
                Assert.AreNotEqual(result.ToString(), Texts.For(result));
            }

            Assert.AreNotEqual(BuildResult.NotInCave.ToString(), Texts.For(BuildResult.NotInCave));
            Assert.Less(InteractionTarget.ForCave(1, 10, 10).Reach, 2.5, "Klient se zastaví v dosahu serveru.");
        }
    }
}

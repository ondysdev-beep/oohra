using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Oohra.Client.Tests
{
    /// <summary>Okolí pro ozdobu (ScatterView) a profil úprav obrazu (GraphicsQuality).</summary>
    public class ScatterViewTests
    {
        /// <summary>Svět 40 × 40: les, louka, bažina, pláž, vrchovina a uprostřed velký skalní hřbet.</summary>
        private static WorldView World()
        {
            var tiles = new TileType[40 * 40];
            for (var i = 0; i < tiles.Length; i++)
            {
                var (x, y) = (i % 40, i / 40);
                tiles[i] = x < 8 ? TileType.Forest : x < 16 ? TileType.Grass : x < 24 ? TileType.Bog : x < 28 ? TileType.Sand : TileType.Highland;
                if (x >= 30 && x < 38 && y >= 10 && y < 30) tiles[i] = TileType.Rock;
            }

            return new WorldView { Width = 40, Height = 40, Tiles = tiles, Shore = new bool[tiles.Length] };
        }

        [Test]
        public void ScatterIsSameEveryTimeAndFitsTheGround()
        {
            var world = World();
            var first = ScatterView.Build(world, null);
            var second = ScatterView.Build(world, null);

            Assert.Greater(first.CountOf("Kapradí"), 0, "V lese má být kapradí.");
            Assert.Greater(first.CountOf("Rákos"), 0, "V bažině má být rákos.");
            Assert.Greater(first.CountOf("Květina"), 0, "Na louce mají být květiny.");
            Assert.Greater(first.CountOf("Obří skála"), 0, "Na skalním hřbetu mají být obří skály.");
            Assert.AreEqual(first.Total, second.Total);
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
        }

        /// <summary>Profil úprav obrazu je soubor v projektu (sestavení by jinak efekty vyhodilo) a má záři i tónování.</summary>
        [Test]
        public void PostProfileIsAnAsset()
        {
            var profile = Resources.Load<VolumeProfile>(GraphicsQuality.PostResource);
            Assert.IsNotNull(profile, "Chybí Assets/Resources/OohraPost.asset (menu Oohra → Sestavit hru ho vytvoří).");
            Assert.IsTrue(profile.Has<Bloom>());
            Assert.IsTrue(profile.Has<Tonemapping>());
            Assert.IsTrue(profile.Has<Vignette>());
        }
    }
}

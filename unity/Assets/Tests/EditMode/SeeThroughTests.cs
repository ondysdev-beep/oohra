using NUnit.Framework;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Kdy věc zakrývá postavu před kamerou (a má zprůhlednět).</summary>
    public class SeeThroughTests
    {
        private static readonly Vector2 Player = new Vector2(10f, 10f);

        // Kamera na jihu od postavy (y mapy roste na jih).
        private static readonly Vector2 ToCamera = new Vector2(0f, 1f);

        private const float LowCamera = 0.36f;
        private const float NormalCamera = 1.43f;
        private const float TopDown = 11f;

        /// <summary>Chyba z hraní: při nízko skloněné kameře zeď těsně před postavou nezprůhledněla.</summary>
        [Test]
        public void WallJustInFrontCoversAtLowCamera()
        {
            Assert.IsTrue(Wall(1f, 0f, LowCamera));
            Assert.IsTrue(Wall(1f, 0f, NormalCamera));
        }

        [Test]
        public void WallBehindOrBesideDoesNotCover()
        {
            Assert.IsFalse(Wall(-1f, 0f, LowCamera));
            Assert.IsFalse(Wall(0f, 1f, NormalCamera));
            Assert.IsFalse(Wall(1f, 2.5f, LowCamera));
        }

        [Test]
        public void FarWallCoversOnlyLowCamera()
        {
            Assert.IsTrue(Wall(4f, 0f, LowCamera));
            Assert.IsFalse(Wall(4f, 0f, NormalCamera));
            Assert.IsFalse(Wall(1.5f, 0f, TopDown));
        }

        [Test]
        public void CrownOverheadCoversTopDown()
        {
            Assert.IsTrue(Crown(0f, TopDown));
            Assert.IsTrue(Crown(2f, NormalCamera));
            Assert.IsTrue(Crown(8f, LowCamera));
            Assert.IsFalse(Crown(8f, NormalCamera));
        }

        /// <summary>Zeď (1 m dlouhá, 2 m vysoká) along metrů směrem ke kameře a aside metrů do strany.</summary>
        private static bool Wall(float along, float aside, float rise)
        {
            var position = Player + ToCamera * along + new Vector2(aside, 0f);
            return SeeThrough.Covers(position, 0.5f, 0f, 2f, Player, ToCamera, rise);
        }

        /// <summary>Koruna stromu (průměr 2,6 m, od 2,2 do 4,8 m) along metrů směrem ke kameře.</summary>
        private static bool Crown(float along, float rise)
        {
            return SeeThrough.Covers(Player + ToCamera * along, 1.3f, 2.2f, 4.8f, Player, ToCamera, rise);
        }
    }
}

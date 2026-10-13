using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Modulární stavba: přichycení ke konci zdi, natočení Q a E a dorovnání mezery (zapadnutí).</summary>
    public class ModularSnapTests
    {
        // Zeď od (9,5; 10) do (10,5; 10).
        private static readonly StructureView Wall = new StructureView(1, BuildKind.StickWall, 10f, 10f, 0f, 1f);

        [Test]
        public void ContinuesStraightFromWallEnd()
        {
            SnapResult snap;
            Assert.IsTrue(ModularSnap.Find(new[] { Wall }, new Vector2(10.9f, 10.05f), 1f, 0f, true, out snap));

            AssertNear(new Vector2(11f, 10f), snap.Center);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, snap.AngleDegrees), 0.01f);
            Assert.AreEqual(1f, snap.Length, 0.001f);
        }

        [Test]
        public void TurnsSidewaysWhereCursorPoints()
        {
            SnapResult snap;
            ModularSnap.Find(new[] { Wall }, new Vector2(10.5f, 10.6f), 1f, 0f, true, out snap);

            AssertNear(new Vector2(10.5f, 10.5f), snap.Center);
            Assert.AreEqual(90f, Mathf.DeltaAngle(0f, snap.AngleDegrees), 0.01f);
        }

        /// <summary>Q a E natočí zeď kolem konce, ke kterému je přichycená (konec zůstane na místě).</summary>
        [Test]
        public void RotatesAroundWallEnd()
        {
            SnapResult snap;
            ModularSnap.Find(new[] { Wall }, new Vector2(11f, 10.2f), 1f, 15f, true, out snap);

            Assert.AreEqual(15f, Mathf.DeltaAngle(0f, snap.AngleDegrees), 0.01f);
            var direction = new Vector2(Mathf.Cos(snap.AngleDegrees * Mathf.Deg2Rad), Mathf.Sin(snap.AngleDegrees * Mathf.Deg2Rad));
            AssertNear(new Vector2(10.5f, 10f), snap.Center - direction * (snap.Length / 2));
        }

        /// <summary>Mezera mezi dvěma konci (0,8 m): zeď se natáhne přesně mezi ně a zapadne.</summary>
        [Test]
        public void FitsIntoGapBetweenWallEnds()
        {
            // Druhá zeď svisle, její horní konec je v (11,2; 10,4).
            var other = new StructureView(2, BuildKind.StickWall, 11.2f, 10.9f, Mathf.PI / 2, 1f);
            var gap = new Vector2(11.2f, 10.4f) - new Vector2(10.5f, 10f);

            SnapResult snap;
            ModularSnap.Find(new[] { Wall, other }, new Vector2(10.85f, 10.2f), 1f, 0f, true, out snap);

            Assert.IsTrue(snap.Fits);
            AssertNear(new Vector2(10.85f, 10.2f), snap.Center);
            Assert.AreEqual(gap.magnitude, snap.Length, 0.001f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(Mathf.Atan2(gap.y, gap.x) * Mathf.Rad2Deg, snap.AngleDegrees) % 180f, 0.01f);
        }

        /// <summary>Zeď s otvorem má pevnou délku, ta se nedorovnává.</summary>
        [Test]
        public void DoorFrameDoesNotStretch()
        {
            var other = new StructureView(2, BuildKind.StickWall, 11.2f, 10.9f, Mathf.PI / 2, 1f);
            SnapResult snap;
            ModularSnap.Find(new[] { Wall, other }, new Vector2(10.85f, 10.2f), 1f, 0f, false, out snap);

            Assert.IsFalse(snap.Fits);
            Assert.AreEqual(1f, snap.Length, 0.001f);
        }

        [Test]
        public void NoSnapFarFromWalls()
        {
            SnapResult snap;
            Assert.IsFalse(ModularSnap.Find(new[] { Wall }, new Vector2(14f, 14f), 1f, 0f, true, out snap));
        }

        private static void AssertNear(Vector2 expected, Vector2 actual)
        {
            Assert.Less(Vector2.Distance(expected, actual), 0.01f, "Čekal jsem " + expected + ", je " + actual);
        }
    }
}

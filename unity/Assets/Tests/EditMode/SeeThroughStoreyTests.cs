using NUnit.Framework;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Zprůhlednění u střech a pater: výšky se berou od podlahy, na které postava stojí.</summary>
    public class SeeThroughStoreyTests
    {
        private static readonly Vector2 Player = new Vector2(10f, 10f);
        private static readonly Vector2 ToCamera = new Vector2(0f, 1f);
        private const float Rise = 1.2f;

        /// <summary>Střecha přímo nad hlavou postavu zakrývá (zprůhlední se).</summary>
        [Test]
        public void RoofRightAboveCovers()
        {
            Assert.IsTrue(SeeThrough.Covers(Player, 1.4f, StructureViews.WallHeight, StructureViews.LevelHeight, Player, ToCamera, Rise));
        }

        /// <summary>V patře: zeď přízemí pod postavou (pod podlahou patra) nezakrývá, střecha patra nad ní ano.</summary>
        [Test]
        public void UpstairsWallsBelowDoNotCoverButRoofAboveDoes()
        {
            var floor = StructureViews.LevelHeight;
            var wallBelow = new Vector2(10f, 11f);
            Assert.IsFalse(SeeThrough.Covers(wallBelow, 0.5f, 0f - floor, StructureViews.WallHeight - floor, Player, ToCamera, Rise));
            // Střecha patra leží o podlaží výš: od podlahy patra je ve stejné výšce jako střecha přízemí od země.
            Assert.IsTrue(SeeThrough.Covers(Player, 1.4f, StructureViews.WallHeight, StructureViews.LevelHeight, Player, ToCamera, Rise));
        }
    }
}

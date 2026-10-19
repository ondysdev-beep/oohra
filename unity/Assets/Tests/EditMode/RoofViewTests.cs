using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Střechy, sloupky a podlahy v Unity: tvary, nabídka stavby a přichycení vedle sousední střechy.</summary>
    public class RoofViewTests
    {
        [TearDown]
        public void ResetCatalog()
        {
            VisualCatalog.Current = null;
        }

        [Test]
        public void RoofSitsOnWallsAndPostIsAsHigh()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var roof = StructureViews.CreateModel(BuildKind.Roof, null, StructureViews.PanelSize);
            var post = StructureViews.CreateModel(BuildKind.Post, null);

            var bounds = VisualCatalog.BoundsOf(roof);
            Assert.AreEqual(StructureViews.WallHeight, bounds.min.y, 0.01f);
            Assert.AreEqual(StructureViews.PanelSize, bounds.size.x, 0.01f);
            Assert.AreEqual(StructureViews.WallHeight, VisualCatalog.BoundsOf(post).max.y, 0.01f);
            Assert.IsTrue(System.Array.Exists(BuildMode.Catalog, o => o.Kind == BuildKind.Roof));
            Assert.IsTrue(System.Array.Exists(BuildMode.Catalog, o => o.Kind == BuildKind.Floor));
            Object.DestroyImmediate(roof);
            Object.DestroyImmediate(post);
        }

        /// <summary>Nová střecha se přichytí těsně vedle té, která už stojí (na straně u kurzoru).</summary>
        [Test]
        public void PanelSnapsNextToNeighbour()
        {
            var structures = new[] { new StructureView(1, BuildKind.Roof, 10f, 10f, 0f, 2f) };
            SnapResult snap;

            Assert.IsTrue(ModularSnap.Panel(structures, new Vector2(12.4f, 10.3f), BuildKind.Roof, 2f, out snap));
            Assert.AreEqual(12f, snap.Center.x, 0.001f);
            Assert.AreEqual(10f, snap.Center.y, 0.001f);
            Assert.IsFalse(ModularSnap.Panel(structures, new Vector2(30f, 30f), BuildKind.Roof, 2f, out snap));
        }

        /// <summary>První střecha se natočí podle nejbližší zdi.</summary>
        [Test]
        public void FirstRoofTurnsLikeWall()
        {
            var structures = new[] { new StructureView(1, BuildKind.StickWall, 10f, 10f, 0.5f, 1f) };
            SnapResult snap;

            Assert.IsTrue(ModularSnap.Panel(structures, new Vector2(10.5f, 11f), BuildKind.Roof, 2f, out snap));
            Assert.AreEqual(0.5f * Mathf.Rad2Deg, snap.AngleDegrees, 0.01f);
        }
    }
}

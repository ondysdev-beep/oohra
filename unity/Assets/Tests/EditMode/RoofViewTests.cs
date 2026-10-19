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
    }
}

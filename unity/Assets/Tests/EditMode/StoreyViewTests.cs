using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Patro v Unity: výška staveb a podlahy patra, žebřík, nabídka stavby.</summary>
    public class StoreyViewTests
    {
        [TearDown]
        public void ResetCatalog()
        {
            VisualCatalog.Current = null;
        }

        [Test]
        public void UpperFloorTopIsAtLevelHeight()
        {
            Assert.AreEqual(StructureViews.LevelHeight, StructureViews.BaseHeight(BuildKind.Floor, 1) + StructureViews.FloorThickness, 0.001f);
            Assert.AreEqual(StructureViews.LevelHeight, StructureViews.BaseHeight(BuildKind.StickWall, 1), 0.001f);
            Assert.AreEqual(0f, StructureViews.BaseHeight(BuildKind.Floor, 0), 0.001f);
        }

        [Test]
        public void LadderReachesUpperFloorAndIsInMenu()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var ladder = StructureViews.CreateModel(BuildKind.Ladder, null);

            Assert.GreaterOrEqual(VisualCatalog.BoundsOf(ladder).max.y, StructureViews.LevelHeight - 0.1f);
            Assert.IsTrue(System.Array.Exists(BuildMode.Catalog, o => o.Kind == BuildKind.Ladder));
            Assert.IsTrue(System.Array.Exists(BuildMode.Catalog, o => o.Kind == BuildKind.Floor && o.Level == 1));
            Assert.AreEqual("Žebřík", StructureViews.NameOf(BuildKind.Ladder));
            Object.DestroyImmediate(ladder);
        }
    }
}

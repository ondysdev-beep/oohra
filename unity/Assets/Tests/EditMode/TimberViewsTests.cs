using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Dřevo na zemi a pařez bez modelů z katalogu (tvary z kódu).</summary>
    public class TimberViewsTests
    {
        [TearDown]
        public void RestoreCatalog()
        {
            VisualCatalog.Current = null;
        }

        [Test]
        public void TrunkLiesAlongItsAngleWithItsLength()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var parent = new GameObject("Dřevo");
            try
            {
                // Úhel 0 = na východ (osa X v Unity), kmen 5 m.
                var trunk = TimberViews.Create(parent.transform, new TimberView(3, TimberKind.Trunk, TreeKind.Birch, 10f, 20f, 0f, 5));
                var bounds = VisualCatalog.BoundsOf(trunk);

                Assert.AreEqual(5f, bounds.size.x, 0.05f);
                Assert.AreEqual(TimberViews.Thickness, bounds.size.z, 0.05f);
                Assert.AreEqual(10f, bounds.center.x, 0.01f);
                Assert.AreEqual(-20f, bounds.center.z, 0.01f);
                Assert.AreEqual(TargetKind.Timber, trunk.GetComponent<Pickable>().Kind);

                // Úhel 90° = na jih na mapě (y dolů), v Unity -z.
                var piece = TimberViews.Create(parent.transform, new TimberView(4, TimberKind.Piece, TreeKind.Oak, 0f, 0f, Mathf.PI / 2, 1));
                var pieceBounds = VisualCatalog.BoundsOf(piece);
                Assert.AreEqual(1f, pieceBounds.size.z, 0.05f);
                Assert.Less(pieceBounds.size.x, 0.6f);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>Kmen z katalogu (6 m dlouhý, podél osy Z jako z Meshy) se natáhne na délku kmene a tloušťku 0,44 m.</summary>
        [Test]
        public void CatalogLogOfAnyLengthFitsTrunk()
        {
            var catalog = ScriptableObject.CreateInstance<VisualCatalog>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.transform.localScale = new Vector3(0.4f, 0.4f, 6f);
            catalog.Log = prefab;
            VisualCatalog.Current = catalog;
            var parent = new GameObject("Dřevo");
            try
            {
                var trunk = TimberViews.Create(parent.transform, new TimberView(1, TimberKind.Trunk, TreeKind.Oak, 0f, 0f, 0f, 4));
                var bounds = VisualCatalog.BoundsOf(trunk.transform.Find("Model").gameObject);

                Assert.AreEqual(4f, bounds.size.x, 0.05f, "Délka kmene podél směru (osa X).");
                Assert.AreEqual(TimberViews.Thickness, bounds.size.y, 0.02f);
                Assert.AreEqual(TimberViews.Thickness, bounds.size.z, 0.02f);
                Assert.AreEqual(0f, bounds.min.y, 0.02f, "Kmen leží na zemi.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void StumpIsLowCylinder()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var parent = new GameObject("Stromy");
            try
            {
                var stump = TreeViews.CreateStump(parent.transform, TreeKind.Oak, 3.5, 4.5);
                var bounds = VisualCatalog.BoundsOf(stump);
                Assert.AreEqual(0.6f, bounds.max.y, 0.02f);
                Assert.AreEqual(3.5f, bounds.center.x, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}

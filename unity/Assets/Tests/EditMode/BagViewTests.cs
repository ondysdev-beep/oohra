using NUnit.Framework;
using Oohra.Core.Items;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Batoh na zádech postavy bez modelu z katalogu (kvádr z kódu).</summary>
    public class BagViewTests
    {
        [TearDown]
        public void RestoreCatalog()
        {
            VisualCatalog.Current = null;
        }

        [Test]
        public void BagSitsOnTheBackAndLargeIsBigger()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var figure = new GameObject("Postava");
            try
            {
                var small = PlayerViews.CreateBag(figure.transform, ItemKind.SmallBag);
                var large = PlayerViews.CreateBag(figure.transform, ItemKind.LargeBag);

                Assert.Less(small.transform.localPosition.z, 0f, "Batoh je na zádech (-Z).");
                Assert.Greater(VisualCatalog.BoundsOf(large).size.y, VisualCatalog.BoundsOf(small).size.y);
                Assert.AreEqual(0.6f, VisualCatalog.BoundsOf(large).size.y, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(figure);
            }
        }
    }
}

using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Stromy podle druhu a výšky ze serveru: tvar z kódu, když katalog nemá model.</summary>
    public class TreeViewsTests
    {
        [TearDown]
        public void RestoreCatalog()
        {
            VisualCatalog.Current = null;
        }

        [Test]
        public void EachKindHasItsShapeAndHeight()
        {
            VisualCatalog.Current = ScriptableObject.CreateInstance<VisualCatalog>();
            var world = new WorldView
            {
                Width = 10,
                Height = 10,
                Trees = new[]
                {
                    new TreeView(11, 0.2f, TreeKind.Oak, 7f),
                    new TreeView(13, 0.2f, TreeKind.Conifer, 8f),
                    new TreeView(15, 0.2f, TreeKind.Birch, 5f),
                    new TreeView(31, 0.2f, TreeKind.Willow, 4f),
                    new TreeView(33, 0.2f, TreeKind.Dead, 4f),
                },
            };
            var root = new GameObject("Ostrov");
            try
            {
                var views = root.AddComponent<TreeViews>();
                views.Init(world, root.AddComponent<SeeThrough>());

                Assert.AreEqual(5, views.Count);
                StringAssert.Contains("Dub", views.TreeAt(11).name);
                Assert.IsNull(views.TreeAt(33).transform.Find("Koruna"), "Suchý strom nemá listí.");
                Assert.IsNotNull(views.TreeAt(33).transform.Find("Větev"));
                Assert.AreEqual(8f, Top(views.TreeAt(13)), 0.1f, "Jehličnan sahá do své výšky.");
                Assert.AreEqual(7f, Top(views.TreeAt(11)), 0.4f);
                var willow = views.TreeAt(31).transform.Find("Koruna").localScale;
                var conifer = views.TreeAt(13).transform.Find("Koruna").localScale;
                Assert.Greater(willow.x / 4f, conifer.x / 8f, "Vrba je širší než jehličnan.");
                Assert.IsNotNull(views.TreeAt(11).GetComponentInChildren<Pickable>());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static float Top(GameObject tree)
        {
            return VisualCatalog.BoundsOf(tree).max.y;
        }
    }
}

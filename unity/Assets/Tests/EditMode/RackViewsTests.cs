using NUnit.Framework;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Maso v kouři a na sušáku: barva podle stavu, kusy pod trojnožkou dokola a na sušáku v řadě.</summary>
    public class RackViewsTests
    {
        [Test]
        public void MeatDarkensAsItSmokes()
        {
            var raw = RackViews.ColorOf(RackHolder.Fire, new RackPieceView(ItemKind.Meat, Cooking.Raw, 0));
            var half = RackViews.ColorOf(RackHolder.Fire, new RackPieceView(ItemKind.Meat, Cooking.Raw, 128));
            var smoked = RackViews.ColorOf(RackHolder.Fire, new RackPieceView(ItemKind.Meat, Cooking.Smoked, 255));

            Assert.Greater(raw.r, half.r);
            Assert.Greater(half.r, smoked.r);
            Assert.AreNotEqual(smoked, RackViews.ColorOf(RackHolder.Structure, new RackPieceView(ItemKind.Meat, Cooking.Dried, 255)));
        }

        [Test]
        public void PiecesHangUnderTripodAndInRowOnRack()
        {
            var a = RackViews.PieceSpot(RackHolder.Fire, 0, 4);
            var b = RackViews.PieceSpot(RackHolder.Fire, 2, 4);
            Assert.Less(a.y, RackViews.TripodHeight);
            Assert.Greater(Vector3.Distance(a, b), 0.1f, "Kusy nad ohněm nevisí přes sebe.");

            var first = RackViews.PieceSpot(RackHolder.Structure, 0, 3);
            var middle = RackViews.PieceSpot(RackHolder.Structure, 1, 3);
            Assert.AreEqual(0f, middle.x, 0.001f, "Řada je na sušáku uprostřed.");
            Assert.Less(first.x, middle.x);
            Assert.AreEqual(RackViews.RackHeight, first.y, 0.001f);
        }
    }
}

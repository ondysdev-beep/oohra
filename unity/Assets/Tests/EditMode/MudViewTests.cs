using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Jarní záplava a stopy v bahně: sítě se postaví ze seznamu od serveru.</summary>
    public class MudViewTests
    {
        [Test]
        public void FloodCoversEachTileAndFacesUp()
        {
            var mesh = FloodView.BuildMesh(new[] { 0, 5, 130 }, 128, new Color(0.3f, 0.3f, 0.2f, 0.5f));

            Assert.AreEqual(12, mesh.vertexCount);
            Assert.Greater(mesh.normals[0].y, 0.9f, "Hladina musí mířit nahoru.");
            // Dlaždice 5 končí v x = 6, dlaždice 130 (řádek 1) v Unity z = -2 (osa y mapy jde do -z), s přesahem 8 cm.
            Assert.AreEqual(6.08f, mesh.bounds.max.x, 0.01f);
            Assert.AreEqual(-2.08f, mesh.bounds.min.z, 0.01f);
            Assert.AreEqual(FloodView.Height, mesh.bounds.center.y, 0.001f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TracksFadeAndPointWhereTheWalkerWent()
        {
            var tracks = new[]
            {
                new TrackView(10.5f, 20.5f, 0, TrackMaker.Human, 255),
                new TrackView(11.5f, 20.5f, 64, TrackMaker.Wolf, 51),
            };

            var mesh = TrackViews.BuildMesh(tracks);

            // Člověk jeden otisk, vlk dva (4 vrcholy na otisk).
            Assert.AreEqual(12, mesh.vertexCount);
            Assert.Greater(mesh.colors[0].a, mesh.colors[4].a * 4, "Starší stopa je bledší.");
            // Směr 0 = podél osy x: otisk člověka je delší ve směru x než do strany.
            var first = new Bounds(mesh.vertices[0], Vector3.zero);
            for (var i = 1; i < 4; i++) first.Encapsulate(mesh.vertices[i]);
            Assert.Greater(first.size.x, first.size.z);
            // Normály míří nahoru (nulová normála dala neplatné světlo a záře z něj bílé skvrny).
            foreach (var normal in mesh.normals) Assert.AreEqual(1f, normal.y, 1e-5f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void EveryMakerHasPrintSize()
        {
            foreach (TrackMaker maker in System.Enum.GetValues(typeof(TrackMaker)))
            {
                Assert.Greater(TrackViews.PrintSize(maker).x, 0f, maker.ToString());
            }
        }
    }
}

using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Klikání na 3D objekty, rozložení hromady kamenů a hladina vody.</summary>
    public class PickAndRockTests
    {
        /// <summary>Krabice pro kliknutí pokryje celý model (i korunu stromu vysoko nad zemí) a ví, co je to za cíl.</summary>
        [Test]
        public void PickableCoversWholeModel()
        {
            var tree = new GameObject("Strom");
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(tree.transform, false);
            crown.transform.localPosition = new Vector3(0f, 4f, 0f);
            crown.transform.localScale = Vector3.one * 2f;
            Object.DestroyImmediate(crown.GetComponent<Collider>());

            var pickable = Pickable.Attach(tree, InteractionTarget.ForTile(TargetKind.Tree, 7, 9));
            var box = tree.GetComponent<BoxCollider>();

            Assert.AreEqual(4f, box.center.y, 1e-4f);
            Assert.AreEqual(2f, box.size.y, 1e-4f);
            var target = pickable.Target();
            Assert.AreEqual(TargetKind.Tree, target.Kind);
            Assert.AreEqual(7, target.TileX);
            Assert.AreEqual(9, target.TileY);
            Object.DestroyImmediate(tree);
        }

        [Test]
        public void TargetsKeepTheirKind()
        {
            var go = new GameObject("Rostlina");
            Assert.AreEqual(TargetKind.Plant, Pickable.Attach(go, InteractionTarget.ForPlant(3, 1.5, 2.5)).Target().Kind);
            Assert.AreEqual(3, go.GetComponent<Pickable>().Target().Id);
            Assert.AreEqual(TargetKind.Animal, Pickable.Attach(go, InteractionTarget.ForAnimal(5, 1, 1)).Target().Kind);
            Assert.AreEqual(TargetKind.Rock, Pickable.Attach(go, InteractionTarget.ForTile(TargetKind.Rock, 2, 3)).Target().Kind);
            Object.DestroyImmediate(go);
        }

        /// <summary>Hromada: dole kruh nejvýš čtyř velkých kamenů na zemi, nahoře menší (berou se jako první).</summary>
        [Test]
        public void PileLayoutStacksStones()
        {
            var layout = RockPileViews.Layout(6, 42, 0.4f, 0.6f);

            Assert.AreEqual(6, layout.Count);
            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(0f, layout[i].Key.y);
                Assert.AreEqual(0.2f, new Vector2(layout[i].Key.x, layout[i].Key.z).magnitude, 1e-4f);
                Assert.That(layout[i].Value, Is.InRange(0.4f, 0.6f));
            }

            Assert.Greater(layout[4].Key.y, 0f);
            Assert.Greater(layout[5].Key.y, layout[4].Key.y);
            Assert.AreEqual(Vector3.zero, RockPileViews.Layout(1, 1, 0.4f, 0.6f)[0].Key);
        }

        /// <summary>Hladina míří nahoru a souřadnice textury jsou v metrech (50 m = 1), ne přes celé moře.</summary>
        [Test]
        public void WaterMeshHasWorldUvs()
        {
            var world = new WorldView { Width = 20, Height = 20, Tiles = new TileType[400], Shore = new bool[400] };
            var mesh = WaterView.BuildMesh(world, 50f);

            Assert.Less(mesh.vertexCount, 65000);
            foreach (var normal in mesh.normals)
            {
                Assert.Greater(normal.y, 0.99f);
            }

            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            for (var i = 0; i < vertices.Length; i += 97)
            {
                Assert.AreEqual(vertices[i].x / 50f, uvs[i].x, 1e-4f);
                Assert.AreEqual(vertices[i].z / 50f, uvs[i].y, 1e-4f);
            }

            // Hladina sahá daleko za ostrov (moře k obzoru).
            Assert.Greater(mesh.bounds.size.x, 20f * 5f);
        }

        /// <summary>Pod souší hladina není (vlny vlastního shaderu by jinak zaplavily ostrov), u okraje mapy ano.</summary>
        [Test]
        public void NoWaterUnderLand()
        {
            var tiles = new TileType[400];
            for (var i = 0; i < tiles.Length; i++) tiles[i] = TileType.Grass;
            var world = new WorldView { Width = 20, Height = 20, Tiles = tiles, Shore = new bool[400] };
            var mesh = WaterView.BuildMesh(world, 50f);

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var nearEdge = false;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var c = (vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f;
                var x = c.x;
                var y = -c.z;
                Assert.IsFalse(x > 4f && x < 16f && y > 4f && y < 16f, "Hladina pod souší v (" + x + ", " + y + ").");
                nearEdge |= x > -2f && x < 22f && y > -2f && y < 22f;
            }

            Assert.IsTrue(nearEdge, "U okraje ostrova chybí hladina.");
        }

        [Test]
        public void ReflectionCameraIsRecognized()
        {
            var notCamera = new GameObject("Kus vody");
            Assert.IsFalse(WaterView.HasReflectionPlane(notCamera));
            Object.DestroyImmediate(notCamera);
        }
    }
}

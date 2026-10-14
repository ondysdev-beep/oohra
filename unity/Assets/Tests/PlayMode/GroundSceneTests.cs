using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Zem jako Terrain s texturami dlaždic, voda s vlnkami a kameny na skalách.</summary>
    public class GroundSceneTests
    {
        private const int TestPort = 9186;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort);
            SceneManager.LoadScene("Ostrov");
            yield return new WaitForSecondsRealtime(2f);
        }

        [UnityTearDown]
        public IEnumerator StopServer()
        {
            PlayTestTools.StopServer(_server);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroundAndWater()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var world = bootstrap.Session.Connection.World;

            var terrain = Object.FindAnyObjectByType<Terrain>();
            Assert.IsNotNull(terrain, "Zem není Terrain.");
            Assert.AreEqual(GroundLayout.LayerCount, terrain.terrainData.terrainLayers.Length);
            Assert.AreEqual(world.Width, terrain.terrainData.size.x, 1e-3f);
            Assert.IsNull(terrain.GetComponent<TerrainCollider>());
            Assert.IsNotNull(Object.FindAnyObjectByType<WaterView>());
            Assert.Greater(GameObject.Find("Kameny").transform.childCount, 0);
            Assert.Greater(Object.FindAnyObjectByType<FoamView>().GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);

            // Souš je ve výšce 0, dno vody pod hladinou.
            Vector2 land;
            Vector2 water;
            FindShore(world, out land, out water);
            var bottom = terrain.transform.position.y;
            Assert.AreEqual(0f, terrain.SampleHeight(WorldCoords.ToUnity(land.x, land.y)) + bottom, 0.01f);
            Assert.Less(terrain.SampleHeight(WorldCoords.ToUnity(water.x, water.y)) + bottom, GroundLayout.WaterLevel);

            yield return WaitFor(() => Object.FindAnyObjectByType<CameraRig>() != null, 5f, "Kamera nezačala sledovat postavu.");
            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            Camera.main.transform.position = WorldCoords.ToUnity(water.x + 2.5f, water.y + 2.5f, 2.2f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(land.x, land.y, 0f));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return SaveScreenshot("zem.png");
        }

        /// <summary>Střed dlaždice pláže a místo ve vodě dvě dlaždice od ní na východ (voda kolem dokola).</summary>
        private static void FindShore(WorldView world, out Vector2 land, out Vector2 water)
        {
            for (var y = 2; y < world.Height - 2; y++)
            {
                for (var x = 2; x < world.Width - 3; x++)
                {
                    var dry = world.TileAt(x, y) == TileType.Sand;
                    var wet = world.TileAt(x + 1, y);
                    if (dry && (wet == TileType.Pond || wet == TileType.Sea) && world.TileAt(x + 2, y) == wet
                        && world.TileAt(x + 2, y - 1) == wet && world.TileAt(x + 2, y + 1) == wet && world.TileAt(x + 3, y) == wet)
                    {
                        land = new Vector2(x + 0.5f, y + 0.5f);
                        water = new Vector2(x + 2.5f, y + 0.5f);
                        return;
                    }
                }
            }

            throw new System.InvalidOperationException("Na ostrově není břeh.");
        }
    }
}

using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Svět 384 × 384 (návrh 0.8): Unity ho načte v rozumném čase, zem má nové vrstvy (kamenitá půda) a hra běží.
    /// Časy vypíše do logu (načtení, snímky za sekundu). Snímek uloží do unity/Logs/svet.png.
    /// </summary>
    public class BigWorldSceneTests
    {
        private const int TestPort = 9187;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, worldSize: 384);
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
        public IEnumerator BigWorldLoadsAndRuns()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            var load = Stopwatch.StartNew();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 60f, "Velký svět se nenačetl.");
            load.Stop();

            var world = bootstrap.Session.Connection.World;
            Assert.AreEqual(384, world.Width);
            var terrain = Object.FindAnyObjectByType<Terrain>();
            Assert.AreEqual(GroundLayout.LayerCount, terrain.terrainData.terrainLayers.Length);

            var frames = 0;
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < 3)
            {
                frames++;
                yield return null;
            }

            var grass = Object.FindAnyObjectByType<GrassView>();
            UnityEngine.Debug.Log("Oohra: svět 384 × 384 načten za " + load.Elapsed.TotalSeconds.ToString("0.0") + " s, "
                + (frames / watch.Elapsed.TotalSeconds).ToString("0") + " snímků/s, stromů " + world.Trees.Length + ", rostlin "
                + world.Plants.Length + ", trsů trávy " + (grass != null ? grass.Count + " v " + grass.ChunkCount + " dávkách" : "žádné"));
            Assert.Less(load.Elapsed.TotalSeconds, 40, "Načtení velkého světa trvá moc dlouho.");
            yield return SaveScreenshot("svet.png");
        }
    }
}

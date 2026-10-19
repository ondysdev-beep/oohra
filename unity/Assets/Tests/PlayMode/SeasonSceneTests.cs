using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Jarní záplava ve scéně: server začne v druhém jarním měsíci a kolem bažiny je kalná voda.</summary>
    public class SeasonSceneTests
    {
        private const int TestPort = 9192;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startMonth\": 1.5");
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
        public IEnumerator SpringFloodIsDrawn()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var connection = bootstrap.Session.Connection;

            var flood = Object.FindAnyObjectByType<FloodView>();
            Assert.IsNotNull(flood, "Ve scéně chybí záplava.");
            yield return WaitFor(() => connection.FloodedTiles.Length > 0 && flood.TileCount == connection.FloodedTiles.Length, 5f, "Záplava se neukázala.");
            Assert.IsTrue(flood.GetComponent<MeshRenderer>().enabled);
            Assert.IsNotNull(Object.FindAnyObjectByType<TrackViews>(), "Ve scéně chybí stopy.");
        }
    }
}

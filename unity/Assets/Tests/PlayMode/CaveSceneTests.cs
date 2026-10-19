using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Jeskyně ve scéně: pod ostrovem jsou skalní stěny podzemí a ve skále jsou vidět vchody, které postava zná.</summary>
    public class CaveSceneTests
    {
        private const int TestPort = 9193;

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
        public IEnumerator CaveWallsAndEntrancesAreDrawn()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var connection = bootstrap.Session.Connection;

            var walls = GameObject.Find("Jeskyně");
            Assert.IsNotNull(walls, "Ve scéně chybí stěny jeskyní.");
            Assert.Greater(walls.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);

            var entrances = Object.FindAnyObjectByType<CaveEntranceViews>();
            Assert.IsNotNull(entrances, "Ve scéně chybí vchody do jeskyní.");
            yield return WaitFor(() => connection.CaveEntrances.Length > 0 && entrances.Count == connection.CaveEntrances.Length, 5f, "Vchody se neukázaly.");
            Assert.IsFalse(bootstrap.Session.InCave, "Postava se rodí venku.");
        }
    }
}

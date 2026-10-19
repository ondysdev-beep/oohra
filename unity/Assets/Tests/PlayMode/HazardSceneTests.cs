using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Nebezpečí ve scéně: vosí hnízda visí ve stromech a prohlídka (klíšťata) dá hlášku.</summary>
    public class HazardSceneTests
    {
        private const int TestPort = 9191;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"wasps\": true");
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
        public IEnumerator WaspNestsHangInTreesAndInspectionReports()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            yield return WaitFor(() => session.Connection.SelfSnapshot().HasValue, 5f, "Postava se neobjevila.");

            var nests = Object.FindAnyObjectByType<WaspNestViews>();
            Assert.IsNotNull(nests, "Ve scéně chybí vosí hnízda.");
            yield return WaitFor(() => nests.Count > 0 && nests.Count == session.Connection.WaspNests.Length, 5f, "Vosí hnízda se neukázala.");
            var first = session.Connection.WaspNests[0];
            var nest = nests.transform.GetChild(0);
            Assert.AreEqual(WorldCoords.Ground(first.X, first.Y) + WaspNestViews.NestHeight, nest.position.y, 0.01f);
            Assert.IsTrue(session.Connection.World.Trees.Any(t => t.TileIndex == first.TileIndex), "Hnízdo musí být ve stromě.");

            session.Connection.InspectBody();
            yield return WaitFor(() => session.Log.Any(l => l.Text.Contains("Prohlédl ses")), 5f, "Prohlídka nedala hlášku.");
        }
    }
}

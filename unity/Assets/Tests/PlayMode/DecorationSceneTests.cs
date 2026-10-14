using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Dekorace ve scéně: nabídka ze serveru, postavení lavice a její výběr kliknutím.</summary>
    public class DecorationSceneTests
    {
        private const int TestPort = 9187;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startSticks\": 3");
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
        public IEnumerator BuildBench()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var buildMode = Object.FindAnyObjectByType<BuildMode>();

            yield return WaitFor(() => session.Connection.DecorationTypes.Length > 0, 5f, "Server neposlal dekorace.");
            var bench = buildMode.Options.Single(o => o.DecorationId == "bench");
            Assert.AreEqual(BuildMode.Catalog.Length + session.Connection.DecorationTypes.Length, buildMode.Options.Length);

            buildMode.Open(bench);
            yield return PlaceAround(session, buildMode, () => session.Connection.Structures.Length > 0);
            Assert.AreEqual(1, session.Connection.Structures.Length, "Lavice se nepostavila.");
            var structure = session.Connection.Structures[0];
            Assert.AreEqual("bench", structure.Decoration);
            Assert.AreEqual("Lavice", session.StructureName(structure));
            buildMode.Close();

            yield return WaitFor(() => Object.FindAnyObjectByType<StructureViews>().Count == 1, 3f, "Lavice se neobjevila ve scéně.");
            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            Camera.main.transform.position = WorldCoords.ToUnity(structure.X, structure.Y + 2, 1.6f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(structure.X, structure.Y, 0.4f));
            yield return SaveScreenshot("dekorace.png");
        }
    }
}

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
    /// <summary>Postava postaví zeď z klacků, zeď se objeví ve scéně, a po rozebrání zase zmizí.</summary>
    public class WallSceneTests
    {
        private const int TestPort = 9179;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startSticks\": 6");
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
        public IEnumerator BuildsAndRemovesWall()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var walls = Object.FindAnyObjectByType<StructureViews>();
            var buildMode = Object.FindAnyObjectByType<BuildMode>();

            // Režim stavby: zeď pootočená o 30°, metr od postavy (zkusí všechny strany).
            buildMode.Open(BuildKind.StickWall);
            Assert.IsTrue(BuildMode.IsActive);
            buildMode.AngleDegrees = 30f;
            yield return PlaceAround(session, buildMode, () => session.Connection.Structures.Length > 0);

            Assert.AreEqual(1, session.Connection.Structures.Length, "Zeď se nepodařilo postavit. Hlášky: "
                + string.Join(" | ", session.Log.Select(l => l.Text)));
            Assert.AreEqual(30f, session.Connection.Structures[0].Angle * Mathf.Rad2Deg, 0.01f);
            yield return WaitFor(() => walls.Count == 1, 3f, "Zeď se ve scéně neobjevila.");
            yield return SaveScreenshot("zed.png");

            // Moc daleko: režim stavby stavbu přisune na 1,2 m od postavy.
            var self = session.Connection.SelfSnapshot().Value;
            buildMode.PlaceAt(self.X + 5, self.Y + 5);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsFalse(session.Log.Any(l => l.Text == "Je to moc daleko."), "Stavba nebyla přisunutá na dosah.");
            buildMode.Close();
            Assert.IsFalse(BuildMode.IsActive);

            var wall = session.Connection.Structures[0];
            session.Menu = InteractionTarget.ForStructure(wall.Id, wall.X, wall.Y);
            session.RemoveStructure();

            yield return WaitFor(() => walls.Count == session.Connection.Structures.Length && session.Connection.Structures.All(s => s.Id != wall.Id), 3f, "Zeď po rozebrání nezmizela.");
            yield return WaitFor(() => session.Count(Oohra.Core.Items.ItemKind.Stick) >= 1, 3f, "Rozebraná zeď nevrátila klacek.");
        }
    }
}

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
    /// <summary>Postava postaví zeď z klacků, zeď se objeví ve scéně, a po rozebrání zase zmizí.</summary>
    public class WallSceneTests
    {
        private const int TestPort = 9179;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startSticks\": 4");
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
            var walls = Object.FindAnyObjectByType<WallViews>();

            // Zeď vznikne na dlaždici, kam se postava dívá. Zkusí to na všechny čtyři strany.
            var directions = new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1) };
            foreach (var direction in directions)
            {
                session.Connection.SendInput(direction.x, direction.y);
                yield return new WaitForSecondsRealtime(0.1f);
                session.Connection.SendInput(0, 0);
                session.BuildWall();
                yield return new WaitForSecondsRealtime(0.7f);
                if (session.Connection.Walls.Length > 0)
                {
                    break;
                }
            }

            Assert.AreEqual(1, session.Connection.Walls.Length, "Zeď se nepodařilo postavit. Hlášky: "
                + string.Join(" | ", session.Log.Select(l => l.Text)));
            yield return WaitFor(() => walls.Count == 1, 3f, "Zeď se ve scéně neobjevila.");
            yield return SaveScreenshot("zed.png");

            var tile = session.Connection.Walls[0];
            var width = session.Connection.World.Width;
            session.Menu = InteractionTarget.ForTile(TargetKind.Wall, tile % width, tile / width);
            session.RemoveWall();

            yield return WaitFor(() => walls.Count == 0, 3f, "Zeď po rozebrání nezmizela.");
            yield return WaitFor(() => session.Count(Oohra.Core.Items.ItemKind.Stick) == 3, 3f, "Rozebraná zeď nevrátila klacek.");
        }
    }
}

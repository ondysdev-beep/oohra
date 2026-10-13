using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Celá cesta: spustí skutečný server, Unity se k němu připojí, postaví ostrov,
    /// přidá se druhý hráč a oba se vidí. Snímek uloží do unity/Logs/sit.png.
    /// </summary>
    public class IslandSceneTests
    {
        private const int TestPort = 9177;

        private Process _server;
        private GameConnection _second;

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
            _second?.Dispose();
            PlayTestTools.StopServer(_server);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConnectsAndSeesSecondPlayer()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");

            var session = bootstrap.Session;
            Assert.AreEqual(84 * 84, session.Connection.World.Tiles.Length);
            yield return WaitFor(() => GameObject.Find("Postava") != null, 5f, "Vlastní postava se neobjevila.");

            _second = new GameConnection();
            _second.Connect("127.0.0.1", TestPort, Invite, "Petr");
            yield return WaitFor(() =>
            {
                _second.Poll();
                return session.Connection.Players.Values.Any(p => p.Nickname == "Petr");
            }, 10f, "Druhý hráč se neobjevil.");
            yield return WaitFor(() =>
            {
                _second.Poll();
                return session.Connection.Snapshot.Players.Length == 2;
            }, 5f, "Ve snímku nejsou oba hráči.");

            var start = session.Connection.SelfSnapshot().Value;
            session.Connection.SendInput(0, 1);
            yield return new WaitForSecondsRealtime(1f);
            session.Connection.SendInput(0, 0);
            var moved = session.Connection.SelfSnapshot().Value;
            Assert.AreNotEqual(start.Y, moved.Y, "Server postavou nepohnul.");

            yield return SaveScreenshot("sit.png");
        }
    }
}

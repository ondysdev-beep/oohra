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
            // Ostrov 128 × 128 a pod ním podzemí s jeskyněmi.
            Assert.AreEqual(128, session.Connection.World.Width);
            Assert.Greater(session.Connection.World.Height, 128);
            Assert.AreEqual(128 * session.Connection.World.Height, session.Connection.World.Tiles.Length);
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

            // Spánek: odpočatá postava (síla 90) neusne a dostane hlášku.
            session.ToggleSleep();
            yield return WaitFor(() => session.Log.Any(l => l.Text == "Nejsi dost unavený."), 3f, "Chybí hláška o únavě.");
            Assert.IsFalse(session.Sleeping);
        }

        /// <summary>
        /// Strom mezi kamerou a postavou (kamera za stromem, dívá se na postavu) zprůhlední, aby postava byla vidět.
        /// Kamera z třetí osoby je nízko, postavu tak zakrývá hlavně kmen.
        /// </summary>
        [UnityTest]
        public IEnumerator CrownsFadeNearPlayer()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var connection = bootstrap.Session.Connection;
            var crowns = Object.FindAnyObjectByType<SeeThrough>();
            yield return WaitFor(() => connection.SelfSnapshot() != null && Object.FindAnyObjectByType<CameraRig>() != null, 5f, "Chybí poloha postavy nebo kamera.");

            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            var self = connection.SelfSnapshot().Value;
            var player = new Vector2(self.X, self.Y);
            var tree = connection.World.Trees
                .Select(t => new Vector2(t.X, t.Y))
                .Where(t => Vector2.Distance(t, player) > 3f)
                .OrderBy(t => Vector2.Distance(t, player))
                .First();
            var away = (tree - player).normalized;
            var camera = tree + away * 4f;
            Camera.main.transform.position = WorldCoords.ToUnity(camera.x, camera.y, 2f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(player.x, player.y, 1f));
            yield return WaitFor(() => crowns.FadedCount > 0, 3f, "Strom mezi kamerou a postavou se nezprůhlednil.");

            yield return new WaitForSecondsRealtime(0.5f);
            yield return SaveScreenshot("koruny.png");
        }
    }
}

using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oohra.Protocol;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Celá cesta: spustí skutečný server (Oohra.Server z server/, musí být sestavený přes dotnet build),
    /// Unity se k němu připojí, postaví ostrov, přidá se druhý hráč a oba se vidí.
    /// Snímky ukládá do unity/Logs (do gitu se nenahrávají).
    /// </summary>
    public class IslandSceneTests
    {
        private const int TestPort = 9177;
        private const string Invite = "UNITY-TEST";

        private Process _server;
        private GameConnection _second;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var serverDll = Path.Combine(repo, "server", "Oohra.Server", "bin", "Debug", "net10.0", "Oohra.Server.dll");
            Assert.IsTrue(File.Exists(serverDll), "Server není sestavený. Spusť dotnet build ve složce server.");

            var config = Path.Combine(Application.temporaryCachePath, "server-test.json");
            File.WriteAllText(config, "{ \"port\": " + TestPort + ", \"seed\": 1, \"worldSize\": 84, \"inviteCodes\": [\"" + Invite + "\"] }");
            _server = Process.Start(new ProcessStartInfo("dotnet", "\"" + serverDll + "\" \"" + config + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            SceneManager.LoadScene("Ostrov");
            yield return new WaitForSecondsRealtime(2f);
        }

        [UnityTearDown]
        public IEnumerator StopServer()
        {
            _second?.Dispose();
            if (_server != null && !_server.HasExited)
            {
                _server.Kill();
            }

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

        private static IEnumerator WaitFor(System.Func<bool> condition, float seconds, string message)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Assert.Fail(message);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Snímek z kamery (bez rozhraní OnGUI). WaitForEndOfFrame ani ScreenCapture v Unity bez okna
        /// (batch mode) nefungují, proto se kreslí přímo kamerou do textury.
        /// </summary>
        private static IEnumerator SaveScreenshot(string fileName)
        {
            yield return null;
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;

            var path = Path.Combine(Application.dataPath, "..", "Logs", fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("Snímek uložen: " + Path.GetFullPath(path));
        }
    }
}

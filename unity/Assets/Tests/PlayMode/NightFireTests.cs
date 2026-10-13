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
    /// <summary>
    /// Brána ohně a noci: svět začne ve 22:00, postava má klacky a kámen. Unity rozdělá oheň,
    /// ve scéně se objeví plamen se světlem, je tma a postavu oheň hřeje. Snímek uloží do unity/Logs/noc.png.
    /// </summary>
    public class NightFireTests
    {
        private const int TestPort = 9178;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startHour\": 22, \"startSticks\": 6, \"startStones\": 2");
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
        public IEnumerator FireLightsUpTheNight()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var fires = Object.FindAnyObjectByType<FireViews>();
            var campFires = fires.BurningCount;
            yield return new WaitForSecondsRealtime(0.5f);

            var sun = Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
            Assert.Less(sun.intensity, 0.1f, "Ve 22:00 má být tma.");

            // Oheň vznikne na dlaždici, kam se postava dívá. Zkusí to na všechny čtyři strany.
            var directions = new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1) };
            foreach (var direction in directions)
            {
                session.Connection.SendInput(direction.x, direction.y);
                yield return new WaitForSecondsRealtime(0.1f);
                session.Connection.SendInput(0, 0);
                session.LightFire();
                yield return new WaitForSecondsRealtime(0.7f);
                if (session.Connection.Fires.Length > 0)
                {
                    break;
                }
            }

            Assert.AreEqual(1, session.Connection.Fires.Length, "Oheň se nepodařilo rozdělat. Hlášky: "
                + string.Join(" | ", session.Log.Select(l => l.Text)) + " Batoh: " + session.Connection.Inventory.Stacks.Length);
            Assert.IsTrue(session.Log.Any(l => l.Text == "Oheň hoří. Je u něj teplo."));
            yield return WaitFor(() => fires.BurningCount == campFires + 1, 3f, "Plamen se světlem se neobjevil.");
            yield return WaitFor(() => session.Connection.Self.NearFire, 3f, "Oheň postavu nehřeje.");
            yield return WaitFor(() => session.Count(Oohra.Core.Items.ItemKind.Stick) == 3, 3f, "Oheň stojí 3 klacky ze 6.");

            yield return SaveScreenshot("noc.png");
        }
    }
}

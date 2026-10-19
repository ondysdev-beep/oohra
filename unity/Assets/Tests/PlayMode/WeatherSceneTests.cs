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
    /// Bouřka v noci: server má vynucenou bouřku, ve scéně padá déšť a mlha zkrátí dohled. Postava rozdělá oheň
    /// (z něj stoupá kouř) a zapálí si od něj pochodeň, která svítí v ruce. Snímek uloží do unity/Logs/bourka.png.
    /// </summary>
    public class WeatherSceneTests
    {
        private const int TestPort = 9181;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startHour\": 22, \"startSticks\": 6, \"startStones\": 2, \"forceWeather\": \"bourka\"");
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
        public IEnumerator StormRainSmokeAndTorch()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var weather = Object.FindAnyObjectByType<WeatherView>();

            yield return WaitFor(() => weather.RainAmount > 0.5f && weather.RainParticles > 200, 10f, "Déšť nepadá.");
            Assert.Greater(weather.StormAmount, 0.3f);
            Assert.IsTrue(RenderSettings.fog);
            // Mlha končí za kamerou o dohled (jasno 55 m, v bouřce méně).
            var cameraDistance = Vector3.Distance(Camera.main.transform.position, Object.FindAnyObjectByType<PlayerViews>().Self.position);
            Assert.Less(RenderSettings.fogEndDistance - cameraDistance, 40f, "V bouřce má být dohled kratší.");

            var buildMode = Object.FindAnyObjectByType<BuildMode>();
            buildMode.Open(Oohra.Core.World.BuildKind.Campfire);
            yield return PlaceAround(session, buildMode, () => session.Connection.Fires.Length > 0);
            buildMode.Close();
            Assert.AreEqual(1, session.Connection.Fires.Length, "Oheň se nepodařilo rozdělat.");
            var fires = Object.FindAnyObjectByType<FireViews>();
            Assert.IsTrue(fires.GetComponentsInChildren<ParticleSystem>().Any(p => p.name == "Kouř"), "Z ohně nestoupá kouř.");

            session.LightTorch();
            var players = Object.FindAnyObjectByType<PlayerViews>();
            yield return WaitFor(() => players.HasLitTorch(session.Connection.PlayerId), 5f, "Pochodeň v ruce nesvítí. Hlášky: "
                + string.Join(" | ", session.Log.Select(l => l.Text)));
            Assert.IsTrue(session.TorchLit);

            yield return new WaitForSecondsRealtime(1f);
            yield return SaveScreenshot("bourka.png");
        }
    }
}

namespace Oohra.Client.Tests
{
    /// <summary>Ranní mlha: dohled je jen pár metrů za postavu, mlha je šedá. Snímek unity/Logs/mlha.png.</summary>
    public class FogSceneTests
    {
        private const int TestPort = 9182;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startHour\": 8, \"forceWeather\": \"mlha\"");
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
        public IEnumerator MorningFogHidesTheDistance()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var weather = Object.FindAnyObjectByType<WeatherView>();

            yield return WaitFor(() => weather.FogAmount > 0.9f, 20f, "Mlha nepřišla.");
            var camera = Camera.main.transform.position;
            var self = Object.FindAnyObjectByType<PlayerViews>().Self.position;
            Assert.Less(RenderSettings.fogEndDistance, Vector3.Distance(camera, self) + 12f, "V mlze má být vidět jen kousek za postavu.");
            Assert.AreEqual(0, weather.RainParticles, "V mlze neprší.");

            yield return SaveScreenshot("mlha.png");
        }
    }
}

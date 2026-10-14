using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Cizinci z cizích kmenů se objeví ve scéně, jde na ně kliknout a otevřít jejich nabídku.</summary>
    public class StrangerSceneTests
    {
        private const int TestPort = 9189;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            // Velký dohled, ať server pošle cizince z obou táborů, ať hráč stojí kdekoli.
            _server = PlayTestTools.StartServer(TestPort, "\"strangers\": true, \"animalViewDistance\": 400");
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
        public IEnumerator StrangersAppearAndOpenMenu()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var views = Object.FindAnyObjectByType<StrangerViews>();

            yield return WaitFor(() => session.Connection.Strangers.Length == 6, 5f, "Server neposlal šest cizinců.");
            yield return WaitFor(() => views.Count == 6, 3f, "Cizinci se ve scéně neobjevili.");
            Assert.IsNotNull(GameObject.Find("Cizinec 0").transform.Find(GenerationLook.FigureName));

            var stranger = session.Connection.Strangers[0];
            session.Selected = InteractionTarget.ForStranger(stranger.Id, stranger.X, stranger.Y);
            yield return null;
            Assert.IsTrue(session.StrangerOf(stranger.Id).HasValue);
            Assert.IsTrue(views.WorldPositionOf(stranger.Id).HasValue);

            // Nabídka se normálně otevře, až postava k cizinci dojde. Tady se otevře rovnou.
            session.Menu = session.Selected;
            session.StartGiving();
            Assert.AreEqual(stranger.Id, session.GiveTo);
            Assert.IsTrue(session.BackpackOpen);

            // Snímek tábora: kamera nad cizincem (kamera jinak sleduje postavu).
            session.BackpackOpen = false;
            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            Camera.main.transform.position = WorldCoords.ToUnity(stranger.X, stranger.Y + 5, 5f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(stranger.X, stranger.Y, 0.8f));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return SaveScreenshot("cizinci.png");
        }
    }
}

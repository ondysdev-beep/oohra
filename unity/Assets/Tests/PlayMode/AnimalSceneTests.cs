using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Zvířata v okolí postavy se objeví ve scéně a dá se na ně kliknout (výběr).</summary>
    public class AnimalSceneTests
    {
        private const int TestPort = 9181;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"animals\": true, \"animalViewDistance\": 60");
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
        public IEnumerator AnimalsAppearNearby()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var views = Object.FindAnyObjectByType<AnimalViews>();

            yield return WaitFor(() => session.Connection.Animals.Length > 0, 5f, "Server neposlal žádná zvířata v okolí.");
            yield return WaitFor(() => views.Count == session.Connection.Animals.Length, 3f, "Zvířata se ve scéně neobjevila.");

            var animal = session.Connection.Animals[0];
            session.Selected = InteractionTarget.ForAnimal(animal.Id, animal.X, animal.Y);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<SelectionRing>().Visible);
            Assert.IsNotNull(session.AnimalOf(animal.Id));

            // Snímek: kamera nad zvířetem, ať je na něj vidět (kamera jinak sleduje postavu).
            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            Camera.main.transform.position = WorldCoords.ToUnity(animal.X, animal.Y + 3, 3.5f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(animal.X, animal.Y, 0.3f));
            yield return SaveScreenshot("zvirata.png");
        }
    }
}

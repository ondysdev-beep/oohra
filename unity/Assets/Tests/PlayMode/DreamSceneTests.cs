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
    /// <summary>Sen bez slov (návrh 4.2): nový hráč ho uvidí po přihlášení, rozhraní mezitím čeká, pak se probudí.</summary>
    public class DreamSceneTests
    {
        private const int TestPort = 9190;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort);
            DreamView.Disabled = false;
            SceneManager.LoadScene("Ostrov");
            yield return new WaitForSecondsRealtime(2f);
        }

        [UnityTearDown]
        public IEnumerator StopServer()
        {
            DreamView.Disabled = true;
            PlayTestTools.StopServer(_server);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewPlayerDreamsThenWakes()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;

            yield return WaitFor(() => DreamView.Active, 3f, "Sen se nespustil.");
            var dream = Object.FindAnyObjectByType<DreamView>();
            var camera = GameObject.Find("Kamera snu").GetComponent<Camera>();

            // Snímek z každé scény (bobule, divočák, noc a oheň, jezírko), v druhé půlce scény.
            for (var scene = 0; scene < DreamView.SceneCount; scene++)
            {
                yield return WaitFor(() => dream.Scene == scene, DreamView.SceneSeconds + 1f, "Scéna " + scene + " nezačala.");
                yield return new WaitForSeconds(DreamView.SceneSeconds * 0.7f);
                yield return SaveScreenshot("sen" + (scene + 1) + ".png", camera);
            }

            yield return WaitFor(() => !DreamView.Active, DreamView.SceneSeconds + 2f, "Sen neskončil.");
            Assert.IsTrue(Camera.main != null && Camera.main.enabled);
            Assert.IsTrue(session.Log.Any(l => l.Text.StartsWith("Probudil ses")));
        }

        /// <summary>Mezerník (tady Finish) sen přeskočí.</summary>
        [UnityTest]
        public IEnumerator DreamCanBeSkipped()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Petr");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            yield return WaitFor(() => DreamView.Active, 3f, "Sen se nespustil.");

            Object.FindAnyObjectByType<DreamView>().Finish();
            yield return null;

            Assert.IsFalse(DreamView.Active);
            Assert.IsNull(GameObject.Find("Sen"));
        }
    }
}

using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using Oohra.Core.Language;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Řeč ve scéně: Pradávný má v zápisníku Starý hlas, řečené slovo a gesto se ukážou jako bublina.</summary>
    public class SpeechSceneTests
    {
        private const int TestPort = 9182;

        private Process _server;

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
            PlayTestTools.StopServer(_server);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SayWordAndGesture()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var hud = Object.FindAnyObjectByType<SpeechHud>();

            yield return WaitFor(() => session.Connection.Vocabulary.Entries.Length == 4, 5f, "Pradávný nezná Starý hlas.");

            hud.SpeechOpen = true;
            hud.Input.Set(new byte[] { 1, 2 });
            hud.Tone = Tone.Alarm;
            hud.SayWord();
            Assert.AreEqual(0, hud.Input.Count);
            yield return WaitFor(() => hud.VisibleBubbles == 1, 3f, "Nad postavou se neukázala bublina se slovem.");
            yield return SaveScreenshot("rec.png");

            hud.MakeGesture(Gesture.Point);
            yield return WaitFor(() => hud.VisibleBubbles == 1, 3f, "Gesto se neukázalo.");

            hud.SpeechOpen = false;
            hud.NotebookOpen = true;
            yield return null;
            yield return SaveScreenshot("slovnik.png");
        }
    }
}

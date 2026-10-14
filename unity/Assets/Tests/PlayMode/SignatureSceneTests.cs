using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Podpis ve hře: uložit jméno ze slabik a znak, server ho vrátí a jde přikreslit do značky.</summary>
    public class SignatureSceneTests
    {
        private const int TestPort = 9188;

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
        public IEnumerator SaveSignature()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var connection = bootstrap.Session.Connection;
            var hud = Object.FindAnyObjectByType<SignatureHud>();

            hud.Show();
            Assert.IsTrue(hud.Open);
            Assert.IsFalse(hud.Save(), "Bez jména se podpis uložit nemá.");
            hud.NameInput.Set(new byte[] { 2, 8 });
            SignBits.Set(hud.Mark, 6, 6, true);
            Assert.IsTrue(hud.Save());

            yield return WaitFor(() => connection.SignatureName.Length == 2, 5f, "Server nevrátil podpis.");
            Assert.IsTrue(SignBits.Get(connection.SignatureMark, 6, 6));

            // Po dalším otevření je v panelu uložený podpis.
            hud.Show();
            Assert.AreEqual(2, hud.NameInput.Count);
            Assert.IsTrue(SignBits.Get(hud.Mark, 6, 6));
        }
    }
}

using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Značky ve scéně: staré rytiny u ruin, nakreslení vlastní značky a její přečtení.</summary>
    public class SignSceneTests
    {
        private const int TestPort = 9183;

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
        public IEnumerator DrawAndReadSign()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var views = Object.FindAnyObjectByType<SignViews>();
            var ruins = session.Connection.World.Ruins.Length;

            yield return WaitFor(() => session.Connection.Signs.Length == ruins && views.Count == ruins, 5f, "Staré rytiny se neobjevily.");
            yield return WaitFor(() => session.Connection.SelfSnapshot() != null, 5f, "Server neposlal polohu postavy.");

            var hud = Object.FindAnyObjectByType<SignHud>();
            hud.EditorOpen = true;
            for (var i = 0; i < SignBits.Size; i++)
            {
                SignBits.Set(hud.Drawing, i, i, true);
                SignBits.Set(hud.Drawing, SignBits.Size - 1 - i, i, true);
            }

            hud.Submit();
            Assert.IsFalse(hud.EditorOpen);
            yield return WaitFor(() => session.Connection.Signs.Length == ruins + 1 && views.Count == ruins + 1, 5f, "Nakreslená značka se neobjevila.");

            var sign = session.Connection.Signs.Single(s => !s.Ancient);
            Assert.AreEqual(session.Connection.PlayerId, sign.AuthorId);
            Assert.AreEqual(2 * SignBits.Size, SignBits.Count(sign.Bits));
            session.Menu = InteractionTarget.ForSign(sign.Id, sign.X, sign.Y);
            Assert.IsNotNull(session.MenuSign());

            // Snímek: kamera nad značkou.
            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            Camera.main.transform.position = WorldCoords.ToUnity(sign.X, sign.Y + 1.2, 1.6f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(sign.X, sign.Y, 0f));
            yield return SaveScreenshot("znacka.png");

            var ruin = session.Connection.Signs.First(s => s.Ancient);
            Camera.main.transform.position = WorldCoords.ToUnity(ruin.X, ruin.Y + 1.5, 0.8f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(ruin.X, ruin.Y, 0.3f));
            yield return SaveScreenshot("rytina.png");

            session.EraseSign();
            yield return WaitFor(() => views.Count == ruins, 5f, "Smazaná značka zůstala ve scéně.");
        }
    }
}

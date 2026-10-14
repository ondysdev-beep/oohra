using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Oohra.Core.Items;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>Zásoby kmene ve scéně: postavit koš, uložit do něj kámen a vzít ho zpátky.</summary>
    public class TribeSceneTests
    {
        private const int TestPort = 9184;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startSticks\": 4, \"startStones\": 1");
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
        public IEnumerator StockpileStoreAndTake()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var connection = session.Connection;
            var buildMode = Object.FindAnyObjectByType<BuildMode>();

            buildMode.Open(BuildKind.Stockpile);
            yield return PlaceAround(session, buildMode, () => connection.Stockpiles.Length > 0);
            Assert.AreEqual(1, connection.Stockpiles.Length, "Koš se nepostavil.");
            var basket = connection.Structures.Single(s => s.Kind == BuildKind.Stockpile);
            Assert.AreEqual(connection.PlayerId, basket.OwnerId);

            session.Menu = InteractionTarget.ForStructure(basket.Id, basket.X, basket.Y);
            Assert.IsNotNull(session.MenuStockpile());
            Assert.IsTrue(session.MayTouch(basket));

            var stone = System.Array.FindIndex(connection.Inventory.Stacks, s => s.Kind == ItemKind.Stone);
            connection.StoreItem(basket.Id, stone);
            yield return WaitFor(() => session.MenuStockpile().Contents.Stacks.Any(s => s.Kind == ItemKind.Stone), 5f, "Kámen se do koše neuložil.");

            Object.FindAnyObjectByType<CameraRig>().enabled = false;
            Camera.main.transform.position = WorldCoords.ToUnity(basket.X, basket.Y + 1.6, 1.8f);
            Camera.main.transform.LookAt(WorldCoords.ToUnity(basket.X, basket.Y, 0.2f));
            yield return SaveScreenshot("kos.png");

            connection.TakeItem(basket.Id, 0);
            yield return WaitFor(() => session.Count(ItemKind.Stone) == 1 && session.MenuStockpile().Contents.Stacks.Length == 0, 5f, "Kámen se z koše nevzal.");
        }
    }
}

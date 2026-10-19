using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Oohra.Core.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Oohra.Client.Tests.PlayTestTools;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Výroba ve hře: nováček má jen dvě ruce, Pradávný zná základní recepty (přijdou ze serveru), spojením si vyrobí sekeru a pak nůž.
    /// Zkoušení spojením věcí, ze kterých nic nejde, napíše hlášku a nic nespotřebuje.
    /// </summary>
    public class CraftSceneTests
    {
        private const int TestPort = 9189;

        private Process _server;

        [UnitySetUp]
        public IEnumerator StartServer()
        {
            _server = PlayTestTools.StartServer(TestPort, "\"startStones\": 4, \"startSticks\": 1");
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
        public IEnumerator HandsRecipesAndKnife()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var connection = session.Connection;

            yield return WaitFor(() => connection.Recipes.Any(r => r.Id == "knife"), 5f, "Recepty Pradávného nepřišly.");
            Assert.AreEqual(2, connection.Inventory.Capacity, "Nováček má jen dvě ruce.");
            Assert.IsFalse(connection.Inventory.Bag.HasValue);

            // Zaškrtnout klacek a kameny a zkusit spojit: Pradávný zná kopí i sekeru, víc materiálu má sekera.
            session.ToggleCombine(0);
            session.ToggleCombine(1);
            session.CombineSelected();
            yield return WaitFor(() => session.Count(ItemKind.Axe) == 1, 5f, "Sekera se spojením nevyrobila.");
            Assert.IsTrue(session.Log.Any(l => l.Text.StartsWith("Hotovo: kamenná sekera")), "Chybí hláška s názvem receptu.");

            // Ze dvou zbylých kamenů nůž (recept ze seznamu, jako tlačítko v batohu).
            session.Craft("knife");
            yield return WaitFor(() => session.Count(ItemKind.Knife) == 1, 5f, "Nůž se nevyrobil. Hlášky: " + string.Join(" | ", session.Log.Select(l => l.Text)));
            Assert.AreEqual(0, session.Count(ItemKind.Stone));
        }

        /// <summary>
        /// Věci na zemi: kameny se položí na zem (nezmizí, je je vidět), sekera se vyrobí z klacku v ruce a kamenů
        /// na zemi a zbylé kameny jdou sebrat.
        /// </summary>
        [UnityTest]
        public IEnumerator DropOnGroundCraftFromItAndPickUp()
        {
            var bootstrap = Object.FindAnyObjectByType<IslandBootstrap>();
            bootstrap.Connect("127.0.0.1", TestPort, Invite, "Ondra");
            yield return WaitFor(() => bootstrap.Session != null, 15f, "Unity se nepřipojilo k serveru.");
            var session = bootstrap.Session;
            var connection = session.Connection;
            var views = Object.FindAnyObjectByType<GroundItemViews>();
            yield return WaitFor(() => connection.Recipes.Any(r => r.Id == "axe") && session.Count(ItemKind.Stone) == 4, 5f, "Recepty nebo kameny nepřišly.");

            connection.Drop(System.Array.FindIndex(connection.Inventory.Stacks, s => s.Kind == ItemKind.Stone));
            yield return WaitFor(() => views.Count == 1 && session.NearbyGround().Count == 1, 5f, "Kameny na zemi nejsou vidět.");
            Assert.AreEqual(0, session.Count(ItemKind.Stone));
            var recipe = connection.Recipes.First(r => r.Id == "axe");
            Assert.IsTrue(session.HasMaterials(recipe), "Kameny na zemi u postavy se počítají jako materiál.");

            session.Craft("axe");
            yield return WaitFor(() => session.Count(ItemKind.Axe) == 1, 5f, "Sekera z věcí na zemi se nevyrobila. Hlášky: " + string.Join(" | ", session.Log.Select(l => l.Text)));
            yield return WaitFor(() => connection.GroundItems.Length == 1 && connection.GroundItems[0].Stack.Count == 2, 5f, "Na zemi měly zbýt dva kameny.");

            connection.PickUpItem(connection.GroundItems[0].Id);
            yield return WaitFor(() => session.Count(ItemKind.Stone) == 2 && views.Count == 0, 5f, "Kameny ze země se nesebraly.");
        }
    }
}

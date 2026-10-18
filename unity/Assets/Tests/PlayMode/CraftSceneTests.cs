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
    }
}

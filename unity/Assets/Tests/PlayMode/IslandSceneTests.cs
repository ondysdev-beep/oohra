using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oohra.Core.Plants;
using Oohra.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Spustí scénu ostrova a vyzkouší hru tak, jak ji hraje hráč (bez myši): sběr, jídlo, pití, smrt.
    /// Test selže i tehdy, když se v konzoli objeví chyba. Snímky ukládá do unity/Logs (do gitu se nenahrávají).
    /// </summary>
    public class IslandSceneTests
    {
        private GameState _state;

        [UnitySetUp]
        public IEnumerator LoadIsland()
        {
            SceneManager.LoadScene("Ostrov");
            yield return null;
            yield return null;
            _state = Object.FindAnyObjectByType<IslandBootstrap>().State;
        }

        [UnityTest]
        public IEnumerator IslandSceneStartsWithoutErrors()
        {
            for (var i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.IsNotNull(GameObject.Find("Postava"), "Postava ve scéně chybí.");
            Assert.IsNotNull(GameObject.Find("Ostrov (vygenerovaný)"), "Ostrov ve scéně chybí.");
            yield return SaveScreenshot("ostrov.png");
        }

        [UnityTest]
        public IEnumerator PickEatAndDrink()
        {
            var plant = _state.Island.Plants.First(p => p.Tile == TileType.Grass);
            _state.Player.Respawn(_state.Character, new Position(plant.X, plant.Y));

            _state.Harvest(plant, PlantPart.Root);
            var other = _state.Island.Plants.First(p => p.Tile == TileType.Grass && p.SpeciesId != plant.SpeciesId);
            _state.Player.Respawn(_state.Character, new Position(other.X, other.Y));
            _state.Harvest(other, PlantPart.Leaf);
            Assert.AreEqual(2, _state.Inventory.Stacks.Count);
            Assert.IsTrue(_state.PlantStates.IsGone(plant.Id, _state.Clock.TotalMinutes));

            _state.BackpackOpen = true;
            yield return null;
            yield return SaveScreenshot("batoh.png");

            _state.Taste(0);
            _state.Eat(0);
            _state.BackpackOpen = false;
            Assert.IsTrue(_state.Log.Any(l => l.Text.StartsWith("Ochutnal jsi")));
            Assert.IsTrue(_state.Log.Any(l => l.Text == "Snědl jsi to."));

            var thirstBefore = DrinkNearPond();
            Assert.Greater(_state.Character.Hydration, thirstBefore);
            Assert.IsTrue(_state.Log.Any(l => l.Text == "Čistá, studená voda."));
        }

        [UnityTest]
        public IEnumerator DeathAndRebirthAsNextGeneration()
        {
            _state.Inventory.TryAdd(new PlantItem(0, PlantPart.Leaf, Ripeness.Ripe, Oohra.Core.Season.Spring, TileType.Grass));
            _state.Character.Hydration = 0;
            _state.Character.Health = 0.05;
            _state.TimeSpeed = 60;
            for (var i = 0; i < 300 && !_state.Character.IsDead; i++)
            {
                yield return null;
            }

            Assert.IsTrue(_state.Character.IsDead);
            Assert.AreEqual(Oohra.Core.Characters.DeathCause.Thirst, _state.Character.DeathCause);
            Assert.IsTrue(_state.Paused);
            yield return SaveScreenshot("smrt.png");

            _state.Reborn();
            Assert.IsFalse(_state.Character.IsDead);
            Assert.AreEqual(1, _state.Character.Traits.Generation);
            Assert.AreEqual(0, _state.Inventory.Stacks.Count);
        }

        /// <summary>Postaví postavu na souš vedle jezírka a napije se. Vrací žízeň před pitím.</summary>
        private double DrinkNearPond()
        {
            var island = _state.Island;
            for (var y = 1; y < island.Height - 1; y++)
            {
                for (var x = 1; x < island.Width - 1; x++)
                {
                    if (island.TileAt(x, y) != TileType.Pond || !Movement.IsWalkable(island, x + 0.5, y + 1.5, 0.28))
                    {
                        continue;
                    }

                    _state.Player.Respawn(_state.Character, new Position(x + 0.5, y + 1.5));
                    _state.Character.Hydration = 20;
                    _state.Drink(x, y);
                    return 20;
                }
            }

            Assert.Fail("Na ostrově není jezírko s průchozím břehem.");
            return 0;
        }

        /// <summary>
        /// Snímek z kamery (bez rozhraní OnGUI). WaitForEndOfFrame ani ScreenCapture v Unity bez okna
        /// (batch mode) nefungují, proto se kreslí přímo kamerou do textury.
        /// </summary>
        private static IEnumerator SaveScreenshot(string fileName)
        {
            yield return null;
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;

            var path = Path.Combine(Application.dataPath, "..", "Logs", fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("Snímek uložen: " + Path.GetFullPath(path));
        }
    }
}

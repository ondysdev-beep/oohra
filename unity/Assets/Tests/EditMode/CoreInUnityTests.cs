using NUnit.Framework;
using Oohra.Core.Characters;
using Oohra.Core.Data;
using Oohra.Core.Plants;
using Oohra.Core.Rng;
using Oohra.Core.Time;
using Oohra.Core.World;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Ověří, že Oohra.Core (verze .NET Standard 2.1) a Newtonsoft fungují uvnitř Unity.
    /// Pravidla samotná testuje Oohra.Core.Tests v řešení server/.
    /// </summary>
    public class CoreInUnityTests
    {
        [Test]
        public void DataFilesLoad()
        {
            Assert.AreEqual(8, GameData.Archetypes.Count);
            Assert.AreEqual(128, GameData.Settings.WorldWidth);
            Assert.AreEqual(70, GameData.Body.Start.Satiety);
            Assert.AreEqual(4, GameData.Movement.WalkSpeed);
            Assert.AreEqual(9, GameData.Gathering.Inventory.StackSize);
        }

        // Stejná čísla jako golden data z prototypu (seed 12345, 84 × 84).
        [Test]
        public void IslandIsSameAsInPrototype()
        {
            var island = IslandGenerator.Build(12345, 84, 84);

            Assert.AreEqual(558, island.Trees.Count);
            Assert.AreEqual(190, island.Plants.Count);
            Assert.AreEqual(new GridPoint(51, 31), island.Camps[0]);
        }

        [Test]
        public void EatingAndPoisonWork()
        {
            var island = IslandGenerator.Build(1, 84, 84);
            var clock = new GameClock(7 * 60, 5400, 32400, 12);
            var character = new Character(1, new Mulberry32(1));
            var secret = island.SpeciesSecrets[0];
            var item = new PlantItem(0, PlantPart.Leaf, Ripeness.Ripe, clock.Season, TileType.Grass);

            Eating.Eat(character, item, secret, clock);
            Physiology.Run(character, clock, 120);

            Assert.IsFalse(character.IsDead);
            Assert.Less(character.Hydration, 75);
        }
    }
}

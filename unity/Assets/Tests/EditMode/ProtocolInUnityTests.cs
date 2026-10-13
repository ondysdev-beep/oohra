using System;
using System.Linq;
using LiteNetLib.Utils;
using NUnit.Framework;
using Oohra.Core;
using Oohra.Core.Characters;
using Oohra.Core.Items;
using Oohra.Core.Plants;
using Oohra.Core.World;
using Oohra.Protocol;

namespace Oohra.Client.Tests
{
    /// <summary>
    /// Ověří, že protokol (zprávy klient–server) funguje i uvnitř Unity
    /// a že klient nemá knihovnu Oohra.Core se skrytými daty.
    /// </summary>
    public class ProtocolInUnityTests
    {
        [Test]
        public void ClientHasNoHiddenData()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToList();

            CollectionAssert.DoesNotContain(loaded, "Oohra.Core");
            CollectionAssert.Contains(loaded, "Oohra.Protocol");
        }

        [Test]
        public void WorldSurvivesTheWire()
        {
            var world = new WorldView
            {
                Width = 2,
                Height = 1,
                Tiles = new[] { TileType.Sea, TileType.Grass },
                Shore = new[] { true, false },
                Trees = new[] { new TreeView(1, 0.5f) },
                Plants = new[] { new PlantView(0, 1.5f, 0.5f) },
                Species = new[] { new SpeciesView(120, 40, 45, 25, 2) },
                Camps = new CampView[0],
                Ruins = new[] { new RuinView(1.5f, 0.5f) },
            };
            var writer = new NetDataWriter();
            Messages.WriteWorld(writer, world);
            var reader = new NetDataReader(writer.CopyData());

            Assert.AreEqual(MessageType.World, (MessageType)reader.GetByte());
            var copy = Messages.ReadWorld(reader);

            Assert.AreEqual(world.Tiles, copy.Tiles);
            Assert.AreEqual(world.Shore, copy.Shore);
            Assert.AreEqual(world.Plants[0], copy.Plants[0]);
            Assert.AreEqual(world.Species[0], copy.Species[0]);
        }

        [Test]
        public void BodyStateSurvivesTheWire()
        {
            var state = new SelfState(50, 60, 70, 40, 90, new[] { Symptom.Fever, Symptom.Tremor }, 2, false, null, true, true);
            var writer = new NetDataWriter();
            Messages.WriteSelfState(writer, state);
            var reader = new NetDataReader(writer.CopyData());
            reader.GetByte();

            var copy = Messages.ReadSelfState(reader);

            Assert.AreEqual(60, copy.Hydration);
            Assert.AreEqual(state.Symptoms, copy.Symptoms);
            Assert.AreEqual(2, copy.Generation);
            Assert.IsNull(copy.DeathCause);
            Assert.IsTrue(copy.NearFire);
            Assert.IsTrue(copy.Sheltered);
        }

        [Test]
        public void InventorySurvivesTheWire()
        {
            var inventory = new InventoryView(10, new[] { new StackView(ItemKind.Plant, 3, PlantPart.Fruit, Ripeness.Overripe, Cooking.Baked, 4), new StackView(ItemKind.Stick, 0, PlantPart.Leaf, Ripeness.Unripe, Cooking.Raw, 5) });
            var writer = new NetDataWriter();
            Messages.WriteInventory(writer, inventory);
            var reader = new NetDataReader(writer.CopyData());
            reader.GetByte();

            var copy = Messages.ReadInventory(reader);

            Assert.AreEqual(10, copy.Capacity);
            Assert.AreEqual(inventory.Stacks, copy.Stacks);
        }
    }
}

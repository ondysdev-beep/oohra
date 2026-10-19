using NUnit.Framework;
using Oohra.Protocol;
using UnityEditor;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Katalog zvuků: nahrávky ze složky Sounds jsou zařazené a dlouhé smyčky se nenačítají celé do paměti.</summary>
    public class SoundCatalogTests
    {
        [Test]
        public void RecordingsAreInCatalog()
        {
            var sounds = SoundCatalog.Current;
            Assert.IsNotNull(sounds.FireLoop, "Oheň");
            Assert.IsNotNull(sounds.RainLoop, "Déšť");
            Assert.IsNotNull(sounds.StormLoop, "Bouřka");
            Assert.IsNotNull(sounds.SeaLoop, "Moře");
            Assert.IsNotEmpty(sounds.StepsGrass, "Kroky po trávě");
            Assert.IsNotEmpty(sounds.Chop, "Sekera");
            Assert.IsNotEmpty(sounds.TreeFall, "Pád stromu");
            Assert.IsNotEmpty(sounds.TakeStone, "Kámen");
            Assert.IsNotEmpty(sounds.StoneWork, "Opracování kamene");
            Assert.IsNotEmpty(sounds.Build, "Stavba");
            Assert.AreEqual(2, sounds.Stomach.Length, "Kručení v břiše");
        }

        [Test]
        public void LongLoopsStreamAndShortSoundsAreLoaded()
        {
            var sounds = SoundCatalog.Current;
            foreach (var loop in new[] { sounds.FireLoop, sounds.RainLoop, sounds.StormLoop, sounds.SeaLoop })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(loop));
                Assert.AreEqual(AudioClipLoadType.Streaming, importer.defaultSampleSettings.loadType, loop.name);
            }

            var chop = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sounds.Chop[0]));
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, chop.defaultSampleSettings.loadType);
            Assert.IsTrue(chop.forceToMono, "Zvuk z místa je mono.");
        }

        /// <summary>Kroky podle země: bez nahrávky pro písek se použije tráva, bez trávy nic (ťuknutí z kódu).</summary>
        [Test]
        public void StepsFallBackToGrass()
        {
            var world = new WorldView { Width = 2, Height = 1, Tiles = new[] { Oohra.Core.World.TileType.Grass, Oohra.Core.World.TileType.Sand } };
            var catalog = ScriptableObject.CreateInstance<SoundCatalog>();
            Assert.IsNull(Ambience.StepClip(catalog, world, 1.5, 0.5, 0, new int[0]));

            var grass = AudioClip.Create("trava", 10, 1, 22050, false);
            var sand = AudioClip.Create("pisek", 10, 1, 22050, false);
            catalog.StepsGrass = new[] { grass };
            Assert.AreSame(grass, Ambience.StepClip(catalog, world, 1.5, 0.5, 0, new int[0]));
            catalog.StepsSand = new[] { sand };
            Assert.AreSame(sand, Ambience.StepClip(catalog, world, 1.5, 0.5, 0, new int[0]));
            Assert.AreSame(grass, Ambience.StepClip(catalog, world, 0.5, 0.5, 0, new int[0]));
        }
    }
}

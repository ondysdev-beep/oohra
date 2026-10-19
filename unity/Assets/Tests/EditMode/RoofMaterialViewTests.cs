using NUnit.Framework;
using Oohra.Core.Items;
using Oohra.Core.World;
using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Střechy z materiálů v Unity: nabídka stavby s cenou, barva podle materiálu, názvy a texty.</summary>
    public class RoofMaterialViewTests
    {
        [Test]
        public void EveryMaterialHasBuildOptionAndColor()
        {
            foreach (RoofMaterial material in System.Enum.GetValues(typeof(RoofMaterial)))
            {
                var option = System.Array.Find(BuildMode.Catalog, o => o.Kind == BuildKind.Roof && o.DecorationId == material.ToString());
                Assert.IsNotNull(option, material.ToString());
            }

            var names = new System.Collections.Generic.HashSet<string>();
            foreach (RoofMaterial material in System.Enum.GetValues(typeof(RoofMaterial)))
            {
                names.Add(Texts.RoofName(material.ToString()));
            }

            Assert.AreEqual(4, names.Count, "Každý materiál má svůj název.");
            var bark = System.Array.Find(BuildMode.Catalog, o => o.DecorationId == "Bark");
            Assert.AreEqual(ItemKind.Bast, bark.ExtraItem);
            Assert.AreNotEqual(StructureViews.RoofColor("Bark"), StructureViews.RoofColor("Shingles"));
        }

        [Test]
        public void FireAndRepairTexts()
        {
            Assert.AreNotEqual(BuildResult.Repaired.ToString(), Texts.For(BuildResult.Repaired));
            Assert.AreNotEqual(BuildResult.CannotRepair.ToString(), Texts.For(BuildResult.CannotRepair));
        }
    }
}

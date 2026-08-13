using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.Entity;

namespace ACE.Server.Tests
{
    [TestClass]
    public class SummonRestrictedLandblocksTests
    {
        [TestMethod]
        public void TryParseSerializedEntry_LegacyAllVariantsEntry()
        {
            Assert.IsTrue(SummonRestrictedLandblocks.TryParseSerializedEntry("0x0066:Conquest Arena", out var key, out var label));
            Assert.AreEqual((ushort)0x0066, key.Landblock);
            Assert.IsNull(key.Variation);
            Assert.AreEqual("Conquest Arena", label);
        }

        [TestMethod]
        public void TryParseSerializedEntry_VariantSpecificEntry()
        {
            Assert.IsTrue(SummonRestrictedLandblocks.TryParseSerializedEntry("0x0066@2:Arena v2", out var key, out var label));
            Assert.AreEqual((ushort)0x0066, key.Landblock);
            Assert.AreEqual(2, key.Variation);
            Assert.AreEqual("Arena v2", label);
        }

        [TestMethod]
        public void TryParseSerializedEntry_VariantWithoutLabel()
        {
            Assert.IsTrue(SummonRestrictedLandblocks.TryParseSerializedEntry("0x0066@2", out var key, out var label));
            Assert.AreEqual((ushort)0x0066, key.Landblock);
            Assert.AreEqual(2, key.Variation);
            Assert.IsNull(label);
        }

        [TestMethod]
        public void FormatSerializedEntry_RoundTrip()
        {
            var allVariants = SummonRestrictedLandblocks.FormatSerializedEntry(0x0066, null, "Conquest Arena");
            Assert.AreEqual("0x0066:Conquest Arena", allVariants);
            Assert.IsTrue(SummonRestrictedLandblocks.TryParseSerializedEntry(allVariants, out var allKey, out var allLabel));
            Assert.AreEqual((ushort)0x0066, allKey.Landblock);
            Assert.IsNull(allKey.Variation);
            Assert.AreEqual("Conquest Arena", allLabel);

            var variantSpecific = SummonRestrictedLandblocks.FormatSerializedEntry(0x0066, 2, "Arena v2");
            Assert.AreEqual("0x0066@2:Arena v2", variantSpecific);
            Assert.IsTrue(SummonRestrictedLandblocks.TryParseSerializedEntry(variantSpecific, out var variantKey, out var variantLabel));
            Assert.AreEqual((ushort)0x0066, variantKey.Landblock);
            Assert.AreEqual(2, variantKey.Variation);
            Assert.AreEqual("Arena v2", variantLabel);
        }

        [TestMethod]
        public void FormatVariantSuffix_AllAndSpecific()
        {
            Assert.AreEqual(" (all)", SummonRestrictedLandblocks.FormatVariantSuffix(null));
            Assert.AreEqual(" v2", SummonRestrictedLandblocks.FormatVariantSuffix(2));
        }

        [TestMethod]
        public void IsRestrictedForVariation_AllVariantsEntryMatchesAnyVariant()
        {
            var entries = new List<(ushort Landblock, int? Variation)> { (0x0066, null) };

            Assert.IsTrue(SummonRestrictedLandblocks.IsRestrictedForVariation(entries, 0x0066, 0));
            Assert.IsTrue(SummonRestrictedLandblocks.IsRestrictedForVariation(entries, 0x0066, 2));
        }

        [TestMethod]
        public void IsRestrictedForVariation_SpecificVariantOnlyMatchesThatVariant()
        {
            var entries = new List<(ushort Landblock, int? Variation)> { (0x0066, 2) };

            Assert.IsFalse(SummonRestrictedLandblocks.IsRestrictedForVariation(entries, 0x0066, 0));
            Assert.IsTrue(SummonRestrictedLandblocks.IsRestrictedForVariation(entries, 0x0066, 2));
            Assert.IsFalse(SummonRestrictedLandblocks.IsRestrictedForVariation(entries, 0x0066, 3));
        }

        [TestMethod]
        public void IsRestrictedForVariation_UnrelatedLandblockNotRestricted()
        {
            var entries = new List<(ushort Landblock, int? Variation)> { (0x0066, null), (0x0066, 2) };

            Assert.IsFalse(SummonRestrictedLandblocks.IsRestrictedForVariation(entries, 0x00AB, 0));
        }
    }
}

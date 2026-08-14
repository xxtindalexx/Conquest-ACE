using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories.Tables;

namespace ACE.Server.Tests
{
    [TestClass]
    public class ShieldPropertyTests
    {
        [TestMethod]
        public void ProcChance_IsZeroAtBase100()
        {
            var chance = ShieldPropertyFormula.GetProcChance(SkillAdvancementClass.Specialized, 100, 0.25f);
            Assert.AreEqual(0.0f, chance, 0.0001f);
        }

        [TestMethod]
        public void ProcChance_ReachesMaxAtBase433Spec()
        {
            var chance = ShieldPropertyFormula.GetProcChance(SkillAdvancementClass.Specialized, 433, 0.25f);
            Assert.AreEqual(0.25f, chance, 0.001f);
        }

        [TestMethod]
        public void ProcChance_TrainedIsLowerThanSpec()
        {
            var trained = ShieldPropertyFormula.GetProcChance(SkillAdvancementClass.Trained, 300, 0.50f);
            var spec = ShieldPropertyFormula.GetProcChance(SkillAdvancementClass.Specialized, 300, 0.50f);
            Assert.IsLessThan(spec, trained);
        }

        [TestMethod]
        public void ProcChance_UntrainedReturnsZero()
        {
            var chance = ShieldPropertyFormula.GetProcChance(SkillAdvancementClass.Untrained, 300, 0.25f);
            Assert.AreEqual(0.0f, chance);
        }

        [TestMethod]
        public void LootTier_OnlyTier9IsEligible()
        {
            Assert.IsFalse(ShieldPropertyLootChance.IsLootTier(8));
            Assert.IsTrue(ShieldPropertyLootChance.IsLootTier(9));
        }
    }
}

using System;
using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    public static class ShieldPropertyFormula
    {
        /// <summary>
        /// Shield skill-scaled proc chance for CriticalBlock / GlancingBlow.
        /// Follows the Magic Absorb shield skill curve.
        /// </summary>
        public static float GetProcChance(SkillAdvancementClass advancementClass, uint baseSkill, float maxPercent)
        {
            if (maxPercent <= 0)
                return 0.0f;

            if (advancementClass < SkillAdvancementClass.Trained || baseSkill < 100)
                return 0.0f;

            var cappedSkill = Math.Min(baseSkill, 433u);
            var specMod = advancementClass == SkillAdvancementClass.Specialized ? 1.0f : 0.8f;

            var chance = (maxPercent * specMod * cappedSkill * 0.003f) - (maxPercent * specMod * 0.3f);

            return Math.Max(0.0f, Math.Min(1.0f, chance));
        }
    }
}

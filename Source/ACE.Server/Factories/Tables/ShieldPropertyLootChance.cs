using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories.Entity;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories.Tables
{
    public enum ShieldPropertyCombo
    {
        Bulwark,
        Tactical,
        Balanced,
        SentinelOnly
    }

    public static class ShieldPropertyLootChance
    {
        private static readonly ChanceTable<bool> PropertyRoll = new ChanceTable<bool>()
        {
            ( false, 0.85f ),
            ( true,  0.15f ),
        };

        private static readonly ChanceTable<ShieldPropertyCombo> ComboRoll = new ChanceTable<ShieldPropertyCombo>()
        {
            ( ShieldPropertyCombo.Bulwark,      0.35f ),
            ( ShieldPropertyCombo.Tactical,     0.35f ),
            ( ShieldPropertyCombo.Balanced,     0.20f ),
            ( ShieldPropertyCombo.SentinelOnly, 0.10f ),
        };

        private static readonly ChanceTable<bool> SentinelStackRoll = new ChanceTable<bool>()
        {
            ( false, 0.75f ),
            ( true,  0.25f ),
        };

        public static bool IsLootTier(int tier) => tier == 9;

        public static void ApplyCombo(WorldObject wo, ShieldPropertyCombo combo, bool addSentinelStack)
        {
            switch (combo)
            {
                case ShieldPropertyCombo.Bulwark:
                    wo.SetProperty(PropertyBool.CriticalBlock, true);
                    break;
                case ShieldPropertyCombo.Tactical:
                    wo.SetProperty(PropertyBool.GlancingBlow, true);
                    break;
                case ShieldPropertyCombo.Balanced:
                    wo.SetProperty(PropertyBool.CriticalBlock, true);
                    wo.SetProperty(PropertyBool.GlancingBlow, true);
                    break;
                case ShieldPropertyCombo.SentinelOnly:
                    wo.SetProperty(PropertyBool.Sentinel, true);
                    break;
            }

            if (combo != ShieldPropertyCombo.SentinelOnly && addSentinelStack)
                wo.SetProperty(PropertyBool.Sentinel, true);
        }

        public static void Roll(WorldObject wo, TreasureDeath profile)
        {
            if (!PropertyRoll.Roll(profile.LootQualityMod))
                return;

            var combo = ComboRoll.Roll(profile.LootQualityMod);
            var addSentinelStack = combo != ShieldPropertyCombo.SentinelOnly && SentinelStackRoll.Roll(profile.LootQualityMod);

            ApplyCombo(wo, combo, addSentinelStack);
        }
    }
}

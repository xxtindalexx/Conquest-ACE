using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class CrossbowWcids
    {
        private static ChanceTable<WeenieClassName> T1_T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.crossbowlight,    0.50f ),
            ( WeenieClassName.crossbowheavy,    0.25f ),
            ( WeenieClassName.crossbowarbalest, 0.25f ),
        };

        private static ChanceTable<WeenieClassName> T5_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.crossbowlight,                     0.20f ),
            ( WeenieClassName.crossbowheavy,                     0.11f ),
            ( WeenieClassName.crossbowarbalest,                  0.11f ),
            ( WeenieClassName.crossbowslashing,                  0.035f ),
            ( WeenieClassName.crossbowpiercing,                  0.035f ),
            ( WeenieClassName.crossbowblunt,                     0.035f ),
            ( WeenieClassName.crossbowacid,                      0.035f ),
            ( WeenieClassName.crossbowfire,                      0.035f ),
            ( WeenieClassName.crossbowfrost,                     0.035f ),
            ( WeenieClassName.crossbowelectric,                  0.035f ),
            ( WeenieClassName.ace31805_slashingcompoundcrossbow, 0.035f ),
            ( WeenieClassName.ace31811_piercingcompoundcrossbow, 0.035f ),
            ( WeenieClassName.ace31807_bluntcompoundcrossbow,    0.035f ),
            ( WeenieClassName.ace31806_acidcompoundcrossbow,     0.035f ),
            ( WeenieClassName.ace31809_firecompoundcrossbow,     0.035f ),
            ( WeenieClassName.ace31810_frostcompoundcrossbow,    0.035f ),
            ( WeenieClassName.ace31808_electriccompoundcrossbow, 0.035f ),
            ( WeenieClassName.conquest_nethercrossbowlight,      0.03f ),
            ( WeenieClassName.conquest_nethercrossbowheavy,      0.03f ),
            ( WeenieClassName.conquest_nethercrossbowarbalest,   0.03f ),
        };

        private static ChanceTable<WeenieClassName> T6_T8_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.crossbowslashing,                  0.06f ),
            ( WeenieClassName.crossbowpiercing,                  0.06f ),
            ( WeenieClassName.crossbowblunt,                     0.055f ),
            ( WeenieClassName.crossbowacid,                      0.055f ),
            ( WeenieClassName.crossbowfire,                      0.055f ),
            ( WeenieClassName.crossbowfrost,                     0.055f ),
            ( WeenieClassName.crossbowelectric,                  0.055f ),
            ( WeenieClassName.ace31805_slashingcompoundcrossbow, 0.06f ),
            ( WeenieClassName.ace31811_piercingcompoundcrossbow, 0.06f ),
            ( WeenieClassName.ace31807_bluntcompoundcrossbow,    0.055f ),
            ( WeenieClassName.ace31806_acidcompoundcrossbow,     0.055f ),
            ( WeenieClassName.ace31809_firecompoundcrossbow,     0.055f ),
            ( WeenieClassName.ace31810_frostcompoundcrossbow,    0.055f ),
            ( WeenieClassName.ace31808_electriccompoundcrossbow, 0.055f ),
            ( WeenieClassName.conquest_nethercrossbowlight,      0.07f ),
            ( WeenieClassName.conquest_nethercrossbowheavy,      0.07f ),
            ( WeenieClassName.conquest_nethercrossbowarbalest,   0.07f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> crossbowTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T5_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
        };

        public static WeenieClassName Roll(int tier)
        {
            return crossbowTiers[tier - 1].Roll();
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static CrossbowWcids()
        {
            foreach (var crossbowTier in crossbowTiers)
            {
                foreach (var entry in crossbowTier)
                    _combined.TryAdd(entry.result, TreasureWeaponType.Crossbow);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}




using System.Collections.Generic;

using ACE.Common;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class FinesseWeaponWcids
    {
        // hammers?

        private static ChanceTable<WeenieClassName> Hatchets = new ChanceTable<WeenieClassName>()
        {
            // finesse - axe
            ( WeenieClassName.axehatchet,                0.35f ),
            ( WeenieClassName.axehatchetacid,            0.13f ),
            ( WeenieClassName.axehatchetelectric,        0.13f ),
            ( WeenieClassName.axehatchetfire,            0.13f ),
            ( WeenieClassName.axehatchetfrost,           0.13f ),
            ( WeenieClassName.conquest_netherhatchet,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Shouonos = new ChanceTable<WeenieClassName>()
        {
            // finesse - axe
            ( WeenieClassName.shouono,                   0.35f ),
            ( WeenieClassName.shouonoacid,               0.13f ),
            ( WeenieClassName.shouonoelectric,           0.13f ),
            ( WeenieClassName.shouonofire,               0.13f ),
            ( WeenieClassName.shouonofrost,              0.13f ),
            ( WeenieClassName.conquest_nethershouono,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Tungis = new ChanceTable<WeenieClassName>()
        {
            // finesse - axe
            ( WeenieClassName.tungi,                     0.35f ),
            ( WeenieClassName.tungiacid,                 0.13f ),
            ( WeenieClassName.tungielectric,             0.13f ),
            ( WeenieClassName.tungifire,                 0.13f ),
            ( WeenieClassName.tungifrost,                0.13f ),
            ( WeenieClassName.conquest_nethertungi,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Knives = new ChanceTable<WeenieClassName>()
        {
            // finesse - dagger (multi-strike)
            ( WeenieClassName.knife,                     0.35f ),
            ( WeenieClassName.knifeacid,                 0.13f ),
            ( WeenieClassName.knifeelectric,             0.13f ),
            ( WeenieClassName.knifefire,                 0.13f ),
            ( WeenieClassName.knifefrost,                0.13f ),
            ( WeenieClassName.conquest_netherknife,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Lancets = new ChanceTable<WeenieClassName>()
        {
            // finesse - dagger (multi-strike)
            ( WeenieClassName.ace31794_lancet,           0.35f ),
            ( WeenieClassName.ace31795_acidlancet,       0.13f ),
            ( WeenieClassName.ace31796_lightninglancet,  0.13f ),
            ( WeenieClassName.ace31797_flaminglancet,    0.13f ),
            ( WeenieClassName.ace31793_frostlancet,      0.13f ),
            ( WeenieClassName.conquest_netherlancet,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Poniards = new ChanceTable<WeenieClassName>()
        {
            // finesse - dagger
            ( WeenieClassName.daggerponiard,             0.35f ),
            ( WeenieClassName.daggerponiardacid,         0.13f ),
            ( WeenieClassName.daggerponiardelectric,     0.13f ),
            ( WeenieClassName.daggerponiardfire,         0.13f ),
            ( WeenieClassName.daggerponiardfrost,        0.13f ),
            ( WeenieClassName.conquest_netherponiard,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> BoardsWithNails = new ChanceTable<WeenieClassName>()
        {
            // finesse - mace
            ( WeenieClassName.ace31774_boardwithnail,           0.35f ),
            ( WeenieClassName.ace31775_acidboardwithnail,       0.13f ),
            ( WeenieClassName.ace31776_electricboardwithnail,   0.13f ),
            ( WeenieClassName.ace31777_fireboardwithnail,       0.13f ),
            ( WeenieClassName.ace31773_frostboardwithnail,      0.13f ),
            ( WeenieClassName.conquest_netherboardwithnail,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Dabus = new ChanceTable<WeenieClassName>()
        {
            // finesse - mace
            ( WeenieClassName.dabus,                     0.35f ),
            ( WeenieClassName.dabusacid,                 0.13f ),
            ( WeenieClassName.dabuselectric,             0.13f ),
            ( WeenieClassName.dabusfire,                 0.13f ),
            ( WeenieClassName.dabusfrost,                0.13f ),
            ( WeenieClassName.conquest_netherdabus,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Jittes = new ChanceTable<WeenieClassName>()
        {
            // finesse - mace
            ( WeenieClassName.jitte,                     0.35f ),
            ( WeenieClassName.jitteacid,                 0.13f ),
            ( WeenieClassName.jitteelectric,             0.13f ),
            ( WeenieClassName.jittefire,                 0.13f ),
            ( WeenieClassName.jittefrost,                0.13f ),
            ( WeenieClassName.conquest_netherjitte,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Tofuns = new ChanceTable<WeenieClassName>()
        {
            // finesse - mace
            ( WeenieClassName.tofun,                     0.35f ),
            ( WeenieClassName.tofunacid,                 0.13f ),
            ( WeenieClassName.tofunelectric,             0.13f ),
            ( WeenieClassName.tofunfire,                 0.13f ),
            ( WeenieClassName.tofunfrost,                0.13f ),
            ( WeenieClassName.conquest_nethertofun,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Budiaqs = new ChanceTable<WeenieClassName>()
        {
            // finesse - spear
            ( WeenieClassName.budiaq,                    0.35f ),
            ( WeenieClassName.budiaqacid,                0.13f ),
            ( WeenieClassName.budiaqelectric,            0.13f ),
            ( WeenieClassName.budiaqfire,                0.13f ),
            ( WeenieClassName.budiaqfrost,               0.13f ),
            ( WeenieClassName.conquest_netherbudiaq,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Naginatas = new ChanceTable<WeenieClassName>()
        {
            // finesse - spear
            ( WeenieClassName.swordstaff,                   0.35f ),
            ( WeenieClassName.swordstaffacid,               0.13f ),
            ( WeenieClassName.swordstaffelectric,           0.13f ),
            ( WeenieClassName.swordstafffire,               0.13f ),
            ( WeenieClassName.swordstafffrost,              0.13f ),
            ( WeenieClassName.conquest_netherswordstaff,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Bastones = new ChanceTable<WeenieClassName>()
        {
            // finesse - staff
            ( WeenieClassName.staffmeleebastone,         0.35f ),
            ( WeenieClassName.staffmeleebastoneacid,     0.13f ),
            ( WeenieClassName.staffmeleebastoneelectric, 0.13f ),
            ( WeenieClassName.staffmeleebastonefire,     0.13f ),
            ( WeenieClassName.staffmeleebastonefrost,    0.13f ),
            ( WeenieClassName.conquest_netherbastone,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Jos = new ChanceTable<WeenieClassName>()
        {
            // finesse - staff
            ( WeenieClassName.jonew,                     0.35f ),
            ( WeenieClassName.joacidnew,                 0.13f ),
            ( WeenieClassName.joelectricnew,             0.13f ),
            ( WeenieClassName.jofirenew,                 0.13f ),
            ( WeenieClassName.jofrostnew,                0.13f ),
            ( WeenieClassName.conquest_netherjo,         0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Sabras = new ChanceTable<WeenieClassName>()
        {
            // finesse - sword
            ( WeenieClassName.swordsabra,                0.35f ),
            ( WeenieClassName.swordsabraacid,            0.13f ),
            ( WeenieClassName.swordsabraelectric,        0.13f ),
            ( WeenieClassName.swordsabrafire,            0.13f ),
            ( WeenieClassName.swordsabrafrost,           0.13f ),
            ( WeenieClassName.conquest_nethersabra,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Scimitars = new ChanceTable<WeenieClassName>()
        {
            // finesse - sword
            ( WeenieClassName.scimitar,                  0.35f ),
            ( WeenieClassName.scimitaracid,              0.13f ),
            ( WeenieClassName.scimitarelectric,          0.13f ),
            ( WeenieClassName.scimitarfire,              0.13f ),
            ( WeenieClassName.scimitarfrost,             0.13f ),
            ( WeenieClassName.conquest_netherscimitar,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> ShortSwords = new ChanceTable<WeenieClassName>()
        {
            // finesse - sword
            ( WeenieClassName.swordshort,                   0.35f ),
            ( WeenieClassName.swordshortacid,               0.13f ),
            ( WeenieClassName.swordshortelectric,           0.13f ),
            ( WeenieClassName.swordshortfire,               0.13f ),
            ( WeenieClassName.swordshortfrost,              0.13f ),
            ( WeenieClassName.conquest_nethershortsword,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Simis = new ChanceTable<WeenieClassName>()
        {
            // finesse - sword
            ( WeenieClassName.simi,                      0.35f ),
            ( WeenieClassName.simiacid,                  0.13f ),
            ( WeenieClassName.simielectric,              0.13f ),
            ( WeenieClassName.simifire,                  0.13f ),
            ( WeenieClassName.simifrost,                 0.13f ),
            ( WeenieClassName.conquest_nethersimi,       0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Rapiers = new ChanceTable<WeenieClassName>()
        {
            // finesse - sword (multi-strike)
            ( WeenieClassName.swordrapier,               0.35f ),
            ( WeenieClassName.ace45104_acidrapier,       0.13f ),
            ( WeenieClassName.ace45105_lightningrapier,  0.13f ),
            ( WeenieClassName.ace45106_flamingrapier,    0.13f ),
            ( WeenieClassName.ace45107_frostrapier,      0.13f ),
            ( WeenieClassName.conquest_netherrapier,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Yaojis = new ChanceTable<WeenieClassName>()
        {
            // finesse - sword
            ( WeenieClassName.yaoji,                     0.35f ),
            ( WeenieClassName.yaojiacid,                 0.13f ),
            ( WeenieClassName.yaojielectric,             0.13f ),
            ( WeenieClassName.yaojifire,                 0.13f ),
            ( WeenieClassName.yaojifrost,                0.13f ),
            ( WeenieClassName.conquest_netheryaoji,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Claws = new ChanceTable<WeenieClassName>()
        {
            // finesse - unarmed
            ( WeenieClassName.ace31784_claw,             0.35f ),
            ( WeenieClassName.ace31785_acidclaw,         0.13f ),
            ( WeenieClassName.ace31786_lightningclaw,    0.13f ),
            ( WeenieClassName.ace31787_flamingclaw,      0.13f ),
            ( WeenieClassName.ace31783_frostclaw,        0.13f ),
            ( WeenieClassName.conquest_netherclaw,       0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> HandWraps = new ChanceTable<WeenieClassName>()
        {
            // finesse - unarmed
            ( WeenieClassName.ace45118_handwraps,           0.35f ),
            ( WeenieClassName.ace45119_acidhandwraps,       0.13f ),
            ( WeenieClassName.ace45120_lightninghandwraps,  0.13f ),
            ( WeenieClassName.ace45121_flaminghandwraps,    0.13f ),
            ( WeenieClassName.ace45122_frosthandwraps,      0.13f ),
            ( WeenieClassName.conquest_netherhandwraps,     0.13f ), // Placeholder - create weenie
        };

        private static readonly List<(ChanceTable<WeenieClassName> table, TreasureWeaponType weaponType)> finesseWeaponsTables = new List<(ChanceTable<WeenieClassName>, TreasureWeaponType)>()
        {
            ( Hatchets,        TreasureWeaponType.Axe ),
            ( Shouonos,        TreasureWeaponType.Axe ),
            ( Tungis,          TreasureWeaponType.Axe ),
            ( Knives,          TreasureWeaponType.DaggerMS ),
            ( Lancets,         TreasureWeaponType.DaggerMS ),
            ( Poniards,        TreasureWeaponType.Dagger ),
            ( BoardsWithNails, TreasureWeaponType.Mace ),
            ( Dabus,           TreasureWeaponType.Mace ),
            ( Jittes,          TreasureWeaponType.MaceJitte ),
            ( Tofuns,          TreasureWeaponType.Mace ),
            ( Budiaqs,         TreasureWeaponType.Spear ),
            ( Naginatas,       TreasureWeaponType.Spear ),
            ( Bastones,        TreasureWeaponType.Staff ),
            ( Jos,             TreasureWeaponType.Staff ),
            ( Sabras,          TreasureWeaponType.Sword ),
            ( Scimitars,       TreasureWeaponType.Sword ),
            ( ShortSwords,     TreasureWeaponType.Sword ),
            ( Simis,           TreasureWeaponType.Sword ),
            ( Rapiers,         TreasureWeaponType.SwordMS ),
            ( Yaojis,          TreasureWeaponType.Sword ),
            ( Claws,           TreasureWeaponType.Unarmed ),
            ( HandWraps,       TreasureWeaponType.Unarmed ),
        };

        public static WeenieClassName Roll(out TreasureWeaponType weaponType)
        {
            // even chance of selecting each weapon table
            var weaponTable = finesseWeaponsTables[ThreadSafeRandom.Next(0, finesseWeaponsTables.Count - 1)];

            weaponType = weaponTable.weaponType;

            return weaponTable.table.Roll();
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static FinesseWeaponWcids()
        {
            foreach (var finesseWeaponsTable in finesseWeaponsTables)
            {
                foreach (var wcid in finesseWeaponsTable.table)
                    _combined.TryAdd(wcid.result, finesseWeaponsTable.weaponType);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}

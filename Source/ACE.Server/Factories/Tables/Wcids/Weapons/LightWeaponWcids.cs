using System.Collections.Generic;

using ACE.Common;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class LightWeaponWcids
    {
        private static ChanceTable<WeenieClassName> Dolabras = new ChanceTable<WeenieClassName>()
        {
            // light - axe
            ( WeenieClassName.axedolabra,                0.35f ),
            ( WeenieClassName.axedolabraacid,            0.13f ),
            ( WeenieClassName.axedolabraelectric,        0.13f ),
            ( WeenieClassName.axedolabrafire,            0.13f ),
            ( WeenieClassName.axedolabrafrost,           0.13f ),
            ( WeenieClassName.conquest_netherdolabra,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> HandAxes = new ChanceTable<WeenieClassName>()
        {
            // light - axe
            ( WeenieClassName.axehand,                   0.35f ),
            ( WeenieClassName.axehandacid,               0.13f ),
            ( WeenieClassName.axehandelectric,           0.13f ),
            ( WeenieClassName.axehandfire,               0.13f ),
            ( WeenieClassName.axehandfrost,              0.13f ),
            ( WeenieClassName.conquest_netherhandaxe,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Onos = new ChanceTable<WeenieClassName>()
        {
            // light - axe
            ( WeenieClassName.ono,                       0.35f ),
            ( WeenieClassName.onoacid,                   0.13f ),
            ( WeenieClassName.onoelectric,               0.13f ),
            ( WeenieClassName.onofire,                   0.13f ),
            ( WeenieClassName.onofrost,                  0.13f ),
            ( WeenieClassName.conquest_netherono,        0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> WarHammers = new ChanceTable<WeenieClassName>()
        {
            // light - axe
            ( WeenieClassName.warhammer,                 0.35f ),
            ( WeenieClassName.warhammeracid,             0.13f ),
            ( WeenieClassName.warhammerelectric,         0.13f ),
            ( WeenieClassName.warhammerfire,             0.13f ),
            ( WeenieClassName.warhammerfrost,            0.13f ),
            ( WeenieClassName.conquest_netherwarhammer,  0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Daggers = new ChanceTable<WeenieClassName>()
        {
            // light - dagger (multi-strike)
            ( WeenieClassName.dagger,                       0.35f ),
            ( WeenieClassName.daggeracid,                   0.13f ),
            ( WeenieClassName.daggerelectric,               0.13f ),
            ( WeenieClassName.daggerfire,                   0.13f ),
            ( WeenieClassName.daggerfrost,                  0.13f ),
            ( WeenieClassName.conquest_netherlightdagger,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Khanjars = new ChanceTable<WeenieClassName>()
        {
            // light - dagger
            ( WeenieClassName.khanjar,                      0.35f ),
            ( WeenieClassName.khanjaracid,                  0.13f ),
            ( WeenieClassName.khanjarelectric,              0.13f ),
            ( WeenieClassName.khanjarfire,                  0.13f ),
            ( WeenieClassName.khanjarfrost,                 0.13f ),
            ( WeenieClassName.conquest_netherkhanjar,       0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Clubs = new ChanceTable<WeenieClassName>()
        {
            // light - mace
            ( WeenieClassName.club,                         0.35f ),
            ( WeenieClassName.clubacid,                     0.13f ),
            ( WeenieClassName.clubelectric,                 0.13f ),
            ( WeenieClassName.clubfire,                     0.13f ),
            ( WeenieClassName.clubfrost,                    0.13f ),
            ( WeenieClassName.conquest_netherlightclub,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Kasrullahs = new ChanceTable<WeenieClassName>()
        {
            // light - mace
            ( WeenieClassName.kasrullah,                    0.35f ),
            ( WeenieClassName.kasrullahacid,                0.13f ),
            ( WeenieClassName.kasrullahelectric,            0.13f ),
            ( WeenieClassName.kasrullahfire,                0.13f ),
            ( WeenieClassName.kasrullahfrost,               0.13f ),
            ( WeenieClassName.conquest_netherkasrullah,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> SpikedClubs = new ChanceTable<WeenieClassName>()
        {
            // light - mace
            ( WeenieClassName.clubspiked,                   0.35f ),
            ( WeenieClassName.clubspikedacid,               0.13f ),
            ( WeenieClassName.clubspikedelectric,           0.13f ),
            ( WeenieClassName.clubspikedfire,               0.13f ),
            ( WeenieClassName.clubspikedfrost,              0.13f ),
            ( WeenieClassName.conquest_netherspikedclub,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Spears = new ChanceTable<WeenieClassName>()
        {
            // light - spear
            ( WeenieClassName.spear,                        0.35f ),
            ( WeenieClassName.spearacid,                    0.13f ),
            ( WeenieClassName.spearelectric,                0.13f ),
            ( WeenieClassName.spearflame,                   0.13f ),
            ( WeenieClassName.spearfrost,                   0.13f ),
            ( WeenieClassName.conquest_netherlightspear,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Yaris = new ChanceTable<WeenieClassName>()
        {
            // light - spear
            ( WeenieClassName.yari,                         0.35f ),
            ( WeenieClassName.yariacid,                     0.13f ),
            ( WeenieClassName.yarielectric,                 0.13f ),
            ( WeenieClassName.yarifire,                     0.13f ),
            ( WeenieClassName.yarifrost,                    0.13f ),
            ( WeenieClassName.conquest_netheryari,          0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> QuarterStaffs = new ChanceTable<WeenieClassName>()
        {
            // light - staff
            ( WeenieClassName.quarterstaffnew,              0.35f ),
            ( WeenieClassName.quarterstaffacidnew,          0.13f ),
            ( WeenieClassName.quarterstaffelectricnew,      0.13f ),
            ( WeenieClassName.quarterstaffflamenew,         0.13f ),
            ( WeenieClassName.quarterstafffrostnew,         0.13f ),
            ( WeenieClassName.conquest_netherquarterstaff,  0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> BroadSwords = new ChanceTable<WeenieClassName>()
        {
            // light - sword
            ( WeenieClassName.swordbroad,                   0.35f ),
            ( WeenieClassName.swordbroadacid,               0.13f ),
            ( WeenieClassName.swordbroadelectric,           0.13f ),
            ( WeenieClassName.swordbroadfire,               0.13f ),
            ( WeenieClassName.swordbroadfrost,              0.13f ),
            ( WeenieClassName.conquest_netherbroadsword,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> DericostBlades = new ChanceTable<WeenieClassName>()
        {
            // light - sword
            ( WeenieClassName.ace31759_dericostblade,           0.35f ),
            ( WeenieClassName.ace31760_aciddericostblade,       0.13f ),
            ( WeenieClassName.ace31761_lightningdericostblade,  0.13f ),
            ( WeenieClassName.ace31762_flamingdericostblade,    0.13f ),
            ( WeenieClassName.ace31758_frostdericostblade,      0.13f ),
            ( WeenieClassName.conquest_netherdericostblade,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> EpeeSwords = new ChanceTable<WeenieClassName>()
        {
            // light - Epee - MS
            ( WeenieClassName.ace45099_epee,                    0.35f ),
            ( WeenieClassName.ace45100_acidepee,                0.13f ),
            ( WeenieClassName.ace45101_lightningepee,           0.13f ),
            ( WeenieClassName.ace45102_flamingepee,             0.13f ),
            ( WeenieClassName.ace45103_frostepee,               0.13f ),
            ( WeenieClassName.conquest_netherepee,              0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Kaskaras = new ChanceTable<WeenieClassName>()
        {
            // light - sword
            ( WeenieClassName.kaskara,                      0.35f ),
            ( WeenieClassName.kaskaraacid,                  0.13f ),
            ( WeenieClassName.kaskaraelectric,              0.13f ),
            ( WeenieClassName.kaskarafire,                  0.13f ),
            ( WeenieClassName.kaskarafrost,                 0.13f ),
            ( WeenieClassName.conquest_netherkaskara,       0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Shamshirs = new ChanceTable<WeenieClassName>()
        {
            // light - sword
            ( WeenieClassName.shamshir,                     0.35f ),
            ( WeenieClassName.shamshiracid,                 0.13f ),
            ( WeenieClassName.shamshirelectric,             0.13f ),
            ( WeenieClassName.shamshirfire,                 0.13f ),
            ( WeenieClassName.shamshirfrost,                0.13f ),
            ( WeenieClassName.conquest_nethershamshir,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Spadas = new ChanceTable<WeenieClassName>()
        {
            // light - sword
            ( WeenieClassName.swordspada,                   0.35f ),
            ( WeenieClassName.swordspadaacid,               0.13f ),
            ( WeenieClassName.swordspadaelectric,           0.13f ),
            ( WeenieClassName.swordspadafire,               0.13f ),
            ( WeenieClassName.swordspadafrost,              0.13f ),
            ( WeenieClassName.conquest_netherspada,         0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Katars = new ChanceTable<WeenieClassName>()
        {
            // light - unarmed
            ( WeenieClassName.katar,                    0.35f ),
            ( WeenieClassName.kataracid,                0.13f ),
            ( WeenieClassName.katarelectric,            0.13f ),
            ( WeenieClassName.katarfire,                0.13f ),
            ( WeenieClassName.katarfrost,               0.13f ),
            ( WeenieClassName.conquest_netherkatar,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Knuckles = new ChanceTable<WeenieClassName>()
        {
            // light - unarmed
            ( WeenieClassName.knuckles,                 0.35f ),
            ( WeenieClassName.knucklesacid,             0.13f ),
            ( WeenieClassName.knuckleselectric,         0.13f ),
            ( WeenieClassName.knucklesfire,             0.13f ),
            ( WeenieClassName.knucklesfrost,            0.13f ),
            ( WeenieClassName.conquest_netherknuckles,  0.13f ), // Placeholder - create weenie
        };

        private static readonly List<(ChanceTable<WeenieClassName> table, TreasureWeaponType weaponType)> lightWeaponsTables = new List<(ChanceTable<WeenieClassName>, TreasureWeaponType)>()
        {
            ( Dolabras,       TreasureWeaponType.Axe ),
            ( HandAxes,       TreasureWeaponType.Axe ),
            ( Onos,           TreasureWeaponType.Axe ),
            ( WarHammers,     TreasureWeaponType.Axe ),
            ( Daggers,        TreasureWeaponType.DaggerMS ),
            ( Khanjars,       TreasureWeaponType.Dagger ),
            ( Clubs,          TreasureWeaponType.Mace ),
            ( Kasrullahs,     TreasureWeaponType.Mace ),
            ( SpikedClubs,    TreasureWeaponType.Mace ),
            ( Spears,         TreasureWeaponType.Spear ),
            ( Yaris,          TreasureWeaponType.Spear ),
            ( QuarterStaffs,  TreasureWeaponType.Staff ),
            ( BroadSwords,    TreasureWeaponType.Sword ),
            ( DericostBlades, TreasureWeaponType.Sword ),
            ( Kaskaras,       TreasureWeaponType.Sword ),
            ( Shamshirs,      TreasureWeaponType.Sword ),
            ( Spadas,         TreasureWeaponType.Sword ),
            ( EpeeSwords,     TreasureWeaponType.SwordMS ),
            ( Katars,         TreasureWeaponType.Unarmed ),
            ( Knuckles,       TreasureWeaponType.Unarmed ),
        };

        public static WeenieClassName Roll(out TreasureWeaponType weaponType)
        {
            // even chance of selecting each weapon table
            var weaponTable = lightWeaponsTables[ThreadSafeRandom.Next(0, lightWeaponsTables.Count - 1)];

            weaponType = weaponTable.weaponType;

            return weaponTable.table.Roll();
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static LightWeaponWcids()
        {
            foreach (var lightWeaponsTable in lightWeaponsTables)
            {
                foreach (var wcid in lightWeaponsTable.table)
                    _combined.TryAdd(wcid.result, lightWeaponsTable.weaponType);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}

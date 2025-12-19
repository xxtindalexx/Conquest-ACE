using System.Collections.Generic;

using ACE.Common;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class HeavyWeaponWcids
    {
        private static ChanceTable<WeenieClassName> BattleAxes = new ChanceTable<WeenieClassName>()
        {
            // heavy - axe
            ( WeenieClassName.axebattle,                     0.35f ),
            ( WeenieClassName.axebattleacid,                 0.13f ),
            ( WeenieClassName.axebattleelectric,             0.13f ),
            ( WeenieClassName.axebattlefire,                 0.13f ),
            ( WeenieClassName.axebattlefrost,                0.13f ),
            ( WeenieClassName.conquest_netherbattleaxe,      0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> LugianHammers = new ChanceTable<WeenieClassName>()
        {
            // heavy - axe
            ( WeenieClassName.ace31764_lugianhammer,          0.35f ),
            ( WeenieClassName.ace31765_acidlugianhammer,      0.13f ),
            ( WeenieClassName.ace31766_lightninglugianhammer, 0.13f ),
            ( WeenieClassName.ace31767_flaminglugianhammer,   0.13f ),
            ( WeenieClassName.ace31763_frostlugianhammer,     0.13f ),
            ( WeenieClassName.conquest_netherlugianhammer,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Silifis = new ChanceTable<WeenieClassName>()
        {
            // heavy - axe
            ( WeenieClassName.silifi,                  0.35f ),
            ( WeenieClassName.silifiacid,              0.13f ),
            ( WeenieClassName.silifielectric,          0.13f ),
            ( WeenieClassName.silififire,              0.13f ),
            ( WeenieClassName.silififrost,             0.13f ),
            ( WeenieClassName.conquest_nethersilifi,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> WarAxes = new ChanceTable<WeenieClassName>()
        {
            // heavy - axe
            ( WeenieClassName.ace31769_waraxe,          0.35f ),
            ( WeenieClassName.ace31770_acidwaraxe,      0.13f ),
            ( WeenieClassName.ace31771_lightningwaraxe, 0.13f ),
            ( WeenieClassName.ace31772_flamingwaraxe,   0.13f ),
            ( WeenieClassName.ace31768_frostwaraxe,     0.13f ),
            ( WeenieClassName.conquest_netherwaraxe,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Dirks = new ChanceTable<WeenieClassName>()
        {
            // heavy - dagger
            ( WeenieClassName.dirk,                  0.35f ),
            ( WeenieClassName.dirkacid,              0.13f ),
            ( WeenieClassName.dirkelectric,          0.13f ),
            ( WeenieClassName.dirkfire,              0.13f ),
            ( WeenieClassName.dirkfrost,             0.13f ),
            ( WeenieClassName.conquest_netherdirk,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Jambiyas = new ChanceTable<WeenieClassName>()
        {
            // heavy - dagger (multi-strike)
            ( WeenieClassName.jambiya,                  0.35f ),
            ( WeenieClassName.jambiyaacid,              0.13f ),
            ( WeenieClassName.jambiyaelectric,          0.13f ),
            ( WeenieClassName.jambiyafire,              0.13f ),
            ( WeenieClassName.jambiyafrost,             0.13f ),
            ( WeenieClassName.conquest_netherjambiya,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Stilettos = new ChanceTable<WeenieClassName>()
        {
            // heavy - dagger (multi-strike)
            ( WeenieClassName.daggerstiletto,              0.35f ),
            ( WeenieClassName.daggerstilettoacid,          0.13f ),
            ( WeenieClassName.daggerstilettoelectric,      0.13f ),
            ( WeenieClassName.daggerstilettofire,          0.13f ),
            ( WeenieClassName.daggerstilettofrost,         0.13f ),
            ( WeenieClassName.conquest_netherstiletto,     0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> FlangedMaces = new ChanceTable<WeenieClassName>()
        {
            // heavy - mace
            ( WeenieClassName.maceflanged,                  0.35f ),
            ( WeenieClassName.maceflangedacid,              0.13f ),
            ( WeenieClassName.maceflangedelectric,          0.13f ),
            ( WeenieClassName.maceflangedfire,              0.13f ),
            ( WeenieClassName.maceflangedfrost,             0.13f ),
            ( WeenieClassName.conquest_netherflangedmace,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Maces = new ChanceTable<WeenieClassName>()
        {
            // heavy - mace
            ( WeenieClassName.mace,                  0.35f ),
            ( WeenieClassName.maceacid,              0.13f ),
            ( WeenieClassName.maceelectric,          0.13f ),
            ( WeenieClassName.macefire,              0.13f ),
            ( WeenieClassName.macefrost,             0.13f ),
            ( WeenieClassName.conquest_nethermace,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Mazules = new ChanceTable<WeenieClassName>()
        {
            // heavy - mace
            ( WeenieClassName.macemazule,              0.35f ),
            ( WeenieClassName.macemazuleacid,          0.13f ),
            ( WeenieClassName.macemazuleelectric,      0.13f ),
            ( WeenieClassName.macemazulefire,          0.13f ),
            ( WeenieClassName.macemazulefrost,         0.13f ),
            ( WeenieClassName.conquest_nethermazule,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> MorningStars = new ChanceTable<WeenieClassName>()
        {
            // heavy - mace
            ( WeenieClassName.morningstar,                  0.35f ),
            ( WeenieClassName.morningstaracid,              0.13f ),
            ( WeenieClassName.morningstarelectric,          0.13f ),
            ( WeenieClassName.morningstarfire,              0.13f ),
            ( WeenieClassName.morningstarfrost,             0.13f ),
            ( WeenieClassName.conquest_nethermorningstar,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Partizans = new ChanceTable<WeenieClassName>()
        {
            // heavy - spear
            ( WeenieClassName.spearpartizan,              0.35f ),
            ( WeenieClassName.spearpartizanacid,          0.13f ),
            ( WeenieClassName.spearpartizanelectric,      0.13f ),
            ( WeenieClassName.spearpartizanfire,          0.13f ),
            ( WeenieClassName.spearpartizanfrost,         0.13f ),
            ( WeenieClassName.conquest_netherpartizan,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> SpineGlaives = new ChanceTable<WeenieClassName>()
        {
            // heavy - spear
            ( WeenieClassName.ace31779_spineglaive,         0.35f ),
            ( WeenieClassName.ace31780_acidspineglaive,     0.13f ),
            ( WeenieClassName.ace31781_electricspineglaive, 0.13f ),
            ( WeenieClassName.ace31782_firespineglaive,     0.13f ),
            ( WeenieClassName.ace31778_frostspineglaive,    0.13f ),
            ( WeenieClassName.conquest_netherspineglaive,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Tridents = new ChanceTable<WeenieClassName>()
        {
            // heavy - spear
            ( WeenieClassName.trident,                  0.35f ),
            ( WeenieClassName.tridentacid,              0.13f ),
            ( WeenieClassName.tridentelectric,          0.13f ),
            ( WeenieClassName.tridentfire,              0.13f ),
            ( WeenieClassName.tridentfrost,             0.13f ),
            ( WeenieClassName.conquest_nethertrident,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Nabuts = new ChanceTable<WeenieClassName>()
        {
            // heavy - staff
            ( WeenieClassName.nabutnew,              0.35f ),
            ( WeenieClassName.nabutacidnew,          0.13f ),
            ( WeenieClassName.nabutelectricnew,      0.13f ),
            ( WeenieClassName.nabutfirenew,          0.13f ),
            ( WeenieClassName.nabutfrostnew,         0.13f ),
            ( WeenieClassName.conquest_nethernabut,  0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Sticks = new ChanceTable<WeenieClassName>()
        {
            // heavy - staff
            ( WeenieClassName.ace31788_stick,          0.35f ),
            ( WeenieClassName.ace31789_acidstick,      0.13f ),
            ( WeenieClassName.ace31790_lightningstick, 0.13f ),
            ( WeenieClassName.ace31791_flamingstick,   0.13f ),
            ( WeenieClassName.ace31792_froststick,     0.13f ),
            ( WeenieClassName.conquest_netherstick,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Flamberges = new ChanceTable<WeenieClassName>()
        {
            // heavy - sword
            ( WeenieClassName.swordflamberge,              0.35f ),
            ( WeenieClassName.swordflambergeacid,          0.13f ),
            ( WeenieClassName.swordflambergeelectric,      0.13f ),
            ( WeenieClassName.swordflambergefire,          0.13f ),
            ( WeenieClassName.swordflambergefrost,         0.13f ),
            ( WeenieClassName.conquest_netherflamberge,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Kens = new ChanceTable<WeenieClassName>()
        {
            // heavy - sword
            ( WeenieClassName.ken,                  0.35f ),
            ( WeenieClassName.kenacid,              0.13f ),
            ( WeenieClassName.kenelectric,          0.13f ),
            ( WeenieClassName.kenfire,              0.13f ),
            ( WeenieClassName.kenfrost,             0.13f ),
            ( WeenieClassName.conquest_netherken,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> LongSwords = new ChanceTable<WeenieClassName>()
        {
            // heavy - sword
            ( WeenieClassName.swordlong,                  0.35f ),
            ( WeenieClassName.swordlongacid,              0.13f ),
            ( WeenieClassName.swordlongelectric,          0.13f ),
            ( WeenieClassName.swordlongfire,              0.13f ),
            ( WeenieClassName.swordlongfrost,             0.13f ),
            ( WeenieClassName.conquest_netherlongsword,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Schlagers = new ChanceTable<WeenieClassName>()
        {
            // heavy - sword
            ( WeenieClassName.ace45108_schlager,          0.35f ),
            ( WeenieClassName.ace45109_acidschlager,      0.13f ),
            ( WeenieClassName.ace45110_lightningschlager, 0.13f ),
            ( WeenieClassName.ace45111_flamingschlager,   0.13f ),
            ( WeenieClassName.ace45112_frostschlager,     0.13f ),
            ( WeenieClassName.conquest_netherschlager,    0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Tachis = new ChanceTable<WeenieClassName>()
        {
            // heavy - sword
            ( WeenieClassName.tachi,                  0.35f ),
            ( WeenieClassName.tachiacid,              0.13f ),
            ( WeenieClassName.tachielectric,          0.13f ),
            ( WeenieClassName.tachifire,              0.13f ),
            ( WeenieClassName.tachifrost,             0.13f ),
            ( WeenieClassName.conquest_nethertachi,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Takubas = new ChanceTable<WeenieClassName>()
        {
            // heavy - sword
            ( WeenieClassName.takuba,                  0.35f ),
            ( WeenieClassName.takubaacid,              0.13f ),
            ( WeenieClassName.takubaelectric,          0.13f ),
            ( WeenieClassName.takubafire,              0.13f ),
            ( WeenieClassName.takubafrost,             0.13f ),
            ( WeenieClassName.conquest_nethertakuba,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Cestus = new ChanceTable<WeenieClassName>()
        {
            // heavy - unarmed
            ( WeenieClassName.cestus,                  0.35f ),
            ( WeenieClassName.cestusacid,              0.13f ),
            ( WeenieClassName.cestuselectric,          0.13f ),
            ( WeenieClassName.cestusfire,              0.13f ),
            ( WeenieClassName.cestusfrost,             0.13f ),
            ( WeenieClassName.conquest_nethercestus,   0.13f ), // Placeholder - create weenie
        };

        private static ChanceTable<WeenieClassName> Nekodes = new ChanceTable<WeenieClassName>()
        {
            // heavy - unarmed
            ( WeenieClassName.nekode,                  0.35f ),
            ( WeenieClassName.nekodeacid,              0.13f ),
            ( WeenieClassName.nekodeelectric,          0.13f ),
            ( WeenieClassName.nekodefire,              0.13f ),
            ( WeenieClassName.nekodefrost,             0.13f ),
            ( WeenieClassName.conquest_nethernekode,   0.13f ), // Placeholder - create weenie
        };

        private static readonly List<(ChanceTable<WeenieClassName> table, TreasureWeaponType weaponType)> heavyWeaponsTables = new List<(ChanceTable<WeenieClassName>, TreasureWeaponType)>()
        {
            ( BattleAxes,    TreasureWeaponType.Axe ),
            ( LugianHammers, TreasureWeaponType.Axe ),
            ( Silifis,       TreasureWeaponType.Axe ),
            ( WarAxes,       TreasureWeaponType.Axe ),
            ( Dirks,         TreasureWeaponType.Dagger ),
            ( Jambiyas,      TreasureWeaponType.DaggerMS ),
            ( Stilettos,     TreasureWeaponType.DaggerMS ),
            ( FlangedMaces,  TreasureWeaponType.Mace ),
            ( Maces,         TreasureWeaponType.Mace ),
            ( Mazules,       TreasureWeaponType.Mace ),
            ( MorningStars,  TreasureWeaponType.Mace ),
            ( Partizans,     TreasureWeaponType.Spear ),
            ( SpineGlaives,  TreasureWeaponType.Spear ),
            ( Tridents,      TreasureWeaponType.Spear ),
            ( Nabuts,        TreasureWeaponType.Staff ),
            ( Sticks,        TreasureWeaponType.Staff ),
            ( Flamberges,    TreasureWeaponType.Sword ),
            ( Kens,          TreasureWeaponType.Sword ),
            ( LongSwords,    TreasureWeaponType.Sword ),
            ( Schlagers,     TreasureWeaponType.SwordMS ),
            ( Tachis,        TreasureWeaponType.Sword ),
            ( Takubas,       TreasureWeaponType.Sword ),
            ( Cestus,        TreasureWeaponType.Unarmed ),
            ( Nekodes,       TreasureWeaponType.Unarmed ),
        };

        public static WeenieClassName Roll(out TreasureWeaponType weaponType)
        {
            // even chance of selecting each weapon table
            var weaponTable = heavyWeaponsTables[ThreadSafeRandom.Next(0, heavyWeaponsTables.Count - 1)];

            weaponType = weaponTable.weaponType;

            return weaponTable.table.Roll();
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static HeavyWeaponWcids()
        {
            foreach (var heavyWeaponsTable in heavyWeaponsTables)
            {
                foreach (var wcid in heavyWeaponsTable.table)
                    _combined.TryAdd(wcid.result, heavyWeaponsTable.weaponType);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}

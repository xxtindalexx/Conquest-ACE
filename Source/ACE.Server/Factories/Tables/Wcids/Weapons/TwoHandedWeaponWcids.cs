using System.Collections.Generic;

using ACE.Common;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class TwoHandedWeaponWcids
    {
        private static ChanceTable<WeenieClassName> GreatAxes = new ChanceTable<WeenieClassName>()
        {
            // two-handed - axe$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> GreatStarMaces = new ChanceTable<WeenieClassName>()
        {
            // two-handed - mace$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> KhandaHandledMaces = new ChanceTable<WeenieClassName>()
        {
            // two-handed - mace$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Quadrelles = new ChanceTable<WeenieClassName>()
        {
            // two-handed - mace$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Tetsubos = new ChanceTable<WeenieClassName>()
        {
            // two-handed - mace$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Assagais = new ChanceTable<WeenieClassName>()
        {
            // two-handed - spear$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Corsecas = new ChanceTable<WeenieClassName>()
        {
            // two-handed - spear$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> MagariYaris = new ChanceTable<WeenieClassName>()
        {
            // two-handed - spear$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Pikes = new ChanceTable<WeenieClassName>()
        {
            // two-handed - spear$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Nodachis = new ChanceTable<WeenieClassName>()
        {
            // two-handed - sword$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Shashqas = new ChanceTable<WeenieClassName>()
        {
            // two-handed - sword$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static ChanceTable<WeenieClassName> Spadones = new ChanceTable<WeenieClassName>()
        {
            // two-handed - sword$10.35f ),$10.13f ),$10.13f ),$10.13f ),$10.13f ),
        };

        private static readonly List<(ChanceTable<WeenieClassName> table, TreasureWeaponType weaponType)> twoHandedWeaponTables = new List<(ChanceTable<WeenieClassName>, TreasureWeaponType)>()
        {
            ( GreatAxes,          TreasureWeaponType.TwoHandedAxe ),
            ( GreatStarMaces,     TreasureWeaponType.TwoHandedMace ),
            ( KhandaHandledMaces, TreasureWeaponType.TwoHandedMace ),
            ( Quadrelles,         TreasureWeaponType.TwoHandedMace ),
            ( Tetsubos,           TreasureWeaponType.TwoHandedMace ),
            ( Assagais,           TreasureWeaponType.TwoHandedSpear ),
            ( Corsecas,           TreasureWeaponType.TwoHandedSpear ),
            ( MagariYaris,        TreasureWeaponType.TwoHandedSpear ),
            ( Pikes,              TreasureWeaponType.TwoHandedSpear ),
            ( Nodachis,           TreasureWeaponType.TwoHandedSword ),
            ( Shashqas,           TreasureWeaponType.TwoHandedSword ),
            ( Spadones,           TreasureWeaponType.TwoHandedSword ),
        };

        public static WeenieClassName Roll(out TreasureWeaponType weaponType)
        {
            // even chance of selecting each weapon table
            var weaponTable = twoHandedWeaponTables[ThreadSafeRandom.Next(0, twoHandedWeaponTables.Count - 1)];

            weaponType = weaponTable.weaponType;

            return weaponTable.table.Roll();
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static TwoHandedWeaponWcids()
        {
            foreach (var twoHandedWeaponsTable in twoHandedWeaponTables)
            {
                foreach (var wcid in twoHandedWeaponsTable.table)
                    _combined.TryAdd(wcid.result, twoHandedWeaponsTable.weaponType);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}


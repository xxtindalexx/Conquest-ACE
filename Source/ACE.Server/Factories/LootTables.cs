using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Factories.Tables;

namespace ACE.Server.Factories
{
    public static class LootTables
    {
        public static int[][] DefaultMaterial { get; } =
        {
            new int[] { (int)MaterialType.Copper, (int)MaterialType.Bronze, (int)MaterialType.Iron, (int)MaterialType.Steel, (int)MaterialType.Silver },            // Armor
            new int[] { (int)MaterialType.Oak, (int)MaterialType.Teak, (int)MaterialType.Mahogany, (int)MaterialType.Pine, (int)MaterialType.Ebony },               // Missile
            new int[] { (int)MaterialType.Brass, (int)MaterialType.Ivory, (int)MaterialType.Gold, (int)MaterialType.Steel, (int)MaterialType.Diamond },             // Melee
            new int[] { (int)MaterialType.RedGarnet, (int)MaterialType.Jet, (int)MaterialType.BlackOpal, (int)MaterialType.FireOpal, (int)MaterialType.Emerald },   // Caster
            new int[] { (int)MaterialType.Granite, (int)MaterialType.Ceramic, (int)MaterialType.Porcelain, (int)MaterialType.Alabaster, (int)MaterialType.Marble }, // Dinnerware
            new int[] { (int)MaterialType.Linen, (int)MaterialType.Wool, (int)MaterialType.Velvet, (int)MaterialType.Satin, (int)MaterialType.Silk }                // Clothes
        };

        // for logging epic/legendary drops
        public static HashSet<int> MinorCantrips;
        public static HashSet<int> MajorCantrips;
        public static HashSet<int> EpicCantrips;
        public static HashSet<int> LegendaryCantrips;

        private static List<SpellId[][]> cantripTables { get; } = new List<SpellId[][]>()
        {
            ArmorCantrips.Table,
            JewelryCantrips.Table,
            WandCantrips.Table,
            MeleeCantrips.Table,
            MissileCantrips.Table
        };

        static LootTables()
        {
            BuildCantripsTable(ref MinorCantrips, 0);
            BuildCantripsTable(ref MajorCantrips, 1);
            BuildCantripsTable(ref EpicCantrips, 2);
            BuildCantripsTable(ref LegendaryCantrips, 3);
        }

        private static void BuildCantripsTable(ref HashSet<int> table, int tier)
        {
            table = new HashSet<int>();

            foreach (var cantripTable in cantripTables)
            {
                foreach (var category in cantripTable)
                    table.Add((int)category[tier]);
            }
        }


        // CONQUEST: Weapon element matrices for morph gem randomization
        // Format: Each row is a weapon type, each column is an element variant
        // Elements: Base, Frost, Fire, Acid, Electric, Nether (5 elements)

        public static readonly int[][] HeavyWeaponsMatrix =
        {
            new int[] { 301, 3750, 3751, 3752, 3753, 63370000 },       //  0 - Battle Axe
            new int[] { 344, 3865, 3866, 3867, 3868, 63370002 },       //  1 - Silifi
            new int[] { 31769, 31770, 31771, 31772, 31768, 63370003 }, //  2 - War Axe
            new int[] { 31764, 31765, 31766, 31767, 31763, 63370001 }, //  3 - Lugian Hammer
            new int[] { 22440, 22441, 22442, 22443, 22444, 63370004 }, //  4 - Dirk
            new int[] { 30601, 30602, 30603, 30604, 30605, 63370006 }, //  5 - Stiletto (MS)
            new int[] { 319, 3794, 3795, 3796, 3797, 63370005 },       //  6 - Jambiya (MS)
            new int[] { 30586, 30587, 30588, 30589, 30590, 63370007 }, //  7 - Flanged Mace
            new int[] { 331, 3834, 3835, 3836, 3837, 63370008 },       //  8 - Mace
            new int[] { 30581, 30582, 30583, 30584, 30585, 63370009 }, //  9 - Mazule
            new int[] { 332, 3937, 3938, 3939, 3940, 63370010 },       // 10 - Morning Star
            new int[] { 31778, 31779, 31780, 31781, 31782, 63370012 }, // 11 - Spine Glaive
            new int[] { 30591, 30592, 30593, 30594, 30595, 63370011 }, // 12 - Partizan
            new int[] { 7772, 7791, 7792, 7793, 7794, 63370013 },      // 13 - Trident
            new int[] { 333, 22159, 22160, 22161, 22162, 63370014 },   // 14 - Nabut
            new int[] { 31788, 31789, 31790, 31791, 31792, 63370015 }, // 15 - Stick
            new int[] { 30576, 30577, 30578, 30579, 30580, 63370016 }, // 16 - Flamberge
            new int[] { 327, 3822, 3823, 3824, 3825, 63370017 },       // 17 - Ken
            new int[] { 351, 3881, 3882, 3883, 3884, 63370018 },       // 18 - Long Sword
            new int[] { 353, 3889, 3890, 3891, 3892, 63370020 },       // 19 - Tachi
            new int[] { 354, 3893, 3894, 3895, 3896, 63370021 },       // 20 - Takuba
            new int[] { 45108, 45109, 45110, 45111, 45112, 63370019 }, // 21 - Schlager (MS)
            new int[] { 4190, 4191, 4192, 4193, 4194, 63370022 },      // 22 - Cestus
            new int[] { 4195, 4196, 4197, 4198, 4199, 63370023 },      // 23 - Nekode
        };

        public static readonly int[][] LightWeaponsMatrix =
        {
            new int[] { 30561, 30562, 30563, 30564, 30565, 63370024 }, //  0 - Dolabra
            new int[] { 303, 3754, 3755, 3756, 3757, 63370025 },       //  1 - Hand Axe
            new int[] { 336, 3842, 3843, 3844, 3845, 63370026 },       //  2 - Ono
            new int[] { 359, 3905, 3906, 3907, 3908, 63370027 },       //  3 - War Hammer
            new int[] { 314, 3778, 3779, 3780, 3781, 63370028 },       //  4 - Dagger (MS)
            new int[] { 328, 3826, 3827, 3828, 3829, 63370029 },       //  5 - Khanjar
            new int[] { 309, 3766, 3767, 3768, 3769, 63370030 },       //  6 - Club
            new int[] { 325, 3814, 3815, 3816, 3817, 63370031 },       //  7 - Kasrullah
            new int[] { 7768, 7787, 7788, 7789, 7790, 63370032 },      //  8 - Spiked Club
            new int[] { 348, 3873, 3874, 3875, 3876, 63370033 },       //  9 - Spear
            new int[] { 362, 3913, 3914, 3915, 3916, 63370034 },       // 10 - Yari
            new int[] { 338, 22164, 22165, 22166, 22167, 63370035 },   // 11 - Quarter Staff
            new int[] { 350, 3877, 3878, 3879, 3880, 63370036 },       // 12 - Broad Sword
            new int[] { 31759, 31760, 31761, 31762, 31758, 63370037 }, // 13 - Dericost Blade
            new int[] { 45099, 45100, 45101, 45102, 45103, 63370041 }, // 14 - Epee (MS)
            new int[] { 324, 3810, 3811, 3812, 3813, 63370038 },       // 15 - Kaskara
            new int[] { 30571, 30572, 30573, 30574, 30575, 63370040 }, // 16 - Spada
            new int[] { 340, 3853, 3854, 3855, 3856, 63370039 },       // 17 - Shamshir
            new int[] { 30611, 30612, 30613, 30614, 30615, 63370043 }, // 18 - Knuckles
            new int[] { 326, 3818, 3819, 3820, 3821, 63370042 }        // 19 - Katar
        };

        public static readonly int[][] FinesseWeaponsMatrix =
        {
            new int[] { 45113, 45114, 45115, 45116, 45117, 63370097 },        //  0 - Hammer
            new int[] { 30556, 30557, 30558, 30559, 30560, 63370044 }, //  1 - Hatchet
            new int[] { 342, 3857, 3858, 3859, 3860, 63370045 },       //  2 - Shou-ono
            new int[] { 357, 3901, 3902, 3903, 3904, 63370046 },       //  3 - Tungi
            new int[] { 329, 3830, 3831, 3832, 3833, 63370047 },       //  4 - Knife (MS)
            new int[] { 31794, 31795, 31796, 31797, 31793, 63370048 }, //  5 - Lancet (MS)
            new int[] { 30596, 30597, 30598, 30599, 30600, 63370049 }, //  6 - Poniard
            new int[] { 31774, 31775, 31776, 31777, 31773, 63370050 }, //  7 - Board with Nail
            new int[] { 313, 3774, 3775, 3776, 3777, 63370051 },       //  8 - Dabus
            new int[] { 356, 3897, 3898, 3899, 3900, 63370053 },       //  9 - Tofun
            new int[] { 321, 3802, 3803, 3804, 3805, 63370052 },       // 10 - Jitte
            new int[] { 308, 3762, 3763, 3764, 3765, 63370054 },       // 11 - Budiaq
            new int[] { 7771, 7795, 7796, 7797, 7798, 63370055 },      // 12 - Naginata
            new int[] { 30606, 30607, 30608, 30609, 30610, 63370056 }, // 13 - Bastone
            new int[] { 322, 3806, 3807, 3808, 3809, 63370057 },       // 14 - Jo
            new int[] { 6853, 45104, 45105, 45106, 45107, 63370062 },  // 15 - Rapier (MS)
            new int[] { 30566, 30567, 30568, 30569, 30570, 63370058 }, // 16 - Sabra
            new int[] { 339, 3849, 3850, 3851, 3852, 63370059 },       // 17 - Scimitar
            new int[] { 352, 3885, 3886, 3887, 3888, 63370060 },       // 18 - Short Sword
            new int[] { 345, 3869, 3870, 3871, 3872, 63370061 },       // 19 - Simi
            new int[] { 361, 3909, 3910, 3911, 3912, 63370063 },       // 20 - Yaoji
            new int[] { 31784, 31785, 31786, 31787, 31783, 63370064 }, // 21 - Claw
            new int[] { 45118, 45119, 45120, 45121, 45122, 63370065 }  // 22 - Hand Wraps
        };

        public static readonly int[][] TwoHandedWeaponsMatrix =
        {
            new int[] { 40760, 40761, 40762, 40763, 40764, 63370075 }, //  0 - Nodachi
            new int[] { 41067, 41068, 41069, 41070, 41071, 63370076 }, //  1 - Shashqa
            new int[] { 40618, 40619, 40620, 40621, 40622, 63370077 }, //  2 - Spadone
            new int[] { 41057, 41058, 41059, 41060, 41061, 63370067 }, //  3 - Great Star Mace
            new int[] { 40623, 40624, 40625, 40626, 40627, 63370069 }, //  4 - Quadrelle
            new int[] { 41062, 41063, 41064, 41065, 41066, 63370068 }, //  5 - Khanda-handled Mace
            new int[] { 40635, 40636, 40637, 40638, 40639, 63370070 }, //  6 - Tetsubo
            new int[] { 41052, 41053, 41054, 41055, 41056, 63370066 }, //  7 - Great Axe
            new int[] { 41036, 41037, 41038, 41039, 41040, 63370071 }, //  8 - Assagai
            new int[] { 41046, 41047, 41048, 41049, 41050, 63370074 }, //  9 - Pike
            new int[] { 40818, 40819, 40820, 40821, 40822, 63370072 }, // 10 - Corsesca
            new int[] { 41041, 41042, 41043, 41044, 41045, 63370073 }  // 11 - Magari Yari
        };

        // Elements: Slashing, Piercing, Blunt, Frost, Fire, Acid, Electric, Nether
        public static readonly int[][] ElementalMissileWeaponsMatrix =
        {
            new int[] { 29244, 29239, 29243, 29241, 29242, 29238, 29240, 63370089 }, // War Bow
            new int[] { 29251, 29246, 29250, 29248, 29249, 29245, 29247, 63370083 }, // Long Bow
            new int[] { 29258, 29253, 29257, 29255, 29256, 29252, 29254, 63370080 }, // Crossbow Heavy
            new int[] { 31812, 31814, 31818, 31816, 31817, 31813, 31815, 63370081 }, // Atlatl
            new int[] { 31798, 31800, 31804, 31802, 31803, 31799, 31801, 63370082 }, // Royal Atlatl
            new int[] { 31805, 31807, 31811, 31809, 31810, 31806, 31808, 63370079 }  // Crossbow Light
        };

        // Elements for casters: Base (undef), Slashing, Piercing, Blunt, Frost, Fire, Acid, Electric, Nether
        // Skip first row (base undef weapons) when randomizing
        public static readonly int[][] CasterWeaponsMatrix =
        {
            new int[] { 2366, 2548, 2547, 2472 }, // Orb, Sceptre, Staff, Wand
            new int[] { 29265, 29264, 29260, 29263, 29262, 29259, 29261, 43381 }, // Sceptre: Slashing, Piercing, Blunt, Frost, Fire, Acid, Electric, Nether
            new int[] { 31819, 31825, 31821, 31824, 31823, 31820, 31822, 43382 }, // Baton: Slashing, Piercing, Blunt, Frost, Fire, Acid, Electric, Nether
            new int[] { 37223, 37222, 37225, 37221, 37220, 37224, 37219, 43383 }  // Staff: Slashing, Piercing, Blunt, Frost, Fire, Acid, Electric, Nether
        };
    }
}

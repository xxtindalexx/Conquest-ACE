using System;

namespace ACE.Entity.Enum
{
    [Flags]
    public enum ImbuedEffectType: uint
    {
        Undef                           = 0,
        CriticalStrike                  = 0x0001,
        CripplingBlow                   = 0x0002,
        ArmorRending                    = 0x0004,
        SlashRending                    = 0x0008,
        PierceRending                   = 0x0010,
        BludgeonRending                 = 0x0020,
        AcidRending                     = 0x0040,
        ColdRending                     = 0x0080,
        ElectricRending                 = 0x0100,
        FireRending                     = 0x0200,
        MeleeDefense                    = 0x0400,
        MissileDefense                  = 0x0800,
        MagicDefense                    = 0x1000,
        Spellbook                       = 0x2000,
        NetherRending                   = 0x4000,

        IgnoreSomeMagicProjectileDamage = 0x20000000,
        AlwaysCritical                  = 0x40000000,
        IgnoreAllArmor                  = 0x80000000
    }


    public static class ImbuedEffectTypeExtensions
    {
        /// <summary>
        /// Returns a user-friendly display name for the imbued effect type
        /// Used in appraisal info for weapon properties
        /// </summary>
        public static string DisplayName(this ImbuedEffectType effect)
        {
            switch (effect)
            {
                case ImbuedEffectType.CriticalStrike:
                    return "Critical Strike";
                case ImbuedEffectType.CripplingBlow:
                    return "Crippling Blow";
                case ImbuedEffectType.ArmorRending:
                    return "Armor Rending";
                case ImbuedEffectType.SlashRending:
                    return "Slashing Rending";
                case ImbuedEffectType.PierceRending:
                    return "Piercing Rending";
                case ImbuedEffectType.BludgeonRending:
                    return "Bludgeoning Rending";
                case ImbuedEffectType.AcidRending:
                    return "Acid Rending";
                case ImbuedEffectType.ColdRending:
                    return "Cold Rending";
                case ImbuedEffectType.ElectricRending:
                    return "Lightning Rending";
                case ImbuedEffectType.FireRending:
                    return "Fire Rending";
                case ImbuedEffectType.MeleeDefense:
                    return "Melee Defense";
                case ImbuedEffectType.MissileDefense:
                    return "Missile Defense";
                case ImbuedEffectType.MagicDefense:
                    return "Magic Defense";
                case ImbuedEffectType.Spellbook:
                    return "Spellbook";
                case ImbuedEffectType.NetherRending:
                    return "Void Rending";
                case ImbuedEffectType.IgnoreSomeMagicProjectileDamage:
                    return "Magic Absorb";
                case ImbuedEffectType.AlwaysCritical:
                    return "Always Critical";
                case ImbuedEffectType.IgnoreAllArmor:
                    return "Ignore Armor";
                default:
                    return effect.ToString();
            }
        }
    }
}

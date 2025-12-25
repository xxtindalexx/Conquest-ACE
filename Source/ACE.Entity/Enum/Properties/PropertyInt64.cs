using System.ComponentModel;

namespace ACE.Entity.Enum.Properties
{
    // No properties are sent to the client unless they featured an attribute.
    // SendOnLogin gets sent to players in the PlayerDescription event
    // AssessmentProperty gets sent in successful appraisal
    public enum PropertyInt64 : ushort
    {
        Undef                 = 0,
        [SendOnLogin]
        TotalExperience       = 1,
        [SendOnLogin]
        AvailableExperience   = 2,
        [AssessmentProperty]
        AugmentationCost      = 3,
        [AssessmentProperty]
        ItemTotalXp           = 4,
        [AssessmentProperty]
        ItemBaseXp            = 5,
        [SendOnLogin]
        AvailableLuminance    = 6,
        [SendOnLogin]
        MaximumLuminance      = 7,
        InteractionReqs       = 8,

        /* Custom Properties */
        AllegianceXPCached    = 9000,
        AllegianceXPGenerated = 9001,
        AllegianceXPReceived  = 9002,
        VerifyXp              = 9003,

        // Conquest: Banking System
        BankedPyreals         = 9004,
        BankedLuminance       = 9005,
        BankedLegendaryKeys   = 9015,
        // CONQUEST: BankedMythicalKeys removed (not used in Conquest)

        // Conquest: Custom Currencies
        ConquestCoins         = 9028,  // Non-tradable currency for augmentations, imbues, tailors
        SoulFragments         = 9029,  // Non-tradable PvP currency
        EventTokens           = 9030,  // Tradable event currency

        // CONQUEST: PK System - Soul Fragment Loot Cooldown (Unix timestamp)
        LastSoulFragmentLootTime = 9031,
        LastPKDeathTime          = 9032,  // Unix timestamp of last PK vs PK death (for 20-minute cooldown)
        LastPKFlagTime           = 9033,  // Unix timestamp when player last flagged PK (for cooldown checks)
        DailySoulFragmentCount   = 9034,  // Daily Soul Fragment drops from PK dungeon mobs (20/day cap)
        LastSoulFragmentResetTime = 9035,  // Unix timestamp of last daily Soul Fragment reset
        LastPKDungeonDeathLocation = 9036,  // Packed landblock+variant where player died (landblock << 16 | variant)
        LastPKDungeonDeathTime   = 9037,  // Unix timestamp when player died in PK dungeon (for 20-min re-entry cooldown)
        LastSoulFragmentCapNotifyTime = 9038, //Unix timestamp of last "daily cap reached" notification (throttle spam)

        // Conquest: Quest & Progression
        QuestCount            = 9006,

        // Conquest: Allegiance Tracking
        AllegianceLumCached   = 9012,
        AllegianceLumGenerated= 9013,
        AllegianceLumReceived = 9014,

        // Conquest: Augmentation Tracking (Luminance Augmentations)
        LumAugCreatureCount   = 9007,
        LumAugItemCount       = 9008,
        LumAugLifeCount       = 9009,
        LumAugVoidCount       = 9010,
        LumAugWarCount        = 9011,
        LumAugDurationCount   = 9016,
        LumAugSpecializeCount = 9017,
        LumAugSummonCount     = 9018,
        LumAugMeleeCount      = 9022,
        LumAugMissileCount    = 9023,
        LumAugMeleeDefenseCount   = 9024,
        LumAugMissileDefenseCount = 9025,
        LumAugMagicDefenseCount   = 9026,
    }
}

using System;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects.Managers
{
    /// <summary>
    /// CONQUEST: Augmentation Gem Handling (+1 and +5 variants)
    /// This partial class extends EmoteManager with luminance augmentation gem processing
    /// </summary>
    public partial class EmoteManager
    {
        // CONQUEST: Tiered Augmentation Cost System 2.0
        // Tier bases are derived from emote.Amount (so gem config still matters)
        // Flat 5% increase across all tiers for simple player calculation
        // Tier 1 (0-14):   1.0x  multiplier → 2.5M base
        // Tier 2 (15-29):  2.4x  multiplier → 6M base
        // Tier 3 (30-59):  4.8x  multiplier → 12M base
        // Tier 4 (60+):    12.0x multiplier → 30M base (ensures smooth transition from T3)

        /// <summary>
        /// CONQUEST: Calculates the cost for a single augmentation based on tiered pricing
        /// </summary>
        /// <param name="augIndex">The 0-based index of the augmentation being purchased</param>
        /// <param name="baseCost">Base cost from emote.Amount</param>
        /// <param name="emotePercent">Percentage increase from emote.Percent</param>
        /// <returns>Cost in luminance for this augmentation</returns>
        private static double CalculateTieredAugmentationCost(long augIndex, double baseCost, double emotePercent)
        {
            double tierBase;
            long positionInTier;

            if (augIndex >= 60)
            {
                // Tier 4: 60+ (30M base with default 2.5M emote.Amount)
                tierBase = baseCost * 12.0;
                positionInTier = augIndex - 60;
            }
            else if (augIndex >= 30)
            {
                // Tier 3: 30-59 (12M base with default 2.5M emote.Amount)
                tierBase = baseCost * 4.8;
                positionInTier = augIndex - 30;
            }
            else if (augIndex >= 15)
            {
                // Tier 2: 15-29 (6M base with default 2.5M emote.Amount)
                tierBase = baseCost * 2.4;
                positionInTier = augIndex - 15;
            }
            else
            {
                // Tier 1: 0-14 (2.5M base - uses emote.Amount directly)
                tierBase = baseCost;
                positionInTier = augIndex;
            }

            // Calculate cost: tierBase × (1 + positionInTier × emotePercent)
            // Flat percentage across all tiers for easy player calculation
            return tierBase * (1.0 + (positionInTier * emotePercent));
        }

        /// <summary>
        /// CONQUEST: Handles augmentation gem usage for EmoteType.PromptAddAugment
        /// Supports +1 and +5 variants for all 10 augmentation types
        /// </summary>
        public void HandleAugmentationGem(PropertiesEmoteAction emote, Player player)
        {
            if (player == null || emote == null)
                return;

            var augType = emote.Message; // e.g., "Creature", "Creature5", "Item", "Item5", etc.

            // Determine augmentation type and count from message suffix
            int augCount = 1; // Default to +1
            string baseAugType = augType;

            if (augType.EndsWith("5"))
            {
                augCount = 5;
                baseAugType = augType.Substring(0, augType.Length - 1);
            }

            // Get current augmentation count
            long currentAugCount = 0;

            switch (baseAugType)
            {
                case "Creature":
                    currentAugCount = player.LuminanceAugmentCreatureCount ?? 0;
                    break;
                case "Item":
                    currentAugCount = player.LuminanceAugmentItemCount ?? 0;
                    break;
                case "Life":
                    currentAugCount = player.LuminanceAugmentLifeCount ?? 0;
                    break;
                case "War":
                    currentAugCount = player.LuminanceAugmentWarCount ?? 0;
                    break;
                case "Void":
                    currentAugCount = player.LuminanceAugmentVoidCount ?? 0;
                    break;
                case "Duration":
                    currentAugCount = player.LuminanceAugmentSpellDurationCount ?? 0;
                    break;
                case "Specialization":
                    currentAugCount = player.LuminanceAugmentSpecializeCount ?? 0;
                    break;
                case "Summon":
                    currentAugCount = player.LuminanceAugmentSummonCount ?? 0;
                    break;
                case "Melee":
                    currentAugCount = player.LuminanceAugmentMeleeCount ?? 0;
                    break;
                case "Missile":
                    currentAugCount = player.LuminanceAugmentMissileCount ?? 0;
                    break;
                default:
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown augmentation type: {augType}", ChatMessageType.Broadcast));
                    return;
            }

            // Base cost and percent increase from emote configuration
            double baseCost = (double)(emote.Amount ?? 2500000);
            double emotePercent = (emote.Percent ?? 5.0) / 100.0; // Convert from percentage to decimal
            double totalCost = 0;

            // CONQUEST: Calculate cumulative cost with tiered pricing
            // Each augmentation is priced based on the player's current count for THIS type
            for (int i = 0; i < augCount; i++)
            {
                long thisAugIndex = currentAugCount + i;
                totalCost += CalculateTieredAugmentationCost(thisAugIndex, baseCost, emotePercent);
            }

            // Check if player has enough banked luminance
            if (player.BankedLuminance < totalCost)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough banked luminance to use this gem. This requires {totalCost:N0} banked luminance.", ChatMessageType.Broadcast));
                return;
            }

            // Show confirmation dialog
            player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, () =>
            {
                // Re-check luminance (player might have spent it between confirmation)
                if (player.BankedLuminance < totalCost)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough banked luminance. This requires {totalCost:N0} banked luminance.", ChatMessageType.Broadcast));
                    return;
                }

                // Deduct luminance using SpendLuminance for proper handling
                if (!player.SpendLuminance((long)totalCost))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spend the necessary luminance. Please try again.", ChatMessageType.Broadcast));
                    return;
                }

                // Apply the augmentations
                switch (baseAugType)
                {
                    case "Creature":
                        player.LuminanceAugmentCreatureCount = currentAugCount + augCount;
                        break;
                    case "Item":
                        player.LuminanceAugmentItemCount = currentAugCount + augCount;
                        break;
                    case "Life":
                        player.LuminanceAugmentLifeCount = currentAugCount + augCount;
                        break;
                    case "War":
                        player.LuminanceAugmentWarCount = currentAugCount + augCount;
                        break;
                    case "Void":
                        player.LuminanceAugmentVoidCount = currentAugCount + augCount;
                        break;
                    case "Duration":
                        player.LuminanceAugmentSpellDurationCount = currentAugCount + augCount;
                        break;
                    case "Specialization":
                        player.LuminanceAugmentSpecializeCount = currentAugCount + augCount;
                        break;
                    case "Summon":
                        player.LuminanceAugmentSummonCount = currentAugCount + augCount;
                        break;
                    case "Melee":
                        player.LuminanceAugmentMeleeCount = currentAugCount + augCount;
                        break;
                    case "Missile":
                        player.LuminanceAugmentMissileCount = currentAugCount + augCount;
                        break;
                }

                // Consume the gem (the WorldObject that triggered this emote)
                if (WorldObject != null)
                {
                    player.TryConsumeFromInventoryWithNetworking(WorldObject, 1);
                }

                // Notify player
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your {baseAugType} augmentation by +{augCount} (now at {currentAugCount + augCount}). You spent {totalCost:N0} banked luminance.", ChatMessageType.Broadcast));

                // Save to database
                player.SaveBiotaToDatabase();

            }), $"You are about to spend {totalCost:N0} banked luminance to increase your {baseAugType} augmentation by +{augCount}. Are you sure?");
        }
    }
}

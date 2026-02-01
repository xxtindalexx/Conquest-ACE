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
        /// <summary>
        /// CONQUEST: Handles augmentation gem usage for EmoteType.PromptAddAugment
        /// Supports +1 and +5 variants for all 10 augmentation types
        /// </summary>
        public void HandleAugmentationGem(PropertiesEmoteAction emote, Player player)
        {
            if (player == null || emote == null)
                return;

            var augType = emote.Message; // e.g., "Creature", "Creature5", "Item", "Item5", etc.

            // Determine augmentation type and count
            int augCount = 1; // Default to +1
            string baseAugType = augType;

            if (augType.EndsWith("5"))
            {
                augCount = 5;
                baseAugType = augType.Substring(0, augType.Length - 1); // Remove the "5"
            }

            // Base luminance cost per augmentation level (from emote.Amount)
            double baseCost = (double)(emote.Amount ?? 1000);

            // Get current augmentation count and calculate total cost
            long currentAugCount = 0;
            double totalCost = 0;
            uint gemWeenieId = 0;

            switch (baseAugType)
            {
                case "Creature":
                    currentAugCount = player.LuminanceAugmentCreatureCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370004u : 13370013u; // Placeholder weenie IDs
                    break;
                case "Item":
                    currentAugCount = player.LuminanceAugmentItemCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370005u : 13370014u;
                    break;
                case "Life":
                    currentAugCount = player.LuminanceAugmentLifeCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370006u : 13370015u;
                    break;
                case "War":
                    currentAugCount = player.LuminanceAugmentWarCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370007u : 13370016u;
                    break;
                case "Void":
                    currentAugCount = player.LuminanceAugmentVoidCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370008u : 13370017u;
                    break;
                case "Duration":
                    currentAugCount = player.LuminanceAugmentSpellDurationCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370011u : 13370020u;
                    break;
                case "Specialization":
                    currentAugCount = player.LuminanceAugmentSpecializeCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370012u : 13370021u;
                    break;
                case "Summon":
                    currentAugCount = player.LuminanceAugmentSummonCount ?? 0;
                    gemWeenieId = augCount == 1 ? 999007u : 999017u;
                    break;
                case "Melee":
                    currentAugCount = player.LuminanceAugmentMeleeCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370009u : 13370018u;
                    break;
                case "Missile":
                    currentAugCount = player.LuminanceAugmentMissileCount ?? 0;
                    gemWeenieId = augCount == 1 ? 13370010u : 13370019u;
                    break;
                default:
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown augmentation type: {augType}", ChatMessageType.Broadcast));
                    return;
            }

            // Calculate cumulative cost for the specified number of augmentations
            // CONQUEST: Linear cost scaling using weenie-configured percent: baseCost * (1 + currentCount * percent) per augmentation
            var percentMultiplier = (emote.Percent ?? 1.0) / 100.0; // Convert from percentage to decimal (e.g., 0.3625% -> 0.003625)
            for (int i = 0; i < augCount; i++)
            {
                var costMultiplier = 1.0 + ((currentAugCount + i) * percentMultiplier);
                totalCost += baseCost * costMultiplier;
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

                // Deduct banked luminance
                player.BankedLuminance -= (long)totalCost;

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

                // Consume the gem
                if (gemWeenieId != 0)
                {
                    player.TryConsumeFromInventoryWithNetworking(gemWeenieId, 1);
                }

                // Notify player
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have successfully increased your {baseAugType} augmentation by +{augCount} (now at {currentAugCount + augCount}). You spent {totalCost:N0} banked luminance.", ChatMessageType.Broadcast));

                // Save to database
                player.SaveBiotaToDatabase();

            }), $"You are about to spend {totalCost:N0} banked luminance to increase your {baseAugType} augmentation by +{augCount}. Are you sure?");
        }
    }
}

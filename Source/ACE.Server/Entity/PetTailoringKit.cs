using System;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Entity
{
    /// <summary>
    /// CONQUEST: Handles pet tailoring kit functionality
    /// Allows copying the visual appearance (PetClass) from one pet device to another
    /// while preserving the target's rarity and stat bonuses
    /// </summary>
    public static class PetTailoringKit
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // CONQUEST: Pet Tailoring Kit WCID
        public const uint PetTailoringKitWcid = 13370142;

        /// <summary>
        /// Returns TRUE if the wcid is a Pet Tailoring Kit
        /// </summary>
        public static bool IsPetTailoringKit(uint wcid)
        {
            return wcid == PetTailoringKitWcid;
        }

        /// <summary>
        /// Returns TRUE if the kit has a stored pet appearance
        /// </summary>
        public static bool HasStoredAppearance(WorldObject kit)
        {
            return kit.GetProperty(PropertyInt.PetClass) != null;
        }

        /// <summary>
        /// Main entry point for using pet tailoring kit
        /// Two-phase operation:
        /// Phase 1: Use kit on source pet device - captures PetClass
        /// Phase 2: Use kit on target pet device - applies stored PetClass
        /// </summary>
        public static void UseObjectOnTarget(Player player, WorldObject kit, WorldObject target)
        {
            // Verify kit is valid
            if (!IsPetTailoringKit(kit.WeenieClassId))
            {
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            // Verify target is a PetDevice
            if (!(target is PetDevice targetPetDevice))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "You can only use the Pet Tailoring Kit on pet devices.",
                    ChatMessageType.Broadcast));
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            // Verify target has PetRarity (only rarity-enabled pet devices can be tailored)
            var targetRarity = targetPetDevice.GetProperty(PropertyInt.PetRarity);
            if (targetRarity == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "This pet device cannot be tailored. Only pets with a rarity level can be tailored.",
                    ChatMessageType.Broadcast));
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            // Verify both items are in player inventory
            if (player.FindObject(kit.Guid.Full, Player.SearchLocations.MyInventory) == null ||
                player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) == null)
            {
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            // Check if kit already has a stored appearance
            if (HasStoredAppearance(kit))
            {
                // Phase 2: Apply stored appearance to target
                ApplyStoredAppearance(player, kit, targetPetDevice);
            }
            else
            {
                // Phase 1: Capture appearance from source
                CaptureAppearance(player, kit, targetPetDevice);
            }
        }

        /// <summary>
        /// Phase 1: Captures the PetClass from source pet device onto the kit
        /// </summary>
        private static void CaptureAppearance(Player player, WorldObject kit, PetDevice source)
        {
            var sourcePetClass = source.PetClass;
            if (sourcePetClass == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "This pet device has no creature appearance to capture.",
                    ChatMessageType.Broadcast));
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            var animTime = 0.0f;
            var actionChain = new ActionChain();

            // Switch to peace mode if needed
            if (player.CombatMode != CombatMode.NonCombat)
            {
                var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                actionChain.AddDelaySeconds(stanceTime);
                animTime += stanceTime;
            }

            // Perform clapping motion
            animTime += player.EnqueueMotion(actionChain, MotionCommand.ClapHands);

            actionChain.AddAction(player, ActionType.PetTailoringKit_CaptureAppearance, () =>
            {
                // Re-verify items are still valid
                if (player.FindObject(kit.Guid.Full, Player.SearchLocations.MyInventory) == null ||
                    player.FindObject(source.Guid.Full, Player.SearchLocations.MyInventory) == null)
                {
                    player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                    return;
                }

                // Store the PetClass on the kit
                kit.SetProperty(PropertyInt.PetClass, sourcePetClass.Value);

                // Store the base name (without rarity prefix) for later application
                var baseName = StripRarityPrefix(source.Name ?? "Pet Essence");
                kit.SetProperty(PropertyString.LongDesc, baseName);

                // CONQUEST: Capture palette properties for visual appearance
                var sourcePaletteTemplate = source.GetProperty(PropertyInt.PaletteTemplate);
                if (sourcePaletteTemplate.HasValue)
                    kit.SetProperty(PropertyInt.PaletteTemplate, sourcePaletteTemplate.Value);
                else
                    kit.RemoveProperty(PropertyInt.PaletteTemplate);

                var sourceShade = source.GetProperty(PropertyFloat.Shade);
                if (sourceShade.HasValue)
                    kit.SetProperty(PropertyFloat.Shade, sourceShade.Value);
                else
                    kit.RemoveProperty(PropertyFloat.Shade);

                // CONQUEST: Capture PaletteBase for the selected palette template
                var sourcePaletteBase = source.GetProperty(PropertyDataId.PaletteBase);
                if (sourcePaletteBase.HasValue)
                    kit.SetProperty(PropertyDataId.PaletteBase, sourcePaletteBase.Value);
                else
                    kit.RemoveProperty(PropertyDataId.PaletteBase);

                // CONQUEST: Capture icon properties from source pet device
                if (source.IconId != 0)
                    kit.IconId = source.IconId;
                if (source.IconOverlayId.HasValue)
                    kit.IconOverlayId = source.IconOverlayId.Value;
                // Don't copy IconUnderlayId - kit doesn't need rarity indicator

                // Update kit name to show it has a stored appearance (e.g., "Tiny Pet Wasp Tailoring Kit")
                kit.Name = $"{baseName} Tailoring Kit";

                // Notify client of property changes
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(kit, PropertyInt.PetClass, sourcePetClass.Value));
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyString(kit, PropertyString.Name, kit.Name));
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyDataID(kit, PropertyDataId.Icon, kit.IconId));

                // Send full object update to refresh the icon visually
                player.Session.Network.EnqueueSend(new GameMessageUpdateObject(kit));

                // Consume the source pet device
                player.TryConsumeFromInventoryWithNetworking(source, 1);

                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"You extract the appearance of the {baseName} into the Pet Tailoring Kit. The source pet has been consumed. Use the kit on another pet device to apply this appearance.",
                    ChatMessageType.Broadcast));

                kit.SaveBiotaToDatabase();
                player.SendUseDoneEvent();
            });

            actionChain.EnqueueChain();
            player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
        }

        /// <summary>
        /// Phase 2: Applies the stored PetClass to the target pet device
        /// </summary>
        private static void ApplyStoredAppearance(Player player, WorldObject kit, PetDevice target)
        {
            var storedPetClass = kit.GetProperty(PropertyInt.PetClass);
            if (storedPetClass == null)
            {
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            // Don't allow applying to the same appearance
            if (target.PetClass == storedPetClass.Value)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "This pet device already has the same appearance.",
                    ChatMessageType.Broadcast));
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            var animTime = 0.0f;
            var actionChain = new ActionChain();

            // Switch to peace mode if needed
            if (player.CombatMode != CombatMode.NonCombat)
            {
                var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                actionChain.AddDelaySeconds(stanceTime);
                animTime += stanceTime;
            }

            // Perform clapping motion
            animTime += player.EnqueueMotion(actionChain, MotionCommand.ClapHands);

            actionChain.AddAction(player, ActionType.PetTailoringKit_ApplyAppearance, () =>
            {
                // Re-verify items are still valid
                if (player.FindObject(kit.Guid.Full, Player.SearchLocations.MyInventory) == null ||
                    player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) == null)
                {
                    player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                    return;
                }

                // Get old name for message
                var oldName = target.Name ?? "Unknown";

                // Apply the new PetClass to the target
                target.PetClass = storedPetClass.Value;

                // CONQUEST: Apply captured palette properties
                var storedPaletteTemplate = kit.GetProperty(PropertyInt.PaletteTemplate);
                if (storedPaletteTemplate.HasValue)
                    target.SetProperty(PropertyInt.PaletteTemplate, storedPaletteTemplate.Value);
                else
                    target.RemoveProperty(PropertyInt.PaletteTemplate);

                var storedShade = kit.GetProperty(PropertyFloat.Shade);
                if (storedShade.HasValue)
                    target.SetProperty(PropertyFloat.Shade, storedShade.Value);
                else
                    target.RemoveProperty(PropertyFloat.Shade);

                // CONQUEST: Apply captured PaletteBase
                var storedPaletteBase = kit.GetProperty(PropertyDataId.PaletteBase);
                if (storedPaletteBase.HasValue)
                    target.SetProperty(PropertyDataId.PaletteBase, storedPaletteBase.Value);
                else
                    target.RemoveProperty(PropertyDataId.PaletteBase);

                // CONQUEST: Apply captured icon properties
                var storedIcon = kit.GetProperty(PropertyDataId.Icon);
                if (storedIcon.HasValue)
                    target.IconId = storedIcon.Value;
                if (kit.IconOverlayId.HasValue)
                    target.IconOverlayId = kit.IconOverlayId.Value;
                // Note: Keep target's IconUnderlayId (rarity indicator) - don't overwrite

                // Get the stored base name and target's rarity to construct new name
                var storedBaseName = kit.GetProperty(PropertyString.LongDesc) ?? "Pet Essence";
                var targetRarity = target.GetProperty(PropertyInt.PetRarity);
                var rarityPrefix = GetRarityPrefix(targetRarity);

                // Combine target's rarity with source's base name
                target.Name = string.IsNullOrEmpty(rarityPrefix) ? storedBaseName : $"{rarityPrefix} {storedBaseName}";

                // Notify client of property changes
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(target, PropertyInt.PetClass, storedPetClass.Value));
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyString(target, PropertyString.Name, target.Name));

                // Send UpdateObject for the client to see changes
                player.Session.Network.EnqueueSend(new GameMessageUpdateObject(target));

                // Consume the kit (single use)
                player.TryConsumeFromInventoryWithNetworking(kit, 1);

                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"You tailor the {oldName} to look like a {target.Name}. The pet's stats and rarity have been preserved.",
                    ChatMessageType.Broadcast));

                target.SaveBiotaToDatabase();
                player.SendUseDoneEvent();
            });

            actionChain.EnqueueChain();
            player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
        }

        /// <summary>
        /// Gets the creature name from a weenie class ID
        /// </summary>
        private static string GetCreatureName(uint wcid)
        {
            var weenie = ACE.Database.DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie != null)
            {
                var name = weenie.GetProperty(PropertyString.Name);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return $"Creature #{wcid}";
        }

        /// <summary>
        /// Rarity prefixes to strip/apply
        /// </summary>
        private static readonly string[] RarityPrefixes = { "Mythic", "Legendary", "Rare", "Common" };

        /// <summary>
        /// Strips the rarity prefix from a pet device name
        /// e.g., "Mythic Tiny Pet Essence" -> "Tiny Pet Essence"
        /// </summary>
        private static string StripRarityPrefix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Pet Essence";

            foreach (var prefix in RarityPrefixes)
            {
                if (name.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return name.Substring(prefix.Length + 1);
                }
            }

            return name;
        }

        /// <summary>
        /// Gets the rarity prefix string based on PetRarity value
        /// 1 = Common, 2 = Rare, 3 = Legendary, 4 = Mythic
        /// </summary>
        private static string GetRarityPrefix(int? rarity)
        {
            return rarity switch
            {
                1 => "Common",
                2 => "Rare",
                3 => "Legendary",
                4 => "Mythic",
                _ => ""
            };
        }
    }
}

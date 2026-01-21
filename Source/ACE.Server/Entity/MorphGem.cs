using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Handles morph gem functionality for weapon modification
    /// </summary>
    public class MorphGem
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // CONQUEST: Morph gem WCIDs
        public const uint MorphGemRandomizeWeaponElement = 13370147;

        // Mapping of damage types to imbue effects for rending
        private static readonly Dictionary<DamageType, ImbuedEffectType> DamageToImbueMap = new()
        {
            { DamageType.Slash, ImbuedEffectType.SlashRending },
            { DamageType.Pierce, ImbuedEffectType.PierceRending },
            { DamageType.Bludgeon, ImbuedEffectType.BludgeonRending },
            { DamageType.Cold, ImbuedEffectType.ColdRending },
            { DamageType.Fire, ImbuedEffectType.FireRending },
            { DamageType.Acid, ImbuedEffectType.AcidRending },
            { DamageType.Electric, ImbuedEffectType.ElectricRending },
            { DamageType.Nether, ImbuedEffectType.NetherRending }
        };

        private static readonly ImbuedEffectType AllRendingFlags =
            ImbuedEffectType.SlashRending |
            ImbuedEffectType.PierceRending |
            ImbuedEffectType.BludgeonRending |
            ImbuedEffectType.AcidRending |
            ImbuedEffectType.ColdRending |
            ImbuedEffectType.ElectricRending |
            ImbuedEffectType.FireRending |
            ImbuedEffectType.NetherRending;

        /// <summary>
        /// Returns TRUE if the wcid is a morph gem
        /// </summary>
        public static bool IsMorphGem(uint wcid)
        {
            return wcid == MorphGemRandomizeWeaponElement;
        }

        /// <summary>
        /// Main entry point for applying morph gems to items
        /// </summary>
        public static void Apply(Player player, WorldObject source, WorldObject target)
        {
            try
            {
                switch (source.WeenieClassId)
                {
                    case MorphGemRandomizeWeaponElement:
                        ApplyRandomizeWeaponElement(player, source, target);
                        break;

                    default:
                        player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                        return;
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Exception in MorphGem.Apply. Ex: {0}", ex);
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
            }
        }

        /// <summary>
        /// Randomizes the element of a weapon to a different element in the same weapon type category
        /// </summary>
        private static void ApplyRandomizeWeaponElement(Player player, WorldObject source, WorldObject target)
        {
            // Valid weapon skills that can have their element randomized
            Skill[] validSkills = new[]
            {
                Skill.FinesseWeapons,
                Skill.HeavyWeapons,
                Skill.LightWeapons,
                Skill.MissileWeapons,
                Skill.TwoHandedCombat,
                Skill.WarMagic,
            };

            // Validate the target weapon
            if (target.WieldSkillType == null ||
                !validSkills.Contains((Skill)target.WieldSkillType) ||
                !ContainsOnlyPhysicalOrElemental(target.W_DamageType))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"{target.NameWithMaterial} must be a valid elemental weapon.",
                    ChatMessageType.Broadcast));
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            // CONQUEST: Only allow morph gems on loot-generated items (must have workmanship)
            if (target.ItemWorkmanship == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Morph gems can only be used on loot-generated weapons.",
                    ChatMessageType.Broadcast));
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            // Map weapon skills to their loot table matrices
            var skillMatrixMap = new Dictionary<Skill, (int[][] Matrix, int RandomMax, bool SkipFirst)>
            {
                { Skill.MissileWeapons, (LootTables.ElementalMissileWeaponsMatrix, 8, false) },  // 8 elements including Nether
                { Skill.WarMagic, (LootTables.CasterWeaponsMatrix, 8, true) },                   // 8 elements including Nether
                { Skill.HeavyWeapons, (LootTables.HeavyWeaponsMatrix, 6, false) },               // 6 elements including Nether
                { Skill.FinesseWeapons, (LootTables.FinesseWeaponsMatrix, 6, false) },           // 6 elements including Nether
                { Skill.LightWeapons, (LootTables.LightWeaponsMatrix, 6, false) },               // 6 elements including Nether
                { Skill.TwoHandedCombat, (LootTables.TwoHandedWeaponsMatrix, 6, false) }         // 6 elements including Nether
            };

            if (skillMatrixMap.TryGetValue((Skill)target.WieldSkillType, out var config))
            {
                var (matrices, randomMax, skipFirst) = config;
                var startIndex = skipFirst ? 1 : 0;

                // Find the weapon in the appropriate loot table matrix
                for (int i = startIndex; i < matrices.Length; i++)
                {
                    var matrix = matrices[i];

                    if (!matrix.Contains((int)target.WeenieClassId))
                        continue;

                    // Get current wcid (use swap id if weapon element was previously changed)
                    uint currentWcid = target.GetProperty(PropertyInt.WeenieSwapClassId).HasValue
                        ? (uint)target.GetProperty(PropertyInt.WeenieSwapClassId)
                        : target.WeenieClassId;

                    const int maxAttempts = 1000;
                    int element = -1;
                    int wcid = -1;
                    WorldObject wo = null;

                    // Retry until we get a different element than current element
                    for (int attempt = 0; attempt < maxAttempts; ++attempt)
                    {
                        element = ThreadSafeRandom.Next(0, Math.Min(randomMax, matrix.Length));

                        // Safety check: ensure element index is valid
                        if (element >= matrix.Length)
                            continue;

                        wcid = matrix[element];

                        // Skip if same as current wcid or invalid (0)
                        if (wcid == currentWcid || wcid == 0)
                            continue;

                        wo = WorldObjectFactory.CreateNewWorldObject((uint)wcid);
                        if (wo != null)
                            break;
                    }

                    if (wo == null)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                            $"Failed to find a valid weapon element for {target.NameWithMaterial}.",
                            ChatMessageType.Broadcast));
                        player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                        return;
                    }

                    // Apply the element swap
                    UpdateWeaponElementSwap(player, wo, target, wcid);
                    wo.DeleteObject(player);
                    break;
                }

                // Success message
                var isMultiDamage = target.W_DamageType.IsMultiDamage();
                var damageName = isMultiDamage ? "Slashing/Piercing" : target.W_DamageType.GetName();

                var playerMsg = $"You apply the Morph Gem skillfully and have altered your item so that its element has changed to {damageName}.";
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(playerMsg, ChatMessageType.Broadcast));

                // Consume the morph gem
                player.TryConsumeFromInventoryWithNetworking(source, 1);
                target.SaveBiotaToDatabase();
                player.SendUseDoneEvent();
            }
            else
            {
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
            }
        }

        /// <summary>
        /// Updates a weapon's element by swapping its visual and damage properties
        /// </summary>
        private static void UpdateWeaponElementSwap(Player player, WorldObject source, WorldObject target, int wcid)
        {
            // Delete from client view
            player.Session.Network.EnqueueSend(new GameMessageDeleteObject(target));

            // Determine the current appearance WCID (tailored appearance or original)
            var appearanceWcid = target.GetProperty(PropertyInt.AppearanceWeenieClassId) ?? (int)target.WeenieClassId;

            // Find the new appearance WCID by looking up the current appearance in the matrix
            var newAppearanceWcid = FindNewAppearanceWcid(appearanceWcid, wcid, (Skill)target.WieldSkillType);

            // If we found a new appearance variant, create temp object and copy visual properties
            if (newAppearanceWcid.HasValue)
            {
                var newAppearance = WorldObjectFactory.CreateNewWorldObject(newAppearanceWcid.Value);
                if (newAppearance != null)
                {
                    // Copy visual properties from the new appearance variant
                    Tailoring.UpdateWeaponProps(player, newAppearance, target);

                    // Update the tracked appearance WCID
                    player.UpdateProperty(target, PropertyInt.AppearanceWeenieClassId, (int)newAppearanceWcid.Value);

                    // Clean up temp object
                    newAppearance.DeleteObject(player);
                }
            }

            // Store the swap wcid for future element changes
            player.UpdateProperty(target, PropertyInt.WeenieSwapClassId, wcid);

            // Update damage type and related properties
            player.UpdateProperty(target, PropertyInt.DamageType, (int)source.W_DamageType);
            player.UpdateProperty(target, PropertyDataId.TsysMutationFilter, source.TsysMutationFilter);
            player.UpdateProperty(target, PropertyDataId.MutateFilter, source.MutateFilter);
            player.UpdateProperty(target, PropertyDataId.PhysicsEffectTable, source.PhysicsTableId);
            player.UpdateProperty(target, PropertyDataId.SoundTable, source.SoundTableId);
            player.UpdateProperty(target, PropertyInt.ResistanceModifierType, (int?)source.W_DamageType);

            // Update UiEffects (PropertyInt 18) for elemental glow
            player.UpdateProperty(target, PropertyInt.UiEffects, (int?)source.UiEffects);

            // Handle elemental/physical rending imbues - update to match new damage type
            // ONLY update if weapon has elemental/physical rending (not Armor Rending, Crit Strike, Crippling Blow)
            if ((target.ImbuedEffect & AllRendingFlags) != 0)
            {
                // Preserve other imbue effects that should not change
                var hasFetish = target.HasImbuedEffect(ImbuedEffectType.IgnoreSomeMagicProjectileDamage);
                var hasArmorRending = target.HasImbuedEffect(ImbuedEffectType.ArmorRending);
                var hasCriticalStrike = target.HasImbuedEffect(ImbuedEffectType.CriticalStrike);
                var hasCripplingBlow = target.HasImbuedEffect(ImbuedEffectType.CripplingBlow);

                // Clear fetish temporarily and clear ONLY elemental/physical rending flags
                target.ImbuedEffect &= ~ImbuedEffectType.IgnoreSomeMagicProjectileDamage;
                target.ImbuedEffect &= ~AllRendingFlags;

                // Apply new rending based on new damage type (using source damage type, not target)
                // For multi-damage weapons (e.g., Slash|Pierce), randomly pick one rending type
                var matchingRendings = new List<ImbuedEffectType>();
                foreach (var mapping in DamageToImbueMap)
                {
                    if ((source.W_DamageType & mapping.Key) != 0)
                    {
                        matchingRendings.Add(mapping.Value);
                    }
                }

                // Apply the new rending
                if (matchingRendings.Count > 0)
                {
                    // Randomly select one rending type if multiple match
                    var randomIndex = ThreadSafeRandom.Next(0, matchingRendings.Count - 1);

                    // Debug logging to diagnose index out of range
                    log.Info($"[MORPHGEM] Rending selection - Count: {matchingRendings.Count}, Index: {randomIndex}, Source DamageType: {source.W_DamageType}");

                    // Explicit bounds check
                    if (randomIndex >= 0 && randomIndex < matchingRendings.Count)
                    {
                        var selectedRending = matchingRendings[randomIndex];
                        target.ImbuedEffect |= selectedRending;

                        // Update icon overlay ONLY for elemental/physical rending (not Armor Rending, Crit Strike, Crippling Blow)
                        // Check against the specific rending type we just applied, not combined ImbuedEffect
                        if (RecipeManager.IconUnderlay.ContainsKey(selectedRending))
                        {
                            target.IconUnderlayId = RecipeManager.IconUnderlay[selectedRending];
                        }
                        // Hardcode nether rending icon since it's not in RecipeManager.IconUnderlay
                        else if (selectedRending == ImbuedEffectType.NetherRending)
                        {
                            target.IconUnderlayId = 0x060067A1; // Nether rending icon overlay
                        }
                    }
                    else
                    {
                        log.Error($"[MORPHGEM] Invalid randomIndex: {randomIndex} for list count: {matchingRendings.Count}");
                    }
                }

                // Restore imbue effects that should not change
                if (hasFetish)
                    target.ImbuedEffect |= ImbuedEffectType.IgnoreSomeMagicProjectileDamage;
                if (hasArmorRending)
                    target.ImbuedEffect |= ImbuedEffectType.ArmorRending;
                if (hasCriticalStrike)
                    target.ImbuedEffect |= ImbuedEffectType.CriticalStrike;
                if (hasCripplingBlow)
                    target.ImbuedEffect |= ImbuedEffectType.CripplingBlow;
            }

            // Respawn weapon in client with slight delay
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(0.1);
            actionChain.AddAction(player, ActionType.PhysicsObj_TrackObject, () =>
            {
                player.Session.Network.EnqueueSend(new GameMessageCreateObject(target));
            });
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Finds the new appearance WCID for a weapon after element change
        /// </summary>
        private static uint? FindNewAppearanceWcid(int currentAppearanceWcid, int newElementWcid, Skill weaponSkill)
        {
            // Map skills to their matrices
            var matrices = weaponSkill switch
            {
                Skill.HeavyWeapons => LootTables.HeavyWeaponsMatrix,
                Skill.LightWeapons => LootTables.LightWeaponsMatrix,
                Skill.FinesseWeapons => LootTables.FinesseWeaponsMatrix,
                Skill.TwoHandedCombat => LootTables.TwoHandedWeaponsMatrix,
                Skill.MissileWeapons => LootTables.ElementalMissileWeaponsMatrix,
                Skill.WarMagic => LootTables.CasterWeaponsMatrix,
                _ => null
            };

            if (matrices == null)
                return null;

            // Step 1: Find which column (element) the new element WCID is in
            int? targetColumn = null;
            for (int rowIndex = 0; rowIndex < matrices.Length; rowIndex++)
            {
                var row = matrices[rowIndex];
                for (int colIndex = 0; colIndex < row.Length; colIndex++)
                {
                    if (row[colIndex] == newElementWcid)
                    {
                        targetColumn = colIndex;
                        break;
                    }
                }
                if (targetColumn.HasValue)
                    break;
            }

            if (!targetColumn.HasValue)
            {
                // Couldn't find new element in matrix, return it as fallback
                return (uint)newElementWcid;
            }

            // Step 2: Find which row the current appearance is in
            for (int rowIndex = 0; rowIndex < matrices.Length; rowIndex++)
            {
                var row = matrices[rowIndex];

                // Check if current appearance WCID is in this row
                if (row.Contains(currentAppearanceWcid))
                {
                    // Return the WCID from this row at the target element column
                    if (targetColumn.Value < row.Length)
                    {
                        return (uint)row[targetColumn.Value];
                    }
                    break;
                }
            }

            // If appearance not found in matrix, return the new element WCID as fallback
            return (uint)newElementWcid;
        }

        /// <summary>
        /// Returns TRUE if the damage type only contains physical or elemental damage types (including nether)
        /// </summary>
        public static bool ContainsOnlyPhysicalOrElemental(DamageType damageType)
        {
            const DamageType allowedTypes = DamageType.Physical | DamageType.Elemental | DamageType.Nether;
            return damageType != DamageType.Undef && (damageType & allowedTypes) == damageType;
        }
    }
}

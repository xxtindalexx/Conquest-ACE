using System;
using System.Linq;

using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Common;
using ACE.Server.Entity.Actions;
using System.Runtime.CompilerServices;

namespace ACE.Server.Entity
{
    public class Enlightenment
    {
        // https://asheron.fandom.com/wiki/Enlightenment

        // Reset your character to level 1, losing all experience and luminance but gaining a title, two points in vitality and one point in all of your skills.
        // In order to be eligible for enlightenment, you must be level 275, Master rank in a Society, and have all luminance auras with the exception of the skill credit auras.

        // As stated in the Spring 2014 patch notes, Enlightenment is a process for the most devoted players of Asheron's Call to continue enhancing characters which have been "maxed out" in terms of experience and abilities.
        // It was not intended to be a quest that every player would undertake or be interested in.

        // Requirements:
        // - Level 275
        // - Have all luminance auras (crafting aura included) except the 2 skill credit auras. (20 million total luminance)
        // - Have mastery rank in a society
        // - Have 25 unused pack spaces
        // - Max # of times for enlightenment: 5

        // You lose:
        // - All experience, reverting to level 1.
        // - All luminance, and luminance auras with the exception of the skill credit auras.
        // - The ability to use aetheria (until you attain sufficient level and re-open aetheria slots).
        // - The ability to gain luminance (until you attain level 200 and re-complete Nalicana's Test).
        // - The ability to equip and use items which have skill and level requirements beyond those of a level 1 character.
        //   Any equipped items are moved to your pack automatically.

        // You keep:
        // - All augmentations obtained through Augmentation Gems.
        // - Skill credits from luminance auras, Aun Ralirea, and Chasing Oswald quests.
        // - All quest flags with the exception of aetheria and luminance.

        // You gain:
        // - A new title each time you enlighten
        // - +2 to vitality
        // - +1 to all of your skills
        // - An attribute reset certificate

        public static void HandleEnlightenment(Player player)
        {
            if (!VerifyRequirements(player))
                return;

            DequipAllItems(player);

            RemoveFromFellowships(player);

            player.SendMotionAsCommands(MotionCommand.MarketplaceRecall, MotionStance.NonCombat);

            var startPos = new ACE.Entity.Position(player.Location);
            ActionChain enlChain = new ActionChain();
            enlChain.AddDelaySeconds(14);

            // Then do teleport
            player.IsBusy = true;
            // CONQUEST: Removed ActionType.Enlightenment_DoEnlighten (ILT-specific enum)
            enlChain.AddAction(player, () =>
            {
                player.IsBusy = false;
                var endPos = new ACE.Entity.Position(player.Location);
                if (startPos.SquaredDistanceTo(endPos) > Player.RecallMoveThresholdSq)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have moved too far during the enlightenment animation!", ChatMessageType.Broadcast));
                    return;
                }

                player.ThreadSafeTeleportOnDeath();

                // CONQUEST: Spend luminance
                if (!SpendLuminance(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough luminance to enlighten!", ChatMessageType.Broadcast));
                    return;
                }

                // CONQUEST: Consume enlightenment currency
                if (!RemoveEnlightenmentCurrency(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough enlightenment currency!", ChatMessageType.Broadcast));
                    return;
                }

                RemoveAbility(player);
                AddPerks(player);
                if (player.Enlightenment >= 25)
                {
                    DequipAllItems(player);
                }
                player.SaveBiotaToDatabase();
            });

            // Set the chain to run
            enlChain.EnqueueChain();

        }

        public static bool VerifyRequirements(Player player)
        {
            if (player.Level < 300)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be level 300 to enlighten.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.GetFreeInventorySlots() < 25)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have at least 25 free inventory slots in your main pack for enlightenment, to unequip your gear automatically.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.HasVitae)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot reach enlightenment with a Vitae Penalty. Go find those lost pieces of your soul and try again. Check under the couch cushions, that's where I usually lose mine.", ChatMessageType.Broadcast));
                return false;
            }

            if (player.Teleporting || player.TooBusyToRecall || player.IsAnimating || player.IsInDeathProcess)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while teleporting or busy. Complete your movement and try again. Neener neener.", ChatMessageType.System));
                return false;
            }

            // CONQUEST: Removed Variation parameter (ILT-specific for dungeon variations)
            Landblock currentLandblock = LandblockManager.GetLandblock(player.Location.LandblockId, false, false);
            if (currentLandblock != null && currentLandblock.IsDungeon)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while inside a dungeon. Find an exit or recall to begin your enlightenment.", ChatMessageType.System));
                return false;
            }

            if (player.CombatMode != CombatMode.NonCombat)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot enlighten while in combat mode. Be at peace, friend.", ChatMessageType.System));
                return false;
            }


            if (player.LastPortalTeleportTimestamp.HasValue)
            {
                var timeSinceLastPortal = Time.GetUnixTime() - player.LastPortalTeleportTimestamp.Value;
                if (timeSinceLastPortal <= 10.0f)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You've teleported too recently to enlighten.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            var targetEnlightenment = player.Enlightenment + 1;

            // CONQUEST: Cap at 100 enlightenment
            if (targetEnlightenment > 100)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have reached the maximum enlightenment level of 100!", ChatMessageType.Broadcast));
                return false;
            }

            // CONQUEST: Luminance auras required after enlightenment 10 (admin bypass)
            if (targetEnlightenment > 10 && !player.IsPlussed)
            {
                if (!VerifyLumAugs(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have all luminance auras (excluding skill credit auras) to enlighten beyond level 10.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            // CONQUEST: Society master rank required after enlightenment 30 (admin bypass)
            if (targetEnlightenment > 30 && !player.IsPlussed)
            {
                if (!VerifySocietyMaster(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be a Society Master to enlighten beyond level 30.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            // CONQUEST: Currency requirement - TODO: Replace placeholder weenie 999999999 with actual currency weenie
            // Required amount scales with enlightenment level
            int currencyRequired = targetEnlightenment; // 1 per enlightenment level, adjust as needed
            var currencyCount = player.GetNumInventoryItemsOfWCID(13370021);
            if (currencyCount < currencyRequired)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You need {currencyRequired} Enlightenment Currency to reach enlightenment level {targetEnlightenment}. You have {currencyCount}.", ChatMessageType.Broadcast));
                return false;
            }

            // CONQUEST: Simple luminance cost - TODO: Determine final cost formula with client
            // Placeholder: 1 million luminance per enlightenment level
            long baseLumCost = 1_000_000;  // 1M luminance base cost
            long reqLum = targetEnlightenment * baseLumCost;

            if (!VerifyLuminance(player, reqLum))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You need {reqLum:N0} banked luminance to enlighten to level {targetEnlightenment}. You have {player.BankedLuminance:N0}.", ChatMessageType.Broadcast));
                return false;
            }

            return true;
        }

        public static bool VerifyLuminance(Player player, long reqLum)
        {
            return player.BankedLuminance >= reqLum;
        }

        public static bool VerifySocietyMaster(Player player)
        {
            return player.SocietyRankCelhan == 1001 || player.SocietyRankEldweb == 1001 || player.SocietyRankRadblo == 1001;
        }

        public static bool VerifyParagonCompleted(Player player)
        {
            return player.QuestManager.GetCurrentSolves("ParagonEnlCompleted") >= 1;
        }

        public static bool VerifyParagonArmorCompleted(Player player)
        {
            return player.QuestManager.GetCurrentSolves("ParagonArmorCompleted") >= 1;
        }

        public static bool VerifyLumAugs(Player player)
        {
            var lumAugCredits = 0;

            lumAugCredits += player.LumAugAllSkills;
            lumAugCredits += player.LumAugSurgeChanceRating;
            lumAugCredits += player.LumAugCritDamageRating;
            lumAugCredits += player.LumAugCritReductionRating;
            lumAugCredits += player.LumAugDamageRating;
            lumAugCredits += player.LumAugDamageReductionRating;
            lumAugCredits += player.LumAugItemManaUsage;
            lumAugCredits += player.LumAugItemManaGain;
            lumAugCredits += player.LumAugHealingRating;
            lumAugCredits += player.LumAugSkilledCraft;
            lumAugCredits += player.LumAugSkilledSpec;

            return lumAugCredits == 65;
        }

        public static void RemoveFromFellowships(Player player)
        {
            player.FellowshipQuit(false);
        }

        public static void DequipAllItems(Player player)
        {
            var equippedObjects = player.EquippedObjects.Keys.ToList();

            foreach (var equippedObject in equippedObjects)
                player.HandleActionPutItemInContainer(equippedObject.Full, player.Guid.Full, 0);
        }

        public static void RemoveAllSpells(Player player)
        {
            player.EnchantmentManager.DispelAllEnchantments();
        }

        public static void RemoveAbility(Player player)
        {
            RemoveSociety(player);
            //RemoveLuminance(player);
            RemoveSkills(player);
            RemoveLevel(player);
            RemoveAllSpells(player);
        }

        public static void RemoveTokens(Player player)
        {
            // CONQUEST: ILT tokens not used
            //player.TryConsumeFromInventoryWithNetworking(300000, player.Enlightenment + 1 - 5);
        }

        public static void RemoveMedallion(Player player)
        {
            // CONQUEST: ILT medallions not used
            //player.TryConsumeFromInventoryWithNetworking(90000217, player.Enlightenment + 1 - 5);
        }

        public static void RemoveSigil(Player player)
        {
            // CONQUEST: ILT sigils not used
            //player.TryConsumeFromInventoryWithNetworking(300101189, player.Enlightenment + 1 - 5);
        }

        public static bool RemoveEnlightenmentCurrency(Player player)
        {
            // CONQUEST: Consume enlightenment currency
            // TODO: Replace placeholder weenie 999999999 with actual currency weenie
            int currencyRequired = player.Enlightenment + 1;
            return player.TryConsumeFromInventoryWithNetworking(13370021, currencyRequired);
        }

        public static bool SpendLuminance(Player player)
        {
            // CONQUEST: Simple linear luminance cost formula
            // TODO: Determine final cost formula with client
            long baseLumCost = 1_000_000;  // 1M luminance base cost
            var targetEnlightenment = player.Enlightenment + 1;
            long reqLum = targetEnlightenment * baseLumCost;

            return player.SpendLuminance(reqLum);
        }

        public static void RemoveSociety(Player player)
        {
            // Leave society alone if server prop is false
            if (PropertyManager.GetBool("enl_removes_society").Item)
            {
                player.QuestManager.Erase("SocietyMember");
                player.QuestManager.Erase("CelestialHandMember");
                player.QuestManager.Erase("EnlightenedCelestialHandMaster");
                player.QuestManager.Erase("EldrytchWebMember");
                player.QuestManager.Erase("EnlightenedEldrytchWebMaster");
                player.QuestManager.Erase("RadiantBloodMember");
                player.QuestManager.Erase("EnlightenedRadiantBloodMaster");

                if (player.SocietyRankCelhan == 1001)
                    player.QuestManager.Stamp("EnlightenedCelestialHandMaster"); // after rejoining society, player can get promoted instantly to master when speaking to promotions officer
                if (player.SocietyRankEldweb == 1001)
                    player.QuestManager.Stamp("EnlightenedEldrytchWebMaster");   // after rejoining society, player can get promoted instantly to master when speaking to promotions officer
                if (player.SocietyRankRadblo == 1001)
                    player.QuestManager.Stamp("EnlightenedRadiantBloodMaster");  // after rejoining society, player can get promoted instantly to master when speaking to promotions officer

                player.Faction1Bits = null;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Faction1Bits, 0));
                //player.SocietyRankCelhan = null;
                //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.SocietyRankCelhan, 0));
                //player.SocietyRankEldweb = null;
                //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.SocietyRankEldweb, 0));
                //player.SocietyRankRadblo = null;
                //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.SocietyRankRadblo, 0));
            }
        }

        public static void RemoveLevel(Player player)
        {
            // CONQUEST: Removed TotalExperienceDouble (ILT-specific property)
            player.TotalExperience = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.TotalExperience, player.TotalExperience ?? 0));

            player.Level = 1;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Level, player.Level ?? 0));
        }

        public static void RemoveAetheria(Player player)
        {
            player.QuestManager.Erase("EFULNorthManaFieldUsed");
            player.QuestManager.Erase("EFULSouthManaFieldUsed");
            player.QuestManager.Erase("EFULEastManaFieldUsed");
            player.QuestManager.Erase("EFULWestManaFieldUsed");
            player.QuestManager.Erase("EFULCenterManaFieldUsed");

            player.QuestManager.Erase("EFMLNorthManaFieldUsed");
            player.QuestManager.Erase("EFMLSouthManaFieldUsed");
            player.QuestManager.Erase("EFMLEastManaFieldUsed");
            player.QuestManager.Erase("EFMLWestManaFieldUsed");
            player.QuestManager.Erase("EFMLCenterManaFieldUsed");

            player.QuestManager.Erase("EFLLNorthManaFieldUsed");
            player.QuestManager.Erase("EFLLSouthManaFieldUsed");
            player.QuestManager.Erase("EFLLEastManaFieldUsed");
            player.QuestManager.Erase("EFLLWestManaFieldUsed");
            player.QuestManager.Erase("EFLLCenterManaFieldUsed");

            player.AetheriaFlags = AetheriaBitfield.None;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AetheriaBitfield, 0));

            player.SendMessage("Your mastery of Aetheric magics fades.", ChatMessageType.Broadcast);
        }

        public static void RemoveAttributes(Player player)
        {
            var propertyCount = Enum.GetNames(typeof(PropertyAttribute)).Length;
            for (var i = 1; i < propertyCount; i++)
            {
                var attribute = (PropertyAttribute)i;

                player.Attributes[attribute].Ranks = 0;
                player.Attributes[attribute].ExperienceSpent = 0;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[attribute]));
            }

            propertyCount = Enum.GetNames(typeof(PropertyAttribute2nd)).Length;
            for (var i = 1; i < propertyCount; i += 2)
            {
                var attribute = (PropertyAttribute2nd)i;

                player.Vitals[attribute].Ranks = 0;
                player.Vitals[attribute].ExperienceSpent = 0;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[attribute]));
            }

            player.SendMessage("Your attribute training fades.", ChatMessageType.Broadcast);
        }

        public static void RemoveSkills(Player player)
        {
            var propertyCount = Enum.GetNames(typeof(Skill)).Length;
            for (var i = 1; i < propertyCount; i++)
            {
                var skill = (Skill)i;

                player.ResetSkill(skill, false);
            }

            player.AvailableExperience = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableExperience, 0));

            var heritageGroup = DatManager.PortalDat.CharGen.HeritageGroups[(uint)player.Heritage];
            var availableSkillCredits = 0;

            availableSkillCredits += (int)heritageGroup.SkillCredits; // base skill credits allowed

            availableSkillCredits += player.QuestManager.GetCurrentSolves("ArantahKill1");       // additional quest skill credit
            availableSkillCredits += player.QuestManager.GetCurrentSolves("OswaldManualCompleted");  // additional quest skill credit
            availableSkillCredits += player.QuestManager.GetCurrentSolves("LumAugSkillQuest");   // additional quest skill credits

            player.AvailableSkillCredits = availableSkillCredits;

            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
        }

        public static void RemoveLuminance(Player player)
        {
            player.AvailableLuminance = 0;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableLuminance, 0));

            player.SendMessage("Your Luminance fades from your spirit.", ChatMessageType.Broadcast);
        }

        public static uint AttributeResetCertificate => 46421;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetEnlightenmentRatingBonus(int EnlightenmentAmt)
        {
            // CONQUEST: Simple formula - +1 DR/DMG per 25 enlightenment
            // ENL 0-24: 0 bonus, ENL 25-49: 1 bonus, ENL 50-74: 2 bonus, ENL 75-99: 3 bonus, ENL 100: 4 bonus
            return EnlightenmentAmt / 25;
        }

        public static void AddPerks(Player player)
        {
            // CONQUEST: Enlightenment bonuses are handled in the following locations:
            // - +1 to all skills: Handled dynamically in CreatureSkill based on Enlightenment property (similar to augmentations)
            // - +1% XP per ENL: Integrated into Player_Xp.cs (EarnXP and other XP gain methods)
            // - +1 all stats per ENL: Integrated into Player.cs (attribute calculations)
            // - +1 DR/DMG per 25 ENL: Integrated into Creature.cs via GetEnlightenmentRatingBonus() method

            player.Enlightenment += 1;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.Enlightenment, player.Enlightenment));

            player.SendMessage("You have become enlightened and view the world with new eyes.", ChatMessageType.Broadcast);
            player.SendMessage("Your available skill credits have been adjusted.", ChatMessageType.Broadcast);
            player.SendMessage("You have risen to a higher tier of enlightenment!", ChatMessageType.Broadcast);

            var lvl = "";

            switch (player.Enlightenment % 100)
            {
                case 11:
                case 12:
                case 13:
                    lvl = player.Enlightenment + "th";
                    break;
            }
            if (string.IsNullOrEmpty(lvl))
            {
                switch (player.Enlightenment % 10)
                {
                    case 1:
                        lvl = player.Enlightenment + "st";
                        break;
                    case 2:
                        lvl = player.Enlightenment + "nd";
                        break;
                    case 3:
                        lvl = player.Enlightenment + "rd";
                        break;
                    default:
                        lvl = player.Enlightenment + "th";
                        break;
                }
            }
            
            // add title
            switch (player.Enlightenment)
            {
                case 1:
                    player.AddTitle(CharacterTitle.Awakened);                   
                    break;
                case 2:
                    player.AddTitle(CharacterTitle.Enlightened);
                    break;
                case 3:
                    player.AddTitle(CharacterTitle.Illuminated);
                    break;
                case 4:
                    player.AddTitle(CharacterTitle.Transcended);
                    break;
                case 5:
                    player.AddTitle(CharacterTitle.CosmicConscious);
                    break;                
            }

            var msg = $"{player.Name} has achieved the {lvl} level of Enlightenment!";
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            DiscordChatManager.SendDiscordMessage(player.Name, msg, ConfigManager.Config.Chat.GeneralChannelId);
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);

            // CONQUEST: Enlightenment bonuses (+1% XP, +1 stats, +1 DR/DMG per 25) are applied dynamically
            // See comments at the top of AddPerks method for integration locations
        }
    }
}

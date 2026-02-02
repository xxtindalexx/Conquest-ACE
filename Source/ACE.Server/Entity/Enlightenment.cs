using System;
using System.Linq;
using System.Runtime.CompilerServices;

using ACE.Common;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

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
            enlChain.AddAction(player, ActionType.Enlightenment_DoEnlighten, () =>
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
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be level 300 to enlighten further.", ChatMessageType.Broadcast));
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
            Landblock currentLandblock = LandblockManager.GetLandblock(player.Location.LandblockId, false, player.Location.Variation, false);
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

            // CONQUEST: Luminance auras required after enlightenment 10
            if (targetEnlightenment > 10 && !player.IsPlussed)
            {
                if (!VerifyLumAugs(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must have all luminance auras (excluding skill credit auras) to enlighten beyond level 10.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            // CONQUEST: Society master rank required after enlightenment 30
            if (targetEnlightenment > 30 && !player.IsPlussed)
            {
                if (!VerifySocietyMaster(player))
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must be a Society Master to enlighten beyond level 30.", ChatMessageType.Broadcast));
                    return false;
                }
            }

            // CONQUEST: Currency requirement - 100 Conquest Coins per enlightenment level from bank
            // ENL 1 = 100 coins, ENL 2 = 200 coins, etc.
            long coinsRequired = targetEnlightenment * 100;
            var bankedCoins = player.GetProperty(PropertyInt64.ConquestCoins) ?? 0;
            if (bankedCoins < coinsRequired)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You need {coinsRequired:N0} Conquest Coins to reach enlightenment level {targetEnlightenment}. You have {bankedCoins:N0} coins in your bank.", ChatMessageType.Broadcast));
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
            // CONQUEST: Deduct Conquest Coins from bank (PropertyInt64.ConquestCoins)
            // Cost: 100 coins * target enlightenment level
            var targetEnlightenment = player.Enlightenment + 1;
            long coinsRequired = targetEnlightenment * 100;
            var bankedCoins = player.GetProperty(PropertyInt64.ConquestCoins) ?? 0;

            if (bankedCoins < coinsRequired)
                return false;

            // Deduct coins from bank
            var newBalance = bankedCoins - coinsRequired;
            player.SetProperty(PropertyInt64.ConquestCoins, newBalance);
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.ConquestCoins, newBalance));

            player.SendMessage($"You have spent {coinsRequired:N0} Conquest Coins. Bank balance: {newBalance:N0} coins.", ChatMessageType.Broadcast);
            return true;
        }

        public static bool SpendLuminance(Player player)
        {
            // CONQUEST: Simple linear luminance cost formula
            // TODO: Determine final cost formula with client
            long baseLumCost = 1_000_000;  // 1M luminance base cost
            var targetEnlightenment = player.Enlightenment + 1;
            long reqLum = targetEnlightenment * baseLumCost;

            // CONQUEST: Spend from BankedLuminance instead of AvailableLuminance
            if (player.BankedLuminance < reqLum)
                return false;

            player.BankedLuminance -= reqLum;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.BankedLuminance, player.BankedLuminance ?? 0));

            return true;
        }

        public static void RemoveSociety(Player player)
        {
            // Leave society alone if server prop is false
            if (PropertyManager.GetBool("enl_removes_society"))
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

            // CONQUEST: Add +1 to all 6 attribute StartingValues (same method as augmentations)
            // This ensures the bonus shows on both the character panel AND inspection panel
            player.Attributes[PropertyAttribute.Strength].StartingValue += 1;
            player.Attributes[PropertyAttribute.Endurance].StartingValue += 1;
            player.Attributes[PropertyAttribute.Coordination].StartingValue += 1;
            player.Attributes[PropertyAttribute.Quickness].StartingValue += 1;
            player.Attributes[PropertyAttribute.Focus].StartingValue += 1;
            player.Attributes[PropertyAttribute.Self].StartingValue += 1;

            // CONQUEST: Refresh all 6 attributes on client to show enlightenment bonus (+1 per ENL)
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[PropertyAttribute.Strength]));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[PropertyAttribute.Endurance]));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[PropertyAttribute.Coordination]));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[PropertyAttribute.Quickness]));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[PropertyAttribute.Focus]));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[PropertyAttribute.Self]));

            // CONQUEST: Refresh vitals since they depend on attributes (Health/Stamina from Endurance, Mana from Self)
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));

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
            
            // CONQUEST: Enlightenment Milestone Titles, Combat Bonuses, and Item Rewards
            switch (player.Enlightenment)
            {
                case 1:
                    // Title: Wimp (retired)
                    player.AddTitle(CharacterTitle.Wimp);
                    break;
                case 5:
                    // Title: GIMP (retired)
                    player.AddTitle(CharacterTitle.Gimp);
                    break;
                case 10:
                    // +1 Cleave: Melee weapons hit an additional target (costs 10 base HP)
                    player.SetProperty(PropertyInt.EnlightenmentCleaveBonus, 1);
                    player.Vitals[PropertyAttribute2nd.MaxHealth].StartingValue -= 10;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    player.SendMessage("You have sacrificed 10 HP to gain +1 Cleave! Your melee weapons now hit an additional target.", ChatMessageType.Broadcast);
                    break;
                case 15:
                    // Title: Lots of Vitae (retired)
                    player.AddTitle(CharacterTitle.LotsofVitae);
                    break;
                case 25:
                    // +1 Arrow Split: Missile weapons hit an additional target (costs 25 base HP)
                    player.SetProperty(PropertyInt.EnlightenmentSplitArrowBonus, 1);
                    player.Vitals[PropertyAttribute2nd.MaxHealth].StartingValue -= 25;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    player.SendMessage("You have sacrificed 25 HP to gain +1 Arrow Split! Your missile weapons now split to hit an additional target.", ChatMessageType.Broadcast);
                    break;
                case 35:
                    // Title: Certified Ganksta (retired)
                    player.AddTitle(CharacterTitle.CertifiedGanksta);
                    break;
                case 50:
                    // +1 Aetheria Surge Level (costs 50 base HP) + Title + Item
                    player.SetProperty(PropertyInt.EnlightenmentAetheriaSurgeBonus, 1);
                    player.Vitals[PropertyAttribute2nd.MaxHealth].StartingValue -= 50;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    player.SendMessage("You have sacrificed 50 HP to gain +1 Aetheria Surge Level! Your aetheria now surge as if they were 1 level higher.", ChatMessageType.Broadcast);
                    // Title: Defender of Dereth (retired)
                    player.AddTitle(CharacterTitle.DefenderofDereth);
                    // Item: Helm of 50th Journey
                    AwardEnlightenmentItem(player, 53370013, "Helm of 50th Journey");
                    break;
                case 60:
                    // Title: Blood Warrior (retired)
                    player.AddTitle(CharacterTitle.BloodWarrior);
                    break;
                case 75:
                    // +1 Spell Chain: War magic spells chain to a nearby target for 30% damage
                    player.SetProperty(PropertyInt.EnlightenmentSpellChainBonus, 1);
                    player.SendMessage("You have gained +1 Spell Chain! Your war magic spells now chain to a nearby target for 30% damage.", ChatMessageType.Broadcast);
                    // Title: Guardian of Dereth + Item
                    player.AddTitle(CharacterTitle.GuardianofDereth);
                    // Item: Robe of 75th Rebirth (Envoy Robe Tailor)
                    AwardEnlightenmentItem(player, 53370012, "Robe of 75th Rebirth");
                    break;
                case 100:
                    // Title: Warlord of Dereth + Item
                    player.AddTitle(CharacterTitle.WarlordofDereth);
                    // Item: Shield of Enlightenment (Envoy Tailor Shield)
                    AwardEnlightenmentItem(player, 53370011, "Shield of Enlightenment");
                    break;
            }

            var msg = $"{player.Name} has achieved the {lvl} level of Enlightenment!";
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            DiscordChatManager.SendDiscordMessage(player.Name, msg, ConfigManager.Config.Chat.GeneralChannelId);
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);

            // CONQUEST: Enlightenment bonuses (+1% XP, +1 stats, +1 DR/DMG per 25) are applied dynamically
            // See comments at the top of AddPerks method for integration locations
        }

        /// <summary>
        /// Awards an enlightenment milestone item to the player
        /// </summary>
        private static void AwardEnlightenmentItem(Player player, uint wcid, string itemName)
        {
            var wo = WorldObjectFactory.CreateNewWorldObject(wcid);
            if (wo == null)
            {
                player.SendMessage($"Error creating {itemName}. Please contact an administrator.", ChatMessageType.Broadcast);
                return;
            }

            if (player.TryCreateInInventoryWithNetworking(wo))
            {
                player.SendMessage($"You have been awarded: {wo.Name}!", ChatMessageType.Broadcast);
                PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{player.Name} has earned the {wo.Name} for reaching Enlightenment {player.Enlightenment}!", ChatMessageType.WorldBroadcast));
            }
            else
            {
                // Inventory full - try to drop at player's feet
                wo.Location = player.Location.InFrontOf(1.0f);
                wo.EnterWorld();
                player.SendMessage($"Your inventory was full. {wo.Name} has been placed at your feet.", ChatMessageType.Broadcast);
            }
        }
    }
}

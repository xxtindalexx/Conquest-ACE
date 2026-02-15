using ACE.Common.Extensions;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Linq;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        // CONQUEST: Constants for dynamic XP calculation past level 275
        public const long xp275 = 191226310247;          // Total XP needed to reach level 275
        public const long xp274to275delta = 3390451400;  // XP difference from 274 to 275
        public const double levelRatio = 0.014234603;    // 1.42% growth per level past 275

        /// <summary>
        /// A player earns XP through natural progression, ie. kills and quests completed
        /// </summary>
        /// <param name="amount">The amount of XP being added</param>
        /// <param name="xpType">The source of XP being added</param>
        /// <param name="shareable">True if this XP can be shared with Fellowship</param>
        public void EarnXP(long amount, XpType xpType, ShareType shareType = ShareType.All, bool isArena = false)
        {
            //Console.WriteLine($"{Name}.EarnXP({amount}, {sharable}, {fixedAmount})");

            // CONQUEST: Mules cannot earn XP
            if (IsMule)
                return;

            // apply xp modifiers.  Quest XP is multiplicative with general XP modification
            var questModifier = PropertyManager.GetDouble("quest_xp_modifier");
            var modifier = PropertyManager.GetDouble("xp_modifier");
            if (xpType == XpType.Quest)
                modifier *= questModifier;

            // CONQUEST: Fellowship XP Sharing - share BASE amount only (no personal bonuses)
            // This prevents "bonus gatekeeping" where high-bonus players kick low-bonus players
            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship))
            {
                // Apply only server modifiers, not personal bonuses
                var baseAmount = (long)Math.Round(amount * modifier);

                if (baseAmount < 0)
                {
                    Console.WriteLine($"{Name}.EarnXP({amount}, {shareType}) - Fellowship - base amount negative");
                    return;
                }

                // Share the base amount - each member will apply their own bonuses
                GrantXP(baseAmount, xpType, shareType);
                return;
            }

            // Solo player or non-shareable XP - apply personal bonuses
            var enchantment = GetXPAndLuminanceModifier(xpType);

            // CONQUEST: Quest Bonus System - account-wide quest completion bonus
            var questBonus = GetQuestCountXPBonus();

            // CONQUEST: PK Dungeon Bonus - +10% XP/Lum in PK-only dungeons
            var pkDungeonBonus = GetPKDungeonBonus();

            var m_amount = (long)Math.Round(amount * enchantment * modifier * questBonus * pkDungeonBonus);

            if (m_amount < 0)
            {
                Console.WriteLine($"{Name}.EarnXP({amount}, {shareType})");
                Console.WriteLine($"modifier: {modifier}, enchantment: {enchantment}, m_amount: {m_amount}");
                return;
            }

            // CONQUEST: Show XP breakdown if player has enabled it via /xpbreakdown command
            // Include Fellowship XP type so players in fellowships can also see the breakdown
            if (ShowXpBreakdown && (xpType == XpType.Kill || xpType == XpType.Quest || xpType == XpType.Fellowship))
            {
                var bonusXP = m_amount - amount;
                var questBonusPercent = (questBonus - 1.0) * 100;
                var pkBonusPercent = (pkDungeonBonus - 1.0) * 100;

                // Separate enlightenment and equipment bonuses
                var enlightenmentBonusPercent = Enlightenment * 1.0; // +1% per enlightenment level
                var equipmentBonusPercent = EnchantmentManager.GetXPBonus() * 100; // Equipment XP bonus
                // Augmentation bonus (5% per aug, kills only)
                var augBonusXp = AugmentationBonusXp;
                var augBonusPercent = (xpType == XpType.Kill) ? (augBonusXp * 5.0) : 0.0;

                var xpSource = xpType == XpType.Fellowship ? "Fellowship" : (xpType == XpType.Quest ? "Quest" : "Kill");
                Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"XP Breakdown ({xpSource}): {amount:N0} base → {m_amount:N0} total (+{bonusXP:N0} bonus)\n" +
                    $"Modifiers: Quest {questBonusPercent:F2}% | PK {pkBonusPercent:F0}% | ENL {enlightenmentBonusPercent:F0}% | Aug {augBonusPercent:F0}% | Equip {equipmentBonusPercent:F0}%",
                    ChatMessageType.Broadcast));
            }

            GrantXP(m_amount, xpType, shareType);
        }

        /// <summary>
        /// Directly grants XP to the player, without the XP modifier
        /// </summary>
        /// <param name="amount">The amount of XP to grant to the player</param>
        /// <param name="xpType">The source of the XP being granted</param>
        /// <param name="shareable">If TRUE, this XP can be shared with fellowship members</param>
        public void GrantXP(long amount, XpType xpType, ShareType shareType = ShareType.All)
        {
            // DEBUG: Log GrantXP entry for troubleshooting vitae issues
            if (HasVitae)
                //Console.WriteLine($"[VITAE DEBUG] {Name}: GrantXP entry - amount={amount:N0}, xpType={xpType}, shareType={shareType}, HasVitae=true");

            if (IsOlthoiPlayer || IsMule)
            {
                if (HasVitae)
                    UpdateXpVitae(amount);

                return;
            }

            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship))
            {
                // this will divy up the XP, and re-call this function
                // with ShareType.Fellowship removed
                Fellowship.SplitXp((ulong)amount, xpType, shareType, this);
                return;
            }

            // CONQUEST: Apply personal bonuses to fellowship share
            // When receiving a fellowship share, apply the recipient's personal bonuses
            // (enchantment, quest count, PK dungeon) but NOT server modifiers (already applied)
            var enchantment = GetXPAndLuminanceModifier(xpType);
            var questBonus = GetQuestCountXPBonus();
            var pkDungeonBonus = GetPKDungeonBonus();

            var bonusedAmount = (long)Math.Round(amount * enchantment * questBonus * pkDungeonBonus);

            // Make sure UpdateXpAndLevel is done on this players thread
            EnqueueAction(new ActionEventDelegate(ActionType.PlayerXp_UpdateXpAndLevel, () => UpdateXpAndLevel(bonusedAmount, xpType)));

            // for passing XP up the allegiance chain,
            // this function is only called at the very beginning, to start the process.
            if (shareType.HasFlag(ShareType.Allegiance))
                UpdateXpAllegiance(amount);

            // only certain types of XP are granted to items
            if (xpType == XpType.Kill || xpType == XpType.Quest)
                GrantItemXP(amount);
        }

        /// <summary>
        /// Adds XP to a player's total XP, handles triggers (vitae, level up)
        /// </summary>
        private void UpdateXpAndLevel(long amount, XpType xpType)
        {
            // until we are max level we must make sure that we send
            var xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();
            // CONQUEST: Use dynamic XP calculation for level 300 instead of retail max (275)
            // Use Math.Ceiling to round up (avoid truncation issues with fractional XP)
            var maxLevelXp = (long)Math.Ceiling(GenerateDynamicLevelPostMax((int?)maxLevel));

            if (Level != maxLevel)
            {
                var addAmount = amount;

                var amountLeftToEnd = (long)maxLevelXp - (TotalExperience ?? 0);
                if (amount > amountLeftToEnd)
                    addAmount = amountLeftToEnd;

               /* if (Level >= 299)
                {
                    Console.WriteLine($"[XP GRANT DEBUG] Level {Level}: Granting {amount:N0} XP");
                    Console.WriteLine($"[XP GRANT DEBUG] TotalXP={TotalExperience:N0}, MaxLevelXP={maxLevelXp:N0}, AmountLeftToEnd={amountLeftToEnd:N0}");
                    Console.WriteLine($"[XP GRANT DEBUG] Final addAmount={addAmount:N0} XP");
                }*/

                AvailableExperience += addAmount;
                TotalExperience += addAmount;

                var xpTotalUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.TotalExperience, TotalExperience ?? 0);
                var xpAvailUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
                Session.Network.EnqueueSend(xpTotalUpdate, xpAvailUpdate);

                CheckForLevelup();
            }

            if (xpType == XpType.Quest)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} experience.", ChatMessageType.Broadcast));

            // Check vitae directly via GetVitae() to bypass potential caching issues
            // HasVitae uses EnchantmentManagerWithCaching which can return stale values
            var vitaeEntry = EnchantmentManager.GetVitae();
            if (vitaeEntry != null && xpType != XpType.Allegiance)
                UpdateXpVitae(amount);
        }

        /// <summary>
        /// Optionally passes XP up the Allegiance tree
        /// </summary>
        private void UpdateXpAllegiance(long amount)
        {
            if (!HasAllegiance) return;

            AllegianceManager.PassXP(AllegianceNode, (ulong)amount, true);
        }

        /// <summary>
        /// Handles updating the vitae penalty through earned XP
        /// </summary>
        /// <param name="amount">The amount of XP to apply to the vitae penalty</param>
        private void UpdateXpVitae(long amount)
        {
            var vitae = EnchantmentManager.GetVitae();

            if (vitae == null)
            {
                log.Error($"{Name}.UpdateXpVitae({amount}) vitae null, likely due to cross-thread operation or corrupt EnchantmentManager cache. Please report this.");
                log.Error(Environment.StackTrace);
                return;
            }

            var vitaePenalty = vitae.StatModValue;
            var startPenalty = vitaePenalty;

            var maxPool = (int)VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            var curPool = VitaeCpPool + amount;

            // DEBUG: Log vitae update progress
            //Console.WriteLine($"[VITAE DEBUG] {Name}: UpdateXpVitae - penalty={((1-vitaePenalty)*100):F1}%, DeathLevel={DeathLevel}, VitaeCpPool={VitaeCpPool:N0}, adding={amount:N0}, newPool={curPool:N0}, threshold={maxPool:N0}");
            while (curPool >= maxPool)
            {
                curPool -= maxPool;
                vitaePenalty = EnchantmentManager.ReduceVitae();
                if (vitaePenalty == 1.0f)
                    break;
                maxPool = (int)VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            }
            VitaeCpPool = (int)curPool;

            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.VitaeCpPool, VitaeCpPool.Value));

            if (vitaePenalty != startPenalty)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your experience has reduced your Vitae penalty!", ChatMessageType.Magic));
                EnchantmentManager.SendUpdateVitae();
            }

            if (vitaePenalty.EpsilonEquals(1.0f) || vitaePenalty > 1.0f)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(2.0f);
                actionChain.AddAction(this, ActionType.PlayerXp_RemoveVitae, () =>
                {
                    var vitae = EnchantmentManager.GetVitae();
                    if (vitae != null)
                    {
                        var curPenalty = vitae.StatModValue;
                        if (curPenalty.EpsilonEquals(1.0f) || curPenalty > 1.0f)
                            EnchantmentManager.RemoveVitae();
                    }
                });
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Returns the maximum possible character level
        /// /// CONQUEST: Capped at 300 instead of retail 275
        /// </summary>
        public static uint GetMaxLevel()
        {
            return 300; // CONQUEST: Max level is 300
        }

        /// <summary>
        /// Returns TRUE if player >= MaxLevel
        /// </summary>
        public bool IsMaxLevel => Level >= GetMaxLevel();

        /// <summary>
        /// Returns the remaining XP required to reach a level
        /// /// CONQUEST: Supports levels 276-300 using dynamic XP formula
        /// </summary>
        public long? GetRemainingXP(int level)
        {
            var maxLevel = GetMaxLevel();
            if (level < 1 || level > maxLevel)
                return null;

            // CONQUEST: For levels past 275, use dynamic calculation
            if (level > 275)
            {
                return (long)(GenerateDynamicLevelPostMax(level) - (TotalExperience ?? 0));
            }
            else
            {
                var levelTotalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[(int)level];
                return (long)levelTotalXP - (TotalExperience ?? 0);
            }
        }

        /// <summary>
        /// Returns the remaining XP required to the next level
        /// /// CONQUEST: Supports levels 276-300 using dynamic XP formula
        /// </summary>
        public ulong GetRemainingXP()
        {
            var maxLevel = GetMaxLevel();
            if (Level >= maxLevel)
                return 0;

            // CONQUEST: For levels past 275, use dynamic calculation
            if (Level >= 275)
            {
                return (ulong)(GenerateDynamicLevelPostMax(Level.Value + 1) - (TotalExperience ?? 0));
            }

            var nextLevelTotalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[Level.Value + 1];
            return nextLevelTotalXP - (ulong)(TotalExperience ?? 0);
        }

        /// <summary>
        /// Returns the total XP required to reach a level
        /// </summary>
        public static ulong GetTotalXP(int level)
        {
            var maxLevel = GetMaxLevel();
            if (level < 0 || level > maxLevel)
                return 0;

            return DatManager.PortalDat.XpTable.CharacterLevelXPList[level];
        }

        /// <summary>
        /// Returns the total amount of XP required for a player reach max level
        /// </summary>
        public static long MaxLevelXP
        {
            get
            {
                var xpTable = DatManager.PortalDat.XpTable.CharacterLevelXPList;

                return (long)xpTable[xpTable.Count - 1];
            }
        }

        /// <summary>
        /// Returns the XP required to go from level A to level B
        /// </summary>
        public ulong GetXPBetweenLevels(int levelA, int levelB)
        {
            // special case for max level
            var maxLevel = (int)GetMaxLevel();

            levelA = Math.Clamp(levelA, 1, maxLevel - 1);
            levelB = Math.Clamp(levelB, 1, maxLevel);

            double levelA_totalXP;
            double levelB_totalXP;

            // CONQUEST: Use dynamic XP calculation for levels > 275
            if (levelA > 275)
                levelA_totalXP = GenerateDynamicLevelPostMax(levelA);
            else
                levelA_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelA];

            if (levelB > 275)
                levelB_totalXP = GenerateDynamicLevelPostMax(levelB);
            else
                levelB_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelB];

            return (ulong)(levelB_totalXP - levelA_totalXP);
        }

        public ulong GetXPToNextLevel(int level)
        {
            return GetXPBetweenLevels(level, level + 1);
        }

        /// <summary>
        /// Determines if the player has advanced a level
        /// /// CONQUEST: Supports levels 276-300 using dynamic XP formula
        /// </summary>
        private void CheckForLevelup()
        {
            var xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();

            if (Level >= maxLevel) return;

            var startingLevel = Level;
            bool creditEarned = false;

            // DEBUG: Check XP values at level 299
           /* if (Level >= 299)
            {
                var xpNeeded = GenerateDynamicLevelPostMax(Level + 1);
                Console.WriteLine($"[LEVELUP CHECK] Level {Level}: Current XP = {TotalExperience:N0}, XP Needed for {Level + 1} = {xpNeeded:N0}");
                Console.WriteLine($"[LEVELUP CHECK] Comparison: {(double)(TotalExperience ?? 0)} > {xpNeeded} = {(double)(TotalExperience ?? 0) > xpNeeded}");
            }*/

            // CONQUEST: increases until the correct level is found
            // Supports both retail levels (1-275) and extended levels (276-300)
            while (
                (Level < 275 && (ulong)(TotalExperience ?? 0) >= xpTable.CharacterLevelXPList[(Level ?? 0) + 1]) // Retail levels
                || (Level >= 275 && (double)(TotalExperience ?? 0) > GenerateDynamicLevelPostMax(Level + 1)) // Extended levels
            )
            {
                /*// DEBUG: Log XP check for levels near 300
                if (Level >= 298)
                {
                    var xpNeeded = GenerateDynamicLevelPostMax(Level + 1);
                    Console.WriteLine($"[LEVELUP DEBUG] Level {Level} -> {Level + 1}: Current XP = {TotalExperience:N0}, XP Needed = {xpNeeded:N0}, Difference = {((double)(TotalExperience ?? 0) - xpNeeded):N0}");
                }*/
                Level++;
                // CONQUEST: increase the skill credits
                if (Level <= 274)
                {
                    // Retail: use XP table for skill credits (levels 1-274)
                    if (xpTable.CharacterLevelSkillCreditList[Level ?? 0] > 0)
                    {
                        AvailableSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                        TotalSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                        creditEarned = true;
                    }
                }
                else
                {
                    // CONQUEST: levels 275+, award 1 skill credit every 5 levels
                    if (Level % 5 == 0)
                    {
                        AvailableSkillCredits++;
                        TotalSkillCredits++;
                        creditEarned = true;
                    }
                }

                // break if we reach max
                if (Level == maxLevel)
                {
                    PlayParticleEffect(PlayScript.WeddingBliss, Guid);
                    break;
                }
            }

            if (Level > startingLevel)
            {
                var message = (Level == maxLevel) ? $"You have reached the maximum level of {Level}!" : $"You are now level {Level}!";

                message += (AvailableSkillCredits > 0) ? $"\nYou have {AvailableExperience:#,###0} experience points and {AvailableSkillCredits} skill credits available to raise skills and attributes." : $"\nYou have {AvailableExperience:#,###0} experience points available to raise skills and attributes.";

                var levelUp = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Level, Level ?? 1);
                var currentCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);

                if (Level != maxLevel && !creditEarned)
                {
                    var nextLevelWithCredits = 0;

                    // CONQUEST: Handle next skill credit message for both retail and extended levels
                    for (int i = (Level ?? 0) + 1; i <= maxLevel; i++)
                    {
                        if (i <= 275 && xpTable.CharacterLevelSkillCreditList[i] > 0)
                        {
                            nextLevelWithCredits = i;
                            break;
                        }
                        else if (i > 275 && i % 5 == 0)
                        {
                            nextLevelWithCredits = i;
                            break;
                        }
                    }
                    if (nextLevelWithCredits > 0)
                        message += $"\nYou will earn another skill credit at level {nextLevelWithCredits}.";
                }

                if (Fellowship != null)
                    Fellowship.OnFellowLevelUp(this);

                // CONQUEST: Removed OnLevelUp call - no longer needed since we allow XP passup regardless of level
                //if (AllegianceNode != null)
                //    AllegianceNode.OnLevelUp();

                Session.Network.EnqueueSend(levelUp);

                SetMaxVitals();

                // play level up effect
                PlayParticleEffect(PlayScript.LevelUp, Guid);

                Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Advancement), currentCredits);
            }
        }

        /// <summary>
        /// Spends the amount of XP specified, deducting it from available experience
        /// </summary>
        public bool SpendXP(long amount, bool sendNetworkUpdate = true)
        {
            if (amount > AvailableExperience)
                return false;

            AvailableExperience -= amount;

            if (sendNetworkUpdate)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0));

            return true;
        }

        /// <summary>
        /// Tries to spend all of the players Xp into Attributes, Vitals and Skills
        /// </summary>
        public void SpendAllXp(bool sendNetworkUpdate = true)
        {
            SpendAllAvailableAttributeXp(Strength, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Endurance, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Coordination, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Quickness, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Focus, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Self, sendNetworkUpdate);

            SpendAllAvailableVitalXp(Health, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Stamina, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Mana, sendNetworkUpdate);

            foreach (var skill in Skills)
            {
                if (skill.Value.AdvancementClass >= SkillAdvancementClass.Trained)
                    SpendAllAvailableSkillXp(skill.Value, sendNetworkUpdate);
            }
        }

        /// <summary>
        /// Gives available XP of the amount specified, without increasing total XP
        /// </summary>
        public void RefundXP(long amount)
        {
            AvailableExperience += amount;

            var xpUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
            Session.Network.EnqueueSend(xpUpdate);
        }

        public void HandleMissingXp()
        {
            var verifyXp = GetProperty(PropertyInt64.VerifyXp) ?? 0;
            if (verifyXp == 0) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, ActionType.PlayerXp_HandleMissingXp, () =>
            {
                var xpType = verifyXp > 0 ? "unassigned experience" : "experience points";

                var msg = $"This character was missing some {xpType} --\nYou have gained an additional {Math.Abs(verifyXp).ToString("N0")} {xpType}!";

                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                if (verifyXp < 0)
                {
                    // add to character's total XP
                    TotalExperience -= verifyXp;

                    CheckForLevelup();
                }

                RemoveProperty(PropertyInt64.VerifyXp);
            });

            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Returns the total amount of XP required to go from vitae to vitae + 0.01
        /// </summary>
        /// <param name="vitae">The current player life force, ie. 0.95f vitae = 5% penalty</param>
        /// <param name="level">The player DeathLevel, their level on last death</param>
        private double VitaeCPPoolThreshold(float vitae, int level)
        {
            return (Math.Pow(level, 2.5) * 2.5 + 20.0) * Math.Pow(vitae, 5.0) + 0.5;
        }

        /// <summary>
        /// Raise the available XP by a percentage of the current level XP or a maximum
        /// </summary>
        public void GrantLevelProportionalXp(double percent, long min, long max, bool isArena = false)
        {
            var nextLevelXP = GetXPBetweenLevels(Level.Value, Level.Value + 1);

            var scaledXP = (long)Math.Round(nextLevelXP * percent);

            if (max > 0)
                scaledXP = Math.Min(scaledXP, max);

            if (min > 0)
                scaledXP = Math.Max(scaledXP, min);

            // apply xp modifiers?
            EarnXP(scaledXP, XpType.Quest, ShareType.Allegiance, isArena);
        }

        /// <summary>
        /// The player earns XP for items that can be leveled up
        /// by killing creatures and completing quests,
        /// while those items are equipped.
        /// </summary>
        public void GrantItemXP(long amount)
        {
            foreach (var item in EquippedObjects.Values.Where(i => i.HasItemLevel))
                GrantItemXP(item, amount);
        }

        public void GrantItemXP(WorldObject item, long amount)
        {
            var prevItemLevel = item.ItemLevel.Value;
            var addItemXP = item.AddItemXP(amount);

            if (addItemXP > 0)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(item, PropertyInt64.ItemTotalXp, item.ItemTotalXp.Value));

            // handle item leveling up
            var newItemLevel = item.ItemLevel.Value;
            if (newItemLevel > prevItemLevel)
            {
                OnItemLevelUp(item, prevItemLevel);

                var actionChain = new ActionChain();
                actionChain.AddAction(this, ActionType.PlayerXp_ItemIncreasedInPower, () =>
                {
                    var msg = $"Your {item.Name} has increased in power to level {newItemLevel}!";
                    Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.AetheriaLevelUp));
                });
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Returns the multiplier to XP and Luminance from Trinkets and Augmentations
        /// </summary>
        public float GetXPAndLuminanceModifier(XpType xpType)
        {
            var enchantmentBonus = EnchantmentManager.GetXPBonus();

            var augBonus = 0.0f;
            if (xpType == XpType.Kill && AugmentationBonusXp > 0)
                augBonus = AugmentationBonusXp * 0.05f;

            // CONQUEST: Add enlightenment XP bonus (+1% per enlightenment level)
            // At ENL 100, this gives +100% XP (double XP)
            var enlightenmentBonus = 0.0f;
            if (Enlightenment > 0)
                enlightenmentBonus = Enlightenment * 0.01f;

            var modifier = 1.0f + enchantmentBonus + augBonus + enlightenmentBonus;
            //Console.WriteLine($"XPAndLuminanceModifier: {modifier}");

            return modifier;
        }

        /// <summary>
        /// CONQUEST: Quest Bonus System
        /// Reads from the quest completion count property to get the running XP bonus
        /// Formula: 1 + (quest_count * 0.0001)
        /// 10% XP bonus per 1,000 quests (0.01% per quest)
        /// Example: 5,000 QB = 50% XP bonus
        /// </summary>
        public double GetQuestCountXPBonus()
        {
            const double questToBonusRatio = 0.0001; // 0.01% per quest = 10% per 1,000 quests
            return 1.0 + (this.QuestCompletionCount ?? 0) * questToBonusRatio;
        }

        /// <summary>
        /// CONQUEST: PK Dungeon Bonus System
        /// Returns a 10% XP/Luminance bonus for PK players in PK-only dungeon variants
        /// </summary>
        public double GetPKDungeonBonus()
        {
            // Check if player is PK and in a PK-only dungeon variant
            if (PlayerKillerStatus == PlayerKillerStatus.PK &&
                CurrentLandblock != null &&
                Location != null)
            {
                var currentLandblock = (ushort)CurrentLandblock.Id.Landblock;
                var currentVariation = Location.Variation ?? 0;

                if (Landblock.pkDungeonLandblocks.Contains((currentLandblock, currentVariation)))
                {
                    return 1.1; // 10% bonus
                }
            }
            return 1.0; // No bonus
        }

        /// <summary>
        /// CONQUEST: Calculates total XP needed for levels past 275
        /// Uses progressive formula: each level requires 1.42% more XP than previous
        /// </summary>
        /// <param name="targetLevel">The level to calculate XP for (must be > 275)</param>
        /// <returns>Total XP required to reach the target level</returns>
        public static double GenerateDynamicLevelPostMax(int? targetLevel)
        {
            if (!targetLevel.HasValue || targetLevel.Value <= 275)
                return xp275;

            int target = targetLevel.Value;

            double nextXpDelta = (xp274to275delta + (xp274to275delta * levelRatio));
            double prevXpDelta = xp274to275delta;
            double nextXpCost = xp275;

            //Console.WriteLine($"[XP CALC DEBUG] Calculating XP for level {target}");
            //Console.WriteLine($"[XP CALC DEBUG] Starting: nextXpCost={nextXpCost:N0}, nextXpDelta={nextXpDelta:N0}");

            int iterations = 0;
            for (int i = 275; i < target; i++)
            {
                nextXpDelta = (prevXpDelta + (prevXpDelta * levelRatio));
                nextXpCost += nextXpDelta;
                prevXpDelta = nextXpDelta;
                iterations++;
            }

            //Console.WriteLine($"[XP CALC DEBUG] Iterations: {iterations}, Final nextXpCost={nextXpCost:N0}");

            return nextXpCost;
        }
    }
}

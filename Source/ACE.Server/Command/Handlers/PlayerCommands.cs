using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;
using System;
using System.Collections.Generic;


namespace ACE.Server.Command.Handlers
{
    public static class PlayerCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // pop
        [CommandHandler("pop", AccessLevel.Player, CommandHandlerFlag.None, 0,
            "Show current world population",
            "")]
        public static void HandlePop(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, $"Current world population: {PlayerManager.GetOnlineCount():N0}", ChatMessageType.Broadcast);
        }

        // quest info (uses GDLe formatting to match plugin expectations)
        [CommandHandler("myquests", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows your quest log")]
        public static void HandleQuests(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("quest_info_enabled").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"myquests\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var quests = session.Player.QuestManager.GetQuests();

            if (quests.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Quest list is empty.", ChatMessageType.Broadcast));
                return;
            }

            foreach (var playerQuest in quests)
            {
                var text = "";
                var questName = QuestManager.GetQuestName(playerQuest.QuestName);
                var quest = DatabaseManager.World.GetCachedQuest(questName);
                if (quest == null)
                {
                    //Console.WriteLine($"Couldn't find quest {playerQuest.QuestName}");
                    continue;
                }

                var minDelta = quest.MinDelta;
                if (QuestManager.CanScaleQuestMinDelta(quest))
                    minDelta = (uint)(quest.MinDelta * PropertyManager.GetDouble("quest_mindelta_rate").Item);

                text += $"{playerQuest.QuestName.ToLower()} - {playerQuest.NumTimesCompleted} solves ({playerQuest.LastTimeCompleted})";
                text += $"\"{quest.Message}\" {quest.MaxSolves} {minDelta}";

                session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));
            }
        }

        [CommandHandler("aug", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays your luminance augmentation levels")]
        public static void HandleAugmentations(Session session, params string[] parameters)
        {
            var player = session.Player;

            session.Network.EnqueueSend(new GameMessageSystemChat("=== Luminance Augmentation Levels ===", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Damage Rating: {player.LumAugDamageRating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Damage Reduction Rating: {player.LumAugDamageReductionRating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Critical Damage Rating: {player.LumAugCritDamageRating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Critical Reduction Rating: {player.LumAugCritReductionRating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Surge Chance Rating: {player.LumAugSurgeChanceRating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Healing Rating: {player.LumAugHealingRating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Item Mana Usage: {player.LumAugItemManaUsage}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Item Mana Gain: {player.LumAugItemManaGain}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Vitality: {player.LumAugVitality}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"All Skills: {player.LumAugAllSkills}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Skilled Craft: {player.LumAugSkilledCraft}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Skilled Spec: {player.LumAugSkilledSpec}", ChatMessageType.Broadcast));
        }

        [CommandHandler("augs", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays your luminance augmentation levels")]
        public static void HandleAugmentations2(Session session, params string[] parameters)
        {
            HandleAugmentations(session, parameters);
        }

        [CommandHandler("qb", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays your Quest Bonus count and XP bonus")]
        public static void HandleQuestBonus(Session session, params string[] parameters)
        {
            var player = session.Player;
            var questCount = player.QuestCompletionCount ?? 0;
            var xpBonus = player.GetQuestCountXPBonus();
            var bonusPercent = ((xpBonus - 1.0) * 100.0);

            session.Network.EnqueueSend(new GameMessageSystemChat("=== Quest Bonus (QB) ===", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Total Quests Completed: {questCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"XP Bonus: {bonusPercent:F2}%", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"(You gain {bonusPercent:F2}% extra XP from all sources)", ChatMessageType.Broadcast));
        }

        [CommandHandler("bonus", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays your Quest Bonus count and XP bonus")]
        public static void HandleQuestBonus2(Session session, params string[] parameters)
        {
            HandleQuestBonus(session, parameters);
        }

        [CommandHandler("top", AccessLevel.Player, CommandHandlerFlag.None, "Show current leaderboards", "use /top qb, /top level, /top enl, /top bank, or /top lum")]
        public static async void HandleTop(Session session, params string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("[TOP] Specify a leaderboard: /top qb, /top level, /top enl, /top bank, or /top lum", ChatMessageType.Broadcast));
                return;
            }

            List<Database.Models.Auth.Leaderboard> list = new List<Database.Models.Auth.Leaderboard>();
            var cache = Database.Models.Auth.LeaderboardCache.Instance;

            using (var context = new Database.Models.Auth.AuthDbContext())
            {
                var category = parameters[0]?.ToLower();

                switch (category)
                {
                    case "qb":
                        list = await cache.GetTopQBAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 50 Players by Quest Bonus:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "level":
                        list = await cache.GetTopLevelAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 50 Players by Level:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "enl":
                    case "enlightenment":
                        list = await cache.GetTopEnlAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 50 Players by Enlightenment:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "bank":
                        list = await cache.GetTopBankAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 50 Players by Banked Pyreals:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "lum":
                    case "luminance":
                        list = await cache.GetTopLumAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 50 Players by Banked Luminance:", ChatMessageType.Broadcast));
                        }
                        break;

                    default:
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[TOP] Unknown leaderboard '{category}'. Use: qb, level, enl, bank, or lum", ChatMessageType.Broadcast));
                        return;
                }

                // Display the leaderboard
                for (int i = 0; i < list.Count; i++)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{i + 1}: {list[i].Score:N0} - {list[i].Character}", ChatMessageType.Broadcast));
                }

                if (list.Count == 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("[TOP] No data available for this leaderboard yet.", ChatMessageType.Broadcast));
                }
            }
        }

        [CommandHandler("b", AccessLevel.Player, CommandHandlerFlag.None, "Handles Banking Operations", "")]
        public static void HandleBankShort(Session session, params string[] parameters)
        {
            if (parameters.Length == 0)
            {
                parameters = new string[] { "b" };
            }

            HandleBank(session, parameters);
        }

        [CommandHandler("bank", AccessLevel.Player, CommandHandlerFlag.None, "Handles Banking Operations", "")]
        public static void HandleBank(Session session, params string[] parameters)
        {
            if (session.Player == null)
                return;

            if (session.Player.IsOlthoiPlayer)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Bugs ain't got banks.", ChatMessageType.Broadcast));
                return;
            }

            if (parameters.Length == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Bank Commands:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit (or /b d) - Deposit all pyreals, luminance, and event tokens", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit pyreals 100 (or /b d p 100) - Deposit specific amount", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw pyreals 100 (or /b w p 100) - Withdraw 100 pyreals", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer pyreals 100 CharName - Transfer 100 pyreals to CharName", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank balance (or /b b) - View your bank balance", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"Currency types: pyreals (p), luminance (l), eventtokens (e)", ChatMessageType.System));
                return;
            }

            // Cleanup edge cases
            if (session.Player.BankedPyreals < 0) session.Player.BankedPyreals = 0;
            if (session.Player.BankedLuminance < 0) session.Player.BankedLuminance = 0;
            if (session.Player.EventTokens < 0) session.Player.EventTokens = 0;

            int iType = 0; // 0=all, 1=pyreals, 2=luminance, 3=eventtokens
            long amount = -1;
            string transferTargetName = "";

            if (parameters.Length >= 2)
            {
                if (parameters[1] == "pyreals" || parameters[1] == "p") iType = 1;
                else if (parameters[1] == "luminance" || parameters[1] == "l") iType = 2;
                else if (parameters[1] == "eventtokens" || parameters[1] == "e") iType = 3;
            }

            if (parameters.Length == 3 || parameters.Length == 4)
            {
                if (!long.TryParse(parameters[2], out amount))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid amount. Please provide a number.", ChatMessageType.System));
                    return;
                }
                if (amount <= 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Amount must be positive.", ChatMessageType.System));
                    return;
                }
            }

            if (parameters.Length == 4)
            {
                transferTargetName = parameters[3];
            }

            // DEPOSIT
            if (parameters[0] == "deposit" || parameters[0] == "d")
            {
                if (session.Player.IsBusy)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot deposit while busy. Complete your movement and try again!", ChatMessageType.System));
                    return;
                }

                if (parameters.Length == 1) // Deposit all
                {
                    session.Player.DepositPyreals();
                    session.Player.DepositLuminance();
                    session.Player.DepositEventTokens();
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all Pyreals, Luminance, and Event Tokens!", ChatMessageType.System));
                }
                else
                {
                    switch (iType)
                    {
                        case 1: // Pyreals
                            if (amount > 0)
                                session.Player.DepositPyreals(amount);
                            else
                            {
                                session.Player.DepositPyreals();
                                session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all pyreals!", ChatMessageType.System));
                            }
                            break;
                        case 2: // Luminance
                            if (amount > 0)
                                session.Player.DepositLuminance(amount);
                            else
                            {
                                session.Player.DepositLuminance();
                                session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all luminance!", ChatMessageType.System));
                            }
                            break;
                        case 3: // Event Tokens
                            session.Player.DepositEventTokens();
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all event tokens!", ChatMessageType.System));
                            break;
                    }
                }
            }

            // WITHDRAW
            else if (parameters[0] == "withdraw" || parameters[0] == "w")
            {
                switch (iType)
                {
                    case 1: // Withdraw pyreals
                        if (session.Player.BankedPyreals != null && amount > session.Player.BankedPyreals)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked pyreals.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to withdraw.", ChatMessageType.System));
                            break;
                        }
                        session.Player.WithdrawPyreals(amount);
                        break;
                    case 2: // Withdraw luminance
                        if (session.Player.BankedLuminance != null && amount > session.Player.BankedLuminance)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked luminance.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to withdraw.", ChatMessageType.System));
                            break;
                        }
                        if (amount + session.Player.AvailableLuminance > session.Player.MaximumLuminance)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot withdraw - would exceed maximum luminance.", ChatMessageType.System));
                            break;
                        }
                        session.Player.WithdrawLuminance(amount);
                        break;
                    case 3: // Withdraw event tokens
                        if (session.Player.EventTokens != null && amount > session.Player.EventTokens)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked event tokens.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to withdraw.", ChatMessageType.System));
                            break;
                        }
                        session.Player.WithdrawEventTokens(amount);
                        break;
                }
            }

            // TRANSFER
            else if (parameters[0] == "transfer" || parameters[0] == "t")
            {
                if (parameters.Length > 4)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Too many parameters. Use \"quotes\" around names with spaces.", ChatMessageType.System));
                    return;
                }

                switch (iType)
                {
                    case 1: // Transfer pyreals
                        if (session.Player.BankedPyreals != null && amount >= session.Player.BankedPyreals)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked pyreals to transfer.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to transfer.", ChatMessageType.System));
                            break;
                        }
                        if (session.Player.TransferPyreals(amount, transferTargetName))
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {amount:N0} Pyreal to {transferTargetName}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer failed: Pyreals to {transferTargetName}", ChatMessageType.System));
                        }
                        break;
                    case 2: // Transfer luminance
                        if (session.Player.BankedLuminance != null && amount >= session.Player.BankedLuminance)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked luminance to transfer.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to transfer.", ChatMessageType.System));
                            break;
                        }
                        if (session.Player.TransferLuminance(amount, transferTargetName))
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {amount:N0} Luminance to {transferTargetName}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer failed: Luminance to {transferTargetName}", ChatMessageType.System));
                        }
                        break;
                    case 3: // Transfer event tokens
                        if (session.Player.EventTokens != null && amount >= session.Player.EventTokens)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked event tokens to transfer.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to transfer.", ChatMessageType.System));
                            break;
                        }
                        if (session.Player.TransferEventTokens(amount, transferTargetName))
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Transferred {amount:N0} Event Tokens to {transferTargetName}", ChatMessageType.System));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer failed: Event Tokens to {transferTargetName}", ChatMessageType.System));
                        }
                        break;
                }
            }

            // BALANCE
            else if (parameters[0] == "balance" || parameters[0] == "b")
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Your balances:", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Pyreals: {session.Player.BankedPyreals:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Luminance: {session.Player.BankedLuminance:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Event Tokens (Dragon Coins): {session.Player.EventTokens:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Conquest Coins (non-transferable): {session.Player.ConquestCoins:N0}", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Soul Fragments (non-transferable): {session.Player.SoulFragments:N0}", ChatMessageType.System));
            }
        }

        [CommandHandler("pk", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Toggle PK status", "/pk on - Enable PK status\n/pk off - Disable PK status")]
        public static void HandlePK(Session session, params string[] parameters)
        {
            if (session.Player == null)
                return;

            if (parameters.Length == 0)
            {
                var status = session.Player.PlayerKillerStatus == PlayerKillerStatus.PK ? "ON" : "OFF";
                session.Network.EnqueueSend(new GameMessageSystemChat($"Your PK status is currently {status}. Use '/pk on' or '/pk off' to change.", ChatMessageType.Broadcast));
                return;
            }

            var param = parameters[0].ToLower();

            // /pk on
            if (param == "on" || param == "enable" || param == "1")
            {
                // Already PK?
                if (session.Player.PlayerKillerStatus == PlayerKillerStatus.PK)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You are already a player killer.", ChatMessageType.Broadcast));
                    return;
                }

                // Check 20-minute cooldown after PK death
                var lastPKDeath = session.Player.GetProperty(PropertyInt64.LastPKDeathTime) ?? 0;
                if (lastPKDeath > 0)
                {
                    var timeSinceDeath = Time.GetUnixTime() - lastPKDeath;
                    var cooldown = 1200; // 20 minutes in seconds

                    if (timeSinceDeath < cooldown)
                    {
                        var remainingMinutes = (int)Math.Ceiling((cooldown - timeSinceDeath) / 60.0);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You must wait {remainingMinutes} more minute{(remainingMinutes == 1 ? "" : "s")} before you can flag PK again after dying in PvP combat.", ChatMessageType.Broadcast));
                        return;
                    }
                }

                // Confirmation dialog
                if (parameters.Length == 1)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("=== WARNING ===", ChatMessageType.Broadcast));
                    session.Network.EnqueueSend(new GameMessageSystemChat("Enabling PK status will allow other PK players to attack you anywhere in the world!", ChatMessageType.Broadcast));
                    session.Network.EnqueueSend(new GameMessageSystemChat("Type '/pk on confirm' to proceed.", ChatMessageType.Broadcast));
                    return;
                }

                if (parameters.Length >= 2 && parameters[1].ToLower() == "confirm")
                {
                    session.Player.PlayerKillerStatus = PlayerKillerStatus.PK;
                    session.Player.SetProperty(PropertyInt64.LastPKFlagTime, (long)Time.GetUnixTime());
                    session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(session.Player, PropertyInt.PlayerKillerStatus, (int)session.Player.PlayerKillerStatus));

                    session.Network.EnqueueSend(new GameMessageSystemChat("You are now a Player Killer! Other PK players can attack you anywhere.", ChatMessageType.Broadcast));
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{session.Player.Name} is now a Player Killer!", ChatMessageType.Broadcast));
                }
            }
            // /pk off
            else if (param == "off" || param == "disable" || param == "0")
            {
                // Not PK?
                if (session.Player.PlayerKillerStatus != PlayerKillerStatus.PK)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You are not currently a player killer.", ChatMessageType.Broadcast));
                    return;
                }

                // Check if in PK-only landblock
                if (session.Player.CurrentLandblock != null)
                {
                    var landblockId = session.Player.CurrentLandblock.Id.Landblock;
                    var variation = session.Player.Location.Variation ?? 0;

                    if (ACE.Server.Entity.Landblock.pkDungeonLandblocks.Contains((landblockId, variation)))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("You cannot disable PK status while in a PK-only dungeon. Leave the dungeon first.", ChatMessageType.Broadcast));
                        return;
                    }
                }

                // Check 5-minute minimum PK duration
                var lastPKFlagTime = session.Player.GetProperty(PropertyInt64.LastPKFlagTime) ?? 0;
                if (lastPKFlagTime > 0)
                {
                    var timeSinceFlagged = Time.GetUnixTime() - lastPKFlagTime;
                    var minimumDuration = 300; // 5 minutes in seconds

                    if (timeSinceFlagged < minimumDuration)
                    {
                        var remainingSeconds = minimumDuration - timeSinceFlagged;
                        var remainingMinutes = (int)Math.Ceiling(remainingSeconds / 60.0);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You must remain PK for at least 5 minutes after flagging. {remainingMinutes} minute{(remainingMinutes == 1 ? "" : "s")} remaining.", ChatMessageType.Broadcast));
                        return;
                    }
                }

                // Check PK timer (cannot turn off during/after recent PK combat)
                if (session.Player.PKTimerActive)
                {
                    var pkTimer = PropertyManager.GetLong("pk_timer").Item;
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot disable PK status while your PK timer is active (lasts {pkTimer} seconds after PK combat).", ChatMessageType.Broadcast));
                    return;
                }

                // Turn off PK
                session.Player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(session.Player, PropertyInt.PlayerKillerStatus, (int)session.Player.PlayerKillerStatus));

                session.Network.EnqueueSend(new GameMessageSystemChat("You are no longer a Player Killer. You are now safe from PvP attacks.", ChatMessageType.Broadcast));
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid parameter. Use '/pk on' or '/pk off'.", ChatMessageType.Broadcast));
            }
        }

        /// <summary>
        /// For characters/accounts who currently own multiple houses, used to select which house they want to keep
        /// </summary>
        [CommandHandler("house-select", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "For characters/accounts who currently own multiple houses, used to select which house they want to keep")]
        public static void HandleHouseSelect(Session session, params string[] parameters)
        {
            HandleHouseSelect(session, false, parameters);
        }

        public static void HandleHouseSelect(Session session, bool confirmed, params string[] parameters)
        {
            if (!int.TryParse(parameters[0], out var houseIdx))
                return;

            // ensure current multihouse owner
            if (!session.Player.IsMultiHouseOwner(false))
            {
                log.Warn($"{session.Player.Name} tried to /house-select {houseIdx}, but they are not currently a multi-house owner!");
                return;
            }

            // get house info for this index
            var multihouses = session.Player.GetMultiHouses();

            if (houseIdx < 1 || houseIdx > multihouses.Count)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Please enter a number between 1 and {multihouses.Count}.", ChatMessageType.Broadcast));
                return;
            }

            var keepHouse = multihouses[houseIdx - 1];

            // show confirmation popup
            if (!confirmed)
            {
                var houseType = $"{keepHouse.HouseType}".ToLower();
                var loc = HouseManager.GetCoords(keepHouse.SlumLord.Location);

                var msg = $"Are you sure you want to keep the {houseType} at\n{loc}?";
                if (!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => HandleHouseSelect(session, true, parameters)), msg))
                    session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            // house to keep confirmed, abandon the other houses
            var abandonHouses = new List<House>(multihouses);
            abandonHouses.RemoveAt(houseIdx - 1);

            foreach (var abandonHouse in abandonHouses)
            {
                var house = session.Player.GetHouse(abandonHouse.Guid.Full);

                HouseManager.HandleEviction(house, house.HouseOwner ?? 0, true);
            }

            // set player properties for house to keep
            var player = PlayerManager.FindByGuid(keepHouse.HouseOwner ?? 0, out bool isOnline);
            if (player == null)
            {
                log.Error($"{session.Player.Name}.HandleHouseSelect({houseIdx}) - couldn't find HouseOwner {keepHouse.HouseOwner} for {keepHouse.Name} ({keepHouse.Guid})");
                return;
            }

            player.HouseId = keepHouse.HouseId;
            player.HouseInstance = keepHouse.Guid.Full;

            player.SaveBiotaToDatabase();

            // update house panel for current player
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(3.0f);  // wait for slumlord inventory biotas above to save
            actionChain.AddAction(session.Player, session.Player.HandleActionQueryHouse);
            actionChain.EnqueueChain();

            Console.WriteLine("OK");
        }

        [CommandHandler("debugcast", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows debug information about the current magic casting state")]
        public static void HandleDebugCast(Session session, params string[] parameters)
        {
            var physicsObj = session.Player.PhysicsObj;

            var pendingActions = physicsObj.MovementManager.MoveToManager.PendingActions;
            var currAnim = physicsObj.PartArray.Sequence.CurrAnim;

            session.Network.EnqueueSend(new GameMessageSystemChat(session.Player.MagicState.ToString(), ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"IsMovingOrAnimating: {physicsObj.IsMovingOrAnimating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"PendingActions: {pendingActions.Count}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"CurrAnim: {currAnim?.Value.Anim.ID:X8}", ChatMessageType.Broadcast));
        }

        [CommandHandler("fixcast", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Fixes magic casting if locked up for an extended time")]
        public static void HandleFixCast(Session session, params string[] parameters)
        {
            var magicState = session.Player.MagicState;

            if (magicState.IsCasting && DateTime.UtcNow - magicState.StartTime > TimeSpan.FromSeconds(5))
            {
                session.Network.EnqueueSend(new GameEventCommunicationTransientString(session, "Fixed casting state"));
                session.Player.SendUseDoneEvent();
                magicState.OnCastDone();
            }
        }

        [CommandHandler("castmeter", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows the fast casting efficiency meter")]
        public static void HandleCastMeter(Session session, params string[] parameters)
        {
            if (parameters.Length == 0)
            {
                session.Player.MagicState.CastMeter = !session.Player.MagicState.CastMeter;
            }
            else
            {
                if (parameters[0].Equals("on", StringComparison.OrdinalIgnoreCase))
                    session.Player.MagicState.CastMeter = true;
                else
                    session.Player.MagicState.CastMeter = false;
            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"Cast efficiency meter {(session.Player.MagicState.CastMeter ? "enabled" : "disabled")}", ChatMessageType.Broadcast));
        }

        private static List<string> configList = new List<string>()
        {
            "Common settings:\nConfirmVolatileRareUse, MainPackPreferred, SalvageMultiple, SideBySideVitals, UseCraftSuccessDialog",
            "Interaction settings:\nAcceptLootPermits, AllowGive, AppearOffline, AutoAcceptFellowRequest, DragItemOnPlayerOpensSecureTrade, FellowshipShareLoot, FellowshipShareXP, IgnoreAllegianceRequests, IgnoreFellowshipRequests, IgnoreTradeRequests, UseDeception",
            "UI settings:\nCoordinatesOnRadar, DisableDistanceFog, DisableHouseRestrictionEffects, DisableMostWeatherEffects, FilterLanguage, LockUI, PersistentAtDay, ShowCloak, ShowHelm, ShowTooltips, SpellDuration, TimeStamp, ToggleRun, UseMouseTurning",
            "Chat settings:\nHearAllegianceChat, HearGeneralChat, HearLFGChat, HearRoleplayChat, HearSocietyChat, HearTradeChat, HearPKDeaths, StayInChatMode",
            "Combat settings:\nAdvancedCombatUI, AutoRepeatAttack, AutoTarget, LeadMissileTargets, UseChargeAttack, UseFastMissiles, ViewCombatTarget, VividTargetingIndicator",
            "Character display settings:\nDisplayAge, DisplayAllegianceLogonNotifications, DisplayChessRank, DisplayDateOfBirth, DisplayFishingSkill, DisplayNumberCharacterTitles, DisplayNumberDeaths"
        };

        /// <summary>
        /// Mapping of GDLE -> ACE CharacterOptions
        /// </summary>
        private static Dictionary<string, string> translateOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common
            { "ConfirmVolatileRareUse", "ConfirmUseOfRareGems" },
            { "MainPackPreferred", "UseMainPackAsDefaultForPickingUpItems" },
            { "SalvageMultiple", "SalvageMultipleMaterialsAtOnce" },
            { "SideBySideVitals", "SideBySideVitals" },
            { "UseCraftSuccessDialog", "UseCraftingChanceOfSuccessDialog" },

            // Interaction
            { "AcceptLootPermits", "AcceptCorpseLootingPermissions" },
            { "AllowGive", "LetOtherPlayersGiveYouItems" },
            { "AppearOffline", "AppearOffline" },
            { "AutoAcceptFellowRequest", "AutomaticallyAcceptFellowshipRequests" },
            { "DragItemOnPlayerOpensSecureTrade", "DragItemToPlayerOpensTrade" },
            { "FellowshipShareLoot", "ShareFellowshipLoot" },
            { "FellowshipShareXP", "ShareFellowshipExpAndLuminance" },
            { "IgnoreAllegianceRequests", "IgnoreAllegianceRequests" },
            { "IgnoreFellowshipRequests", "IgnoreFellowshipRequests" },
            { "IgnoreTradeRequests", "IgnoreAllTradeRequests" },
            { "UseDeception", "AttemptToDeceiveOtherPlayers" },

            // UI
            { "CoordinatesOnRadar", "ShowCoordinatesByTheRadar" },
            { "DisableDistanceFog", "DisableDistanceFog" },
            { "DisableHouseRestrictionEffects", "DisableHouseRestrictionEffects" },
            { "DisableMostWeatherEffects", "DisableMostWeatherEffects" },
            { "FilterLanguage", "FilterLanguage" },
            { "LockUI", "LockUI" },
            { "PersistentAtDay", "AlwaysDaylightOutdoors" },
            { "ShowCloak", "ShowYourCloak" },
            { "ShowHelm", "ShowYourHelmOrHeadGear" },
            { "ShowTooltips", "Display3dTooltips" },
            { "SpellDuration", "DisplaySpellDurations" },
            { "TimeStamp", "DisplayTimestamps" },
            { "ToggleRun", "RunAsDefaultMovement" },
            { "UseMouseTurning", "UseMouseTurning" },

            // Chat
            { "HearAllegianceChat", "ListenToAllegianceChat" },
            { "HearGeneralChat", "ListenToGeneralChat" },
            { "HearLFGChat", "ListenToLFGChat" },
            { "HearRoleplayChat", "ListentoRoleplayChat" },
            { "HearSocietyChat", "ListenToSocietyChat" },
            { "HearTradeChat", "ListenToTradeChat" },
            { "HearPKDeaths", "ListenToPKDeathMessages" },
            { "StayInChatMode", "StayInChatModeAfterSendingMessage" },

            // Combat
            { "AdvancedCombatUI", "AdvancedCombatInterface" },
            { "AutoRepeatAttack", "AutoRepeatAttacks" },
            { "AutoTarget", "AutoTarget" },
            { "LeadMissileTargets", "LeadMissileTargets" },
            { "UseChargeAttack", "UseChargeAttack" },
            { "UseFastMissiles", "UseFastMissiles" },
            { "ViewCombatTarget", "KeepCombatTargetsInView" },
            { "VividTargetingIndicator", "VividTargetingIndicator" },

            // Character Display
            { "DisplayAge", "AllowOthersToSeeYourAge" },
            { "DisplayAllegianceLogonNotifications", "ShowAllegianceLogons" },
            { "DisplayChessRank", "AllowOthersToSeeYourChessRank" },
            { "DisplayDateOfBirth", "AllowOthersToSeeYourDateOfBirth" },
            { "DisplayFishingSkill", "AllowOthersToSeeYourFishingSkill" },
            { "DisplayNumberCharacterTitles", "AllowOthersToSeeYourNumberOfTitles" },
            { "DisplayNumberDeaths", "AllowOthersToSeeYourNumberOfDeaths" },
        };

        /// <summary>
        /// Manually sets a character option on the server. Use /config list to see a list of settings.
        /// </summary>
        [CommandHandler("config", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Manually sets a character option on the server.\nUse /config list to see a list of settings.", "<setting> <on/off>")]
        public static void HandleConfig(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("player_config_command").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"config\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            // /config list - show character options
            if (parameters[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in configList)
                    session.Network.EnqueueSend(new GameMessageSystemChat(line, ChatMessageType.Broadcast));

                return;
            }

            // translate GDLE CharacterOptions for existing plugins
            if (!translateOptions.TryGetValue(parameters[0], out var param) || !Enum.TryParse(param, out CharacterOption characterOption))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown character option: {parameters[0]}", ChatMessageType.Broadcast));
                return;
            }

            var option = session.Player.GetCharacterOption(characterOption);

            // modes of operation:
            // on / off / toggle

            // - if none specified, default to toggle
            var mode = "toggle";

            if (parameters.Length > 1)
            {
                if (parameters[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                    mode = "on";
                else if (parameters[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                    mode = "off";
            }

            // set character option
            if (mode.Equals("on"))
                option = true;
            else if (mode.Equals("off"))
                option = false;
            else
                option = !option;

            session.Player.SetCharacterOption(characterOption, option);

            session.Network.EnqueueSend(new GameMessageSystemChat($"Character option {parameters[0]} is now {(option ? "on" : "off")}.", ChatMessageType.Broadcast));

            // update client
            session.Network.EnqueueSend(new GameEventPlayerDescription(session));
        }

        /// <summary>
        /// Force resend of all visible objects known to this player. Can fix rare cases of invisible object bugs.
        /// Can only be used once every 5 mins max.
        /// </summary>
        [CommandHandler("objsend", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Force resend of all visible objects known to this player. Can fix rare cases of invisible object bugs. Can only be used once every 5 mins max.")]
        public static void HandleObjSend(Session session, params string[] parameters)
        {
            // a good repro spot for this is the first room after the door in facility hub
            // in the portal drop / staircase room, the VisibleCells do not have the room after the door
            // however, the room after the door *does* have the portal drop / staircase room in its VisibleCells (the inverse relationship is imbalanced)
            // not sure how to fix this atm, seems like it triggers a client bug..

            if (DateTime.UtcNow - session.Player.PrevObjSend < TimeSpan.FromMinutes(5))
            {
                session.Player.SendTransientError("You have used this command too recently!");
                return;
            }

            var creaturesOnly = parameters.Length > 0 && parameters[0].Contains("creature", StringComparison.OrdinalIgnoreCase);

            var knownObjs = session.Player.GetKnownObjects();

            foreach (var knownObj in knownObjs)
            {
                if (creaturesOnly && !(knownObj is Creature))
                    continue;

                session.Player.RemoveTrackedObject(knownObj, false);
                session.Player.TrackObject(knownObj);
            }
            session.Player.PrevObjSend = DateTime.UtcNow;
        }

        // show player ace server versions
        [CommandHandler("aceversion", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows this server's version data")]
        public static void HandleACEversion(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("version_info_enabled").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"aceversion\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var msg = ServerBuildInfo.GetVersionInfo();

            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
        }

        // reportbug < code | content > < description >
        [CommandHandler("reportbug", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 2,
            "Generate a Bug Report",
            "<category> <description>\n" +
            "This command generates a URL for you to copy and paste into your web browser to submit for review by server operators and developers.\n" +
            "Category can be the following:\n" +
            "Creature\n" +
            "NPC\n" +
            "Item\n" +
            "Quest\n" +
            "Recipe\n" +
            "Landblock\n" +
            "Mechanic\n" +
            "Code\n" +
            "Other\n" +
            "For the first three options, the bug report will include identifiers for what you currently have selected/targeted.\n" +
            "After category, please include a brief description of the issue, which you can further detail in the report on the website.\n" +
            "Examples:\n" +
            "/reportbug creature Drudge Prowler is over powered\n" +
            "/reportbug npc Ulgrim doesn't know what to do with Sake\n" +
            "/reportbug quest I can't enter the portal to the Lost City of Frore\n" +
            "/reportbug recipe I cannot combine Bundle of Arrowheads with Bundle of Arrowshafts\n" +
            "/reportbug code I was killed by a Non-Player Killer\n"
            )]
        public static void HandleReportbug(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("reportbug_enabled").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"reportbug\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var category = parameters[0];
            var description = "";

            for (var i = 1; i < parameters.Length; i++)
                description += parameters[i] + " ";

            description.Trim();

            switch (category.ToLower())
            {
                case "creature":
                case "npc":
                case "quest":
                case "item":
                case "recipe":
                case "landblock":
                case "mechanic":
                case "code":
                case "other":
                    break;
                default:
                    category = "Other";
                    break;
            }

            var sn = ConfigManager.Config.Server.WorldName;
            var c = session.Player.Name;

            var st = "ACE";

            //var versions = ServerBuildInfo.GetVersionInfo();
            var databaseVersion = DatabaseManager.World.GetVersion();
            var sv = ServerBuildInfo.FullVersion;
            var pv = databaseVersion.PatchVersion;

            //var ct = PropertyManager.GetString("reportbug_content_type").Item;
            var cg = category.ToLower();

            var w = "";
            var g = "";

            if (cg == "creature" || cg == "npc"|| cg == "item" || cg == "item")
            {
                var objectId = new ObjectGuid();
                if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
                {
                    if (session.Player.HealthQueryTarget.HasValue)
                        objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
                    else if (session.Player.ManaQueryTarget.HasValue)
                        objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
                    else
                        objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

                    //var wo = session.Player.CurrentLandblock?.GetObject(objectId);

                    var wo = session.Player.FindObject(objectId.Full, Player.SearchLocations.Everywhere);

                    if (wo != null)
                    {
                        w = $"{wo.WeenieClassId}";
                        g = $"0x{wo.Guid:X8}";
                    }
                }
            }

            var l = session.Player.Location.ToLOCString();

            var issue = description;

            var urlbase = $"https://www.accpp.net/bug?";

            var url = urlbase;
            if (sn.Length > 0)
                url += $"sn={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sn))}";
            if (c.Length > 0)
                url += $"&c={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(c))}";
            if (st.Length > 0)
                url += $"&st={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(st))}";
            if (sv.Length > 0)
                url += $"&sv={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sv))}";
            if (pv.Length > 0)
                url += $"&pv={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pv))}";
            //if (ct.Length > 0)
            //    url += $"&ct={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(ct))}";
            if (cg.Length > 0)
            {
                if (cg == "npc")
                    cg = cg.ToUpper();
                else
                    cg = char.ToUpper(cg[0]) + cg.Substring(1);
                url += $"&cg={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cg))}";
            }
            if (w.Length > 0)
                url += $"&w={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(w))}";
            if (g.Length > 0)
                url += $"&g={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(g))}";
            if (l.Length > 0)
                url += $"&l={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(l))}";
            if (issue.Length > 0)
                url += $"&i={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(issue))}";

            var msg = "\n\n\n\n";
            msg += "Bug Report - Copy and Paste the following URL into your browser to submit a bug report\n";
            msg += "-=-\n";
            msg += $"{url}\n";
            msg += "-=-\n";
            msg += "\n\n\n\n";

            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.AdminTell));
        }
    }
}

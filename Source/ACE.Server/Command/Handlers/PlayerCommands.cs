using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Database.Models.Log;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using Lifestoned.DataModel.DerethForever;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;


namespace ACE.Server.Command.Handlers
{
    public static class PlayerCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        [CommandHandler("fship", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Commands to handle fellowships aside from the UI", "")]
        public static void HandleFellowCommand(Session session, params string[] parameters)
        {
            if (parameters == null || parameters.Count() == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship add <name or targetted player>", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship landblock to invite all players in your landblock", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship remove <name or targetted player>", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship create <name> to create a fellowship", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship leave", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship disband", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: use /fship list to see all fellowships looking for members", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: tell any fellowship member 'xp' to join their fellowship", ChatMessageType.Broadcast));
                return;
            }

            if (parameters.Count() == 1)
            {
                if (parameters[0] == "list")
                {
                    // CONQUEST: List all fellowships that aren't full
                    var availableFellowships = new List<(string leaderName, string fellowName, int count, int max, string location)>();
                    var seenFellowships = new HashSet<uint>(); // Track by leader guid to avoid duplicates

                    foreach (var player in PlayerManager.GetAllOnline())
                    {
                        if (player.Fellowship != null && !player.Fellowship.IsLocked)
                        {
                            var fellowship = player.Fellowship;
                            var memberCount = fellowship.FellowshipMembers.Count;

                            // Only list if not full and we haven't already listed this fellowship
                            if (memberCount < Entity.Fellowship.MaxFellows && !seenFellowships.Contains(fellowship.FellowshipLeaderGuid))
                            {
                                seenFellowships.Add(fellowship.FellowshipLeaderGuid);
                                var leader = PlayerManager.GetOnlinePlayer(fellowship.FellowshipLeaderGuid);
                                var leaderName = leader?.Name ?? "Unknown";

                                // CONQUEST: Skip fellowships where any member is in a PK dungeon
                                bool isInPkDungeon = false;
                                foreach (var memberEntry in fellowship.FellowshipMembers)
                                {
                                    var member = PlayerManager.GetOnlinePlayer(memberEntry.Key);
                                    if (member?.CurrentLandblock != null)
                                    {
                                        var landblock = (ushort)member.CurrentLandblock.Id.Landblock;
                                        var variation = member.CurrentLandblock.VariationId ?? 0;
                                        if (Entity.Landblock.pkDungeonLandblocks.Contains((landblock, variation)))
                                        {
                                            isInPkDungeon = true;
                                            break;
                                        }
                                    }
                                }

                                if (!isInPkDungeon)
                                {
                                    // CONQUEST: Get location name from cached landblock names (loaded at startup)
                                    string locationName = "";
                                    if (leader?.CurrentLandblock != null)
                                    {
                                        var leaderLandblock = (ushort)leader.CurrentLandblock.Id.Landblock;
                                        var leaderVariant = leader.CurrentLandblock.VariationId ?? 0;
                                        locationName = Entity.Landblock.GetLandblockName(leaderLandblock);

                                        // Append variant number if not base variant (0)
                                        if (!string.IsNullOrEmpty(locationName) && leaderVariant > 0)
                                        {
                                            locationName += $" v{leaderVariant}";
                                        }
                                    }

                                    availableFellowships.Add((leaderName, fellowship.FellowshipName, memberCount, Entity.Fellowship.MaxFellows, locationName));
                                }
                            }
                        }
                    }

                    if (availableFellowships.Count == 0)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: No fellowships are currently looking for members.", ChatMessageType.Broadcast));
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Fellowships looking for members (tell any member 'xp' to join):", ChatMessageType.Broadcast));
                        foreach (var (leaderName, fellowName, count, max, location) in availableFellowships)
                        {
                            var locationDisplay = string.IsNullOrEmpty(location) ? "" : $" @ {location}";
                            session.Network.EnqueueSend(new GameMessageSystemChat($"  - {fellowName} (Leader: {leaderName}) [{count}/{max}]{locationDisplay}", ChatMessageType.Broadcast));
                        }
                    }
                    return;
                }

                // Debug command to see all fellowships and why they're filtered
                if (parameters[0] == "debug")
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP DEBUG]: Scanning all online players...", ChatMessageType.Broadcast));
                    var seenFellowships = new HashSet<uint>();

                    foreach (var player in PlayerManager.GetAllOnline())
                    {
                        if (player.Fellowship != null && !seenFellowships.Contains(player.Fellowship.FellowshipLeaderGuid))
                        {
                            seenFellowships.Add(player.Fellowship.FellowshipLeaderGuid);
                            var fellowship = player.Fellowship;
                            var leader = PlayerManager.GetOnlinePlayer(fellowship.FellowshipLeaderGuid);
                            var leaderName = leader?.Name ?? "Unknown";
                            var memberCount = fellowship.FellowshipMembers.Count;

                            string status = "VISIBLE";
                            string reason = "";

                            if (fellowship.IsLocked)
                            {
                                status = "HIDDEN";
                                reason = "Locked";
                            }
                            else if (memberCount >= Entity.Fellowship.MaxFellows)
                            {
                                status = "HIDDEN";
                                reason = "Full";
                            }
                            else
                            {
                                // Check PK dungeon
                                foreach (var memberEntry in fellowship.FellowshipMembers)
                                {
                                    var member = PlayerManager.GetOnlinePlayer(memberEntry.Key);
                                    if (member?.CurrentLandblock != null)
                                    {
                                        var lb = (ushort)member.CurrentLandblock.Id.Landblock;
                                        var var_ = member.CurrentLandblock.VariationId ?? 0;
                                        if (Entity.Landblock.pkDungeonLandblocks.Contains((lb, var_)))
                                        {
                                            status = "HIDDEN";
                                            reason = $"PK Dungeon (0x{lb:X4} v{var_}, member: {member.Name})";
                                            break;
                                        }
                                    }
                                }
                            }

                            session.Network.EnqueueSend(new GameMessageSystemChat($"  {fellowship.FellowshipName} (Leader: {leaderName}) [{memberCount}/{Entity.Fellowship.MaxFellows}] - {status} {reason}", ChatMessageType.Broadcast));
                        }
                    }
                    return;
                }

                if (parameters[0] == "landblock")
                {
                    if (session.Player.CurrentLandblock == null)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Your current landblock is not found, for some reason (logged)", ChatMessageType.Broadcast));
                        return;
                    }
                    if (session.Player.CurrentLandblock.Id.Landblock == 0x016C)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Your current landblock is in the Marketplace, and cannot be used to form landblock fellowships", ChatMessageType.Broadcast));
                        return;
                    }
                    bool currentPlayerOver50 = session.Player.Level >= 50;
                    foreach (var player in session.Player.CurrentLandblock.players)
                    {
                        if (player.Guid != session.Player.Guid && !player.IsMule && (player.CloakStatus == CloakStatus.Player || player.CloakStatus == CloakStatus.Off || player.CloakStatus == CloakStatus.Undef))
                        {
                            if (!currentPlayerOver50 || player.Level >= 50) // Don't add lowbies to a fellowship of players over 50
                            {
                                if (!session.Player.SquelchManager.Squelches.Contains(player, ChatMessageType.Tell))
                                {
                                    session.Player.FellowshipRecruit(player);
                                }
                            }
                        }
                    }
                    return;
                }

                if (parameters[0] == "leave")
                {
                    session.Player.Fellowship.QuitFellowship(session.Player, false);
                    return;
                }
                if (parameters[0] == "disband")
                {
                    session.Player.Fellowship.QuitFellowship(session.Player, true);
                    return;
                }
                if (parameters[0] == "add")
                {
                    var tPGuid = session.Player.CurrentAppraisalTarget;
                    if (tPGuid != null)
                    {
                        var tplayer = PlayerManager.GetOnlinePlayer(tPGuid.Value);
                        if (tplayer != null)
                        {
                            session.Player.FellowshipRecruit(tplayer);
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Target player is not online.", ChatMessageType.Broadcast));
                        }
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: No player targeted. Use /fship add <name> to add by name.", ChatMessageType.Broadcast));
                    }
                    return;
                }
                if (parameters[0] == "remove")
                {
                    var tPGuid = session.Player.CurrentAppraisalTarget;
                    if (tPGuid != null)
                    {
                        session.Player.FellowshipDismissPlayer(tPGuid.Value);
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: No player targeted. Use /fship remove <name> to remove by name.", ChatMessageType.Broadcast));
                    }
                    return;
                }
            }

            if (parameters.Count() == 2)
            {
                if (parameters[0] == "create")
                {
                    session.Player.FellowshipCreate(parameters[1], true);
                    return;
                }
                if (parameters[0] == "add")
                {
                    var tplayer = PlayerManager.GetOnlinePlayer(parameters[1]);
                    if (tplayer != null)
                    {
                        session.Player.FellowshipRecruit(tplayer);
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Player '{parameters[1]}' is not online.", ChatMessageType.Broadcast));
                    }
                    return;
                }
                if (parameters[0] == "remove")
                {
                    var tplayer = PlayerManager.GetOnlinePlayer(parameters[1]);
                    if (tplayer != null)
                    {
                        session.Player.FellowshipDismissPlayer(tplayer.Guid.Full);
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Player '{parameters[1]}' is not online.", ChatMessageType.Broadcast));
                    }
                    return;
                }
            }
        }

        // pop
        [CommandHandler("pop", AccessLevel.Player, CommandHandlerFlag.None, 0,
            "Show current world population",
            "")]
        public static void HandlePop(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, $"Current world population: {PlayerManager.GetOnlineCount():N0}", ChatMessageType.Broadcast);
        }

        // upop - admin command to show unique IP connections
        [CommandHandler("upop", AccessLevel.Sentinel, CommandHandlerFlag.None, 0,
            "Show unique IP connections vs total population",
            "")]
        public static void HandleUniquePop(Session session, params string[] parameters)
        {
            var onlinePlayers = PlayerManager.GetAllOnline();
            var totalCount = onlinePlayers.Count;
            var uniqueIPs = new HashSet<string>();

            foreach (var player in onlinePlayers)
            {
                if (player.Session?.EndPoint?.Address != null)
                {
                    uniqueIPs.Add(player.Session.EndPoint.Address.ToString());
                }
            }

            var uniqueCount = uniqueIPs.Count;
            var multiboxers = totalCount - uniqueCount;

            CommandHandlerHelper.WriteOutputInfo(session, $"Population: {totalCount:N0} total, {uniqueCount:N0} unique IPs, {multiboxers:N0} multiboxed", ChatMessageType.Broadcast);
        }

        /// <summary>
        /// Rate limiter for /passwd command
        /// </summary>
        private static readonly TimeSpan MyQuests = TimeSpan.FromSeconds(60);

        // quest info (uses GDLe formatting to match plugin expectations)
        [CommandHandler("myquests", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows your quest log")]
        public static void HandleQuests(Session session, params string[] parameters)
        {
            if (PropertyManager.GetBool("myquest_throttle_enabled"))
            {
                var currentTime = DateTime.UtcNow;

                if (currentTime - session.LastMyQuestsCommandTime < MyQuests)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"[MyQuests] This command may only be run once every {MyQuests.TotalSeconds} seconds.", ChatMessageType.Broadcast);
                    return;
                }
            }

            session.LastMyQuestsCommandTime = DateTime.UtcNow;

            if (!PropertyManager.GetBool("quest_info_enabled"))
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

            var questMessages = new List<string>();
            foreach (var playerQuest in quests)
            {
                var questName = QuestManager.GetQuestName(playerQuest.QuestName);
                var quest = DatabaseManager.World.GetCachedQuest(questName);
                if (quest == null)
                {
                    continue;
                }

                var minDelta = quest.MinDelta;
                if (QuestManager.CanScaleQuestMinDelta(quest))
                    minDelta = (uint)(quest.MinDelta * PropertyManager.GetDouble("quest_mindelta_rate"));

                var text = $"{playerQuest.QuestName.ToLower()} - {playerQuest.NumTimesCompleted} solves ({playerQuest.LastTimeCompleted}) \"{quest.Message}\" {quest.MaxSolves} {minDelta}";
                questMessages.Add(text);
            }

            foreach (var message in questMessages)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            }
        }

        [CommandHandler("aug", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays your advanced augmentation levels")]
        public static void HandleAugmentations(Session session, params string[] parameters)
        {
            var player = session.Player;

            session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Advanced Augmentation Levels:", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Creature: {session.Player.LuminanceAugmentCreatureCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Item: {session.Player.LuminanceAugmentItemCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Life: {session.Player.LuminanceAugmentLifeCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"War: {session.Player.LuminanceAugmentWarCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Void: {session.Player.LuminanceAugmentVoidCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Duration: {session.Player.LuminanceAugmentSpellDurationCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Specialization: {session.Player.LuminanceAugmentSpecializeCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Melee: {session.Player.LuminanceAugmentMeleeCount:N0}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Missile: {session.Player.LuminanceAugmentMissileCount:N0}", ChatMessageType.Broadcast));
        }

        [CommandHandler("augs", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays your advanced augmentation levels")]
        public static void HandleAugmentations2(Session session, params string[] parameters)
        {
            HandleAugmentations(session, parameters);
        }

        [CommandHandler("qb", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays your Quest Bonus count")]
        public static void HandleQuestBonus(Session session, params string[] parameters)
        {
            var player = session.Player;
            var questCount = player.QuestCompletionCount ?? 0;
            session.Network.EnqueueSend(new GameMessageSystemChat("=== Quest Bonus (QB) ===", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Total Quests Completed: {questCount:N0}", ChatMessageType.Broadcast));
        }

        [CommandHandler("bonus", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Displays all your XP bonuses")]
        public static void HandleBonus(Session session, params string[] parameters)
        {
            var player = session.Player;

            // Quest Bonus
            var questCount = player.QuestCompletionCount ?? 0;
            var questBonus = player.GetQuestCountXPBonus();
            var questBonusPercent = ((questBonus - 1.0) * 100.0);

            // Enlightenment Bonus (+1% per enlightenment level)
            var enlightenmentBonus = 1.0 + (player.Enlightenment * 0.01);
            var enlightenmentBonusPercent = ((enlightenmentBonus - 1.0) * 100.0);

            // PK Dungeon Bonus
            var pkDungeonBonus = player.GetPKDungeonBonus();
            var pkDungeonBonusPercent = ((pkDungeonBonus - 1.0) * 100.0);

            // XP Augmentation Bonus (5% per augmentation, kills only)
            var augmentationBonusXp = player.AugmentationBonusXp;
            var augBonus = 1.0 + (augmentationBonusXp * 0.05);
            var augBonusPercent = (augmentationBonusXp * 5.0);

            // Equipment Bonus (from enchantments) - GetXPBonus() returns additive modifier (e.g., 0.05 for 5%)
            var equipmentBonus = 1.0 + player.EnchantmentManager.GetXPBonus();
            var equipmentBonusPercent = (player.EnchantmentManager.GetXPBonus() * 100.0);

            session.Network.EnqueueSend(new GameMessageSystemChat("=== XP Bonuses ===", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Quest Bonus: {questBonusPercent:F2}% ({questCount:N0} quests)", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Enlightenment Bonus: {enlightenmentBonusPercent:F2}% (Enlightenment {player.Enlightenment})", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"PK Dungeon Bonus: {pkDungeonBonusPercent:F2}%", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Augmentation Bonus: {augBonusPercent:F2}% (kills only, {augmentationBonusXp} augs)", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"Equipment Bonus: {equipmentBonusPercent:F2}%", ChatMessageType.Broadcast));

            var totalBonus = (questBonus * enlightenmentBonus * pkDungeonBonus * augBonus * equipmentBonus) - 1.0;
            var totalBonusPercent = totalBonus * 100.0;
            session.Network.EnqueueSend(new GameMessageSystemChat($"Total Bonus: {totalBonusPercent:F2}% (aug bonus applies to kills only)", ChatMessageType.Broadcast));
        }

        [CommandHandler("xpdebugging", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Toggle XP breakdown display when earning XP from kills and quests")]
        public static void HandleXpBreakdown(Session session, params string[] parameters)
        {
            var player = session.Player;

            // Toggle the setting
            player.ShowXpBreakdown = !player.ShowXpBreakdown;

            var status = player.ShowXpBreakdown ? "enabled" : "disabled";
            session.Network.EnqueueSend(new GameMessageSystemChat($"XP breakdown display is now {status}.", ChatMessageType.Broadcast));
        }

        [CommandHandler("enl", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Begin the enlightenment process")]
        public static void HandleEnlighten(Session session, params string[] parameters)
        {
            var player = session.Player;

            if (!Entity.Enlightenment.VerifyRequirements(player))
                return; // Error messages sent by VerifyRequirements

            Entity.Enlightenment.HandleEnlightenment(player);
        }

        [CommandHandler("enlighten", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Begin the enlightenment process")]
        public static void HandleEnlighten2(Session session, params string[] parameters)
        {
            HandleEnlighten(session, parameters);
        }

        [CommandHandler("top", AccessLevel.Player, CommandHandlerFlag.None, "Show current leaderboards", "use /top qb, /top level, /top enl, /top bank, /top lum, /top augs, /top deaths, or /top titles")]
        public static async void HandleTop(Session session, params string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("[TOP] Specify a leaderboard: /top qb, /top level, /top enl, /top bank, /top lum, /top augs, /top deaths, or /top titles", ChatMessageType.Broadcast));
                return;
            }

            // Rate limit check for /top qb command
            if (parameters[0]?.ToLower() == "qb")
            {
                var qbCommandLimit = PropertyManager.GetLong("qb_command_limit");
                var timeSinceLastCommand = DateTime.UtcNow - session.LastQBCommandTime;
                if (timeSinceLastCommand.TotalSeconds < qbCommandLimit)
                {
                    var remainingTime = (int)(qbCommandLimit - timeSinceLastCommand.TotalSeconds);
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You must wait {remainingTime} more second(s) before using /top qb again.", ChatMessageType.Broadcast));
                    return;
                }
                session.LastQBCommandTime = DateTime.UtcNow;
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
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Quest Bonus:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "level":
                        list = await cache.GetTopLevelAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Level:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "enl":
                    case "enlightenment":
                        list = await cache.GetTopEnlAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Enlightenment:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "bank":
                        list = await cache.GetTopBankAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Banked Pyreals:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "lum":
                    case "luminance":
                        list = await cache.GetTopLumAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Banked Luminance:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "augs":
                    case "aug":
                    case "augmentations":
                        list = await cache.GetTopAugsAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Total Augmentations:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "deaths":
                    case "death":
                        list = await cache.GetTopDeathsAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Death Count:", ChatMessageType.Broadcast));
                        }
                        break;

                    case "titles":
                    case "title":
                        list = await cache.GetTopTitlesAsync(context);
                        if (list.Count > 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat("Top 25 Players by Title Count:", ChatMessageType.Broadcast));
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
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit (or /b d) - Deposit all Pyreals, Luminance, Conquest Coins, Soul Fragments, and Event Tokens", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank deposit pyreals 100 (or /b d p 100) - Deposit specific amount", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw pyreals 100 (or /b w p 100) - Withdraw 100 pyreals", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank withdraw notes 5 (or /b w n 5) - Withdraw 5 trade notes (250k each)", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank transfer pyreals 100 CharName - Transfer 100 pyreals to CharName", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"/bank balance (or /b b) - View your bank balance", ChatMessageType.System));
                session.Network.EnqueueSend(new GameMessageSystemChat($"Currency types: Pyreals (p), Luminance (l), ConquestCoins (c), SoulFragments (s), Eventtokens (e), Notes (n)", ChatMessageType.System));
                return;
            }

            // Rate limit check (only for deposit/withdraw/transfer, not for balance or help)
            if (parameters[0] == "deposit" || parameters[0] == "d" || parameters[0] == "withdraw" || parameters[0] == "w" || parameters[0] == "transfer" || parameters[0] == "t")
            {
                var bankCommandLimit = PropertyManager.GetLong("bank_command_limit");
                var timeSinceLastCommand = DateTime.UtcNow - session.LastBankCommandTime;
                if (timeSinceLastCommand.TotalSeconds < bankCommandLimit)
                {
                    var remainingTime = (int)(bankCommandLimit - timeSinceLastCommand.TotalSeconds);
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You must wait {remainingTime} more second(s) before using this bank command again.", ChatMessageType.System));
                    return;
                }
                session.LastBankCommandTime = DateTime.UtcNow;
            }

            // Cleanup edge cases
            if (session.Player.BankedPyreals < 0) session.Player.BankedPyreals = 0;
            if (session.Player.BankedLuminance < 0) session.Player.BankedLuminance = 0;
            if (session.Player.EventTokens < 0) session.Player.EventTokens = 0;
            if (session.Player.ConquestCoins < 0) session.Player.ConquestCoins = 0;
            if (session.Player.SoulFragments < 0) session.Player.SoulFragments = 0;

            int iType = 0; // 0=all, 1=pyreals, 2=luminance, 3=eventtokens
            long amount = -1;
            string transferTargetName = "";

            if (parameters.Length >= 2)
            {
                if (parameters[1] == "Pyreals" || parameters[1] == "p") iType = 1;
                else if (parameters[1] == "Luminance" || parameters[1] == "l") iType = 2;
                else if (parameters[1] == "Eventtokens" || parameters[1] == "e") iType = 3;
                else if (parameters[1] == "ConquestCoins" || parameters[1] == "c") iType = 4;
                else if (parameters[1] == "SoulFragments" || parameters[1] == "s") iType = 5;
                else if (parameters[1] == "notes" || parameters[1] == "n") iType = 6;
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
                    session.Player.DepositTradeNotes();
                    session.Player.DepositPeas();
                    session.Player.DepositLuminance();
                    session.Player.DepositEventTokens();
                    session.Player.DepositConquestCoins();
                    session.Player.DepositSoulFragments();
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all Pyreals, Trade Notes, Peas, Luminance, Conquest Coins, Soul Fragments, and Event Tokens!", ChatMessageType.System));
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
                        case 4: // Conquest Coins
                            session.Player.DepositConquestCoins();
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all conquest coins!", ChatMessageType.System));
                            break;
                        case 5: // Soul Fragments
                            session.Player.DepositSoulFragments();
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Deposited all soul fragments!", ChatMessageType.System));
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
                    case 4: // Withdraw conquest coins
                        if (session.Player.ConquestCoins != null && amount > session.Player.ConquestCoins)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked conquest coins.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to withdraw.", ChatMessageType.System));
                            break;
                        }
                        session.Player.WithdrawConquestCoins(amount);
                        break;
                    case 5: // Withdraw soul fragments
                        if (session.Player.SoulFragments != null && amount > session.Player.SoulFragments)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked soul fragments.", ChatMessageType.System));
                            break;
                        }
                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify amount to withdraw.", ChatMessageType.System));
                            break;
                        }
                        session.Player.WithdrawSoulFragments(amount);
                        break;
                    case 6: // Withdraw trade notes
                        const int TradeNoteWeenieId = 20630;
                        const long TradeNoteValue = 250000;
                        const int TradeNoteMaxStack = 250;

                        if (amount <= 0)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Specify number of trade notes to withdraw.", ChatMessageType.System));
                            break;
                        }

                        long totalPyrealCost = amount * TradeNoteValue;
                        if (session.Player.BankedPyreals == null || session.Player.BankedPyreals < totalPyrealCost)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"Insufficient banked pyreals. You need {totalPyrealCost:N0} pyreals for {amount:N0} trade notes.", ChatMessageType.System));
                            break;
                        }

                        // Calculate how many stacks we need
                        long notesRemaining = amount;
                        int notesCreated = 0;

                        while (notesRemaining > 0)
                        {
                            int stackSize = (int)Math.Min(notesRemaining, TradeNoteMaxStack);
                            var tradeNote = WorldObjectFactory.CreateNewWorldObject((uint)TradeNoteWeenieId);
                            if (tradeNote == null)
                            {
                                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Error creating trade notes.", ChatMessageType.System));
                                break;
                            }

                            tradeNote.SetStackSize(stackSize);
                            if (session.Player.TryCreateInInventoryWithNetworking(tradeNote))
                            {
                                notesCreated += stackSize;
                                notesRemaining -= stackSize;
                            }
                            else
                            {
                                session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Inventory full. Created {notesCreated:N0} trade notes.", ChatMessageType.System));
                                break;
                            }
                        }

                        if (notesCreated > 0)
                        {
                            long pyrealCost = (long)notesCreated * TradeNoteValue;
                            session.Player.BankedPyreals -= pyrealCost;
                            session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Withdrew {notesCreated:N0} trade notes ({pyrealCost:N0} pyreals). Balance: {session.Player.BankedPyreals:N0}", ChatMessageType.System));

                            // Log the transaction
                            TransferLogger.LogBankTransfer(session.Player, session.Player.Name, $"Trade Note (250k)", notesCreated, "Trade Note Withdrawal");
                        }
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
                        if (!session.Player.TransferPyreals(amount, transferTargetName))
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
                        if (!session.Player.TransferLuminance(amount, transferTargetName))
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
                        if (!session.Player.TransferEventTokens(amount, transferTargetName))
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

            // CONQUEST: Check for admin-style parameters and pass through to admin handler
            if (parameters.Length > 0)
            {
                var adminParams = new[] { "npk", "pk", "pkl", "free" };
                if (adminParams.Contains(parameters[0].ToLower()))
                {
                    // Check if user has Developer access
                    if (session.AccessLevel >= AccessLevel.Developer)
                    {
                        // Pass through to admin handler
                        AdminCommands.HandlePk(session, parameters);
                        return;
                    }
                    else
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown parameter '{parameters[0]}'. Use '/pk on' or '/pk off'.", ChatMessageType.Broadcast));
                        return;
                    }
                }
            }

            // CONQUEST: Mules cannot use PK command
            if (session.Player.IsMule)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Mules cannot toggle PK status.", ChatMessageType.Broadcast));
                return;
            }

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

                // Check 2-hour cooldown after PK death
                var lastPKDeath = session.Player.GetProperty(PropertyInt64.LastPKDeathTime) ?? 0;
                if (lastPKDeath > 0)
                {
                    var timeSinceDeath = Time.GetUnixTime() - lastPKDeath;
                    var cooldown = 7200; // 2 hours in seconds

                    if (timeSinceDeath < cooldown)
                    {
                        var remainingSeconds = (long)(cooldown - timeSinceDeath);
                        var timeDisplay = FormatTimeRemaining(remainingSeconds);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You must wait {timeDisplay} before you can flag PK again after dying in PvP combat.", ChatMessageType.Broadcast));
                        return;
                    }
                }

                // Check if player is busy
                if (session.Player.Teleporting || session.Player.TooBusyToRecall || session.Player.IsBusy || session.Player.IsInDeathProcess)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Cannot change PK status while teleporting or busy. Complete your movement and try again.", ChatMessageType.System));
                    return;
                }

                // Show popup confirmation dialog
                var message = "Enabling PK status will allow other PK players to attack you anywhere in the world! Are you certain you want to become a Player Killer?";

                var confirm = session.Player.ConfirmationManager.EnqueueSend(
                    new Confirmation_Custom(session.Player.Guid, () => {
                        // This callback executes when player clicks "Yes"
                        session.Player.PlayerKillerStatus = PlayerKillerStatus.PK;
                        session.Player.PkLevel = PKLevel.PK; // Set PkLevel so respite timer restores them to PK
                        session.Player.SetProperty(PropertyInt64.LastPKFlagTime, (long)Time.GetUnixTime());
                        session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(session.Player, PropertyInt.PlayerKillerStatus, (int)session.Player.PlayerKillerStatus));

                        session.Network.EnqueueSend(new GameMessageSystemChat("You are now a Player Killer! Other PK players can attack you anywhere.", ChatMessageType.Broadcast));
                        PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{session.Player.Name} is now a Player Killer!", ChatMessageType.Broadcast));
                    }),
                    message
                );
            }
            // /pk off
            else if (param == "off" || param == "disable" || param == "0")
            {
                // Check 2-hour minimum PK duration first (before checking current status, to handle respite NPK correctly)
                var lastPKFlagTime = session.Player.GetProperty(PropertyInt64.LastPKFlagTime) ?? 0;
                if (lastPKFlagTime > 0 && session.Player.PkLevel == PKLevel.PK) // Only check if they're a "real" PK (not just temporarily NPK from respite)
                {
                    var timeSinceFlagged = Time.GetUnixTime() - lastPKFlagTime;
                    var minimumDuration = 7200; // 2 hours in seconds

                    if (timeSinceFlagged < minimumDuration)
                    {
                        var remainingSeconds = (long)(minimumDuration - timeSinceFlagged);
                        var timeDisplay = FormatTimeRemaining(remainingSeconds);
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You must remain PK for at least 2 hours after flagging. {timeDisplay} remaining.", ChatMessageType.Broadcast));
                        return;
                    }
                }
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

                // Check PK timer (cannot turn off during/after recent PK combat)
                if (session.Player.PKTimerActive)
                {
                    var pkTimer = PropertyManager.GetLong("pk_timer");
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot disable PK status while your PK timer is active (lasts {pkTimer} seconds after PK combat).", ChatMessageType.Broadcast));
                    return;
                }

                // Check if player is busy
                if (session.Player.Teleporting || session.Player.TooBusyToRecall || session.Player.IsBusy || session.Player.IsInDeathProcess)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Cannot change PK status while teleporting or busy. Complete your movement and try again.", ChatMessageType.System));
                    return;
                }

                // Show popup confirmation dialog
                var confirmMessage = "Are you sure you want to disable PK status? You will no longer be able to participate in open world PvP.";

                var confirm = session.Player.ConfirmationManager.EnqueueSend(
                    new Confirmation_Custom(session.Player.Guid, () => {
                        // This callback executes when player clicks "Yes"
                        session.Player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                        session.Player.PkLevel = PKLevel.NPK; // Set PkLevel so respite timer won't restore them to PK
                        session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(session.Player, PropertyInt.PlayerKillerStatus, (int)session.Player.PlayerKillerStatus));

                        session.Network.EnqueueSend(new GameMessageSystemChat("You are no longer a Player Killer. You are now safe from PvP attacks.", ChatMessageType.Broadcast));
                    }),
                    confirmMessage
                );
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid parameter. Use '/pk on' or '/pk off'.", ChatMessageType.Broadcast));
            }
        }

        /// <summary>
        /// Helper function to format remaining time in h/m/s format
        /// </summary>
        private static string FormatTimeRemaining(long totalSeconds)
        {
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var seconds = totalSeconds % 60;

            var parts = new List<string>();
            if (hours > 0)
                parts.Add($"{hours}h");
            if (minutes > 0)
                parts.Add($"{minutes}m");
            if (seconds > 0 || parts.Count == 0) // Always show seconds if nothing else, or if there are remaining seconds
                parts.Add($"{seconds}s");

            return string.Join(" ", parts);
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
            actionChain.AddAction(session.Player, ActionType.PlayerHouse_HandleActionQueryHouse, session.Player.HandleActionQueryHouse);
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

        [CommandHandler("instance", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows the current instance")]
        public static void HandleInstanceInfo(Session session, params string[] parameters)
        {
            var physicsObj = session.Player.PhysicsObj;

            var physInstance = physicsObj.Position.Variation;
            var locInstance = session.Player.Location.Variation;

            session.Network.EnqueueSend(new GameMessageSystemChat($"Physics Instance: {physInstance}\nLocation Instance: {locInstance}", ChatMessageType.Broadcast));
            if (session.Player.CurrentLandblock != null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock World Object Count: {session.Player.CurrentLandblock.WorldObjectCount}", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock Physics Object Count: {session.Player.CurrentLandblock.PhysicsObjectCount}", ChatMessageType.Broadcast));
            }

        }

        [CommandHandler("knownobjects", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows the current known objects")]
        public static void HandleKnownObjectList(Session session, params string[] parameters)
        {
            List<WorldObject> objects = session.Player.GetKnownObjects();
            if (objects == null)
            {
                return;
            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"Known Objects Count: {objects.Count}", ChatMessageType.Broadcast));

            foreach (var item in objects)
            {
                // Don't list objects the player can't see
                if (item.Visibility && !session.Player.Adminvision)
                    continue;

                session.Network.EnqueueSend(new GameMessageSystemChat($"{item.Name}, {item.Guid}, {item.Location}", ChatMessageType.Broadcast));
            }
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
            if (!PropertyManager.GetBool("player_config_command"))
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
            if (!PropertyManager.GetBool("version_info_enabled"))
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
            if (!PropertyManager.GetBool("reportbug_enabled"))
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

        // CONQUEST: Arena Commands
        [CommandHandler("arena", AccessLevel.Player, CommandHandlerFlag.None, 1,
            "The arena command is used to join an arena event or get information about arena statistics")]
        public static void HandleArena(Session session, params string[] parameters)
        {
            log.Debug($"HandleArena called for player = {session.Player?.Name}, params = {string.Join(" ", parameters)}");

            if (!CheckPlayerCommandRateLimit(session))
                return;

            if (parameters.Count() < 1)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Invalid parameters.  See the arena help file below for valid parameters.");
                parameters[0] = "help";
            }

            var actionType = parameters[0];

            switch (actionType?.ToLower())
            {
                case "join":

                    string eventType = "1v1";
                    string param2 = string.Empty;
                    if (parameters.Length > 1)
                    {
                        eventType = parameters[1];

                        if (!ArenaManager.IsValidEventType(eventType))
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"Invalid parameters.  The Join command does not support the event type {eventType}. Proper syntax is as follows...\n  To join a 1v1 arena match: /arena join\n  To join a specific type of arena match, replace eventType with the string code for the type of match you want to join, such as 1v1, 2v2, ffa or tugak. : /arena join eventType\n  To get your current character's stats: /arena stats\n  To get a named character's stats, replace characterName with the target character's name: /arena stats characterName");
                            return;
                        }

                        if (parameters.Length > 2)
                        {
                            param2 = parameters[2];
                        }
                    }

                    if (eventType.ToLower().Equals("group"))
                    {
                        //Get a list of players in the fellowship
                        Fellowship firstPlayerFellowship = session.Player.Fellowship;
                        if (firstPlayerFellowship != null)
                        {
                            //Don't allow groups under 3 in size
                            if (firstPlayerFellowship.FellowshipMembers.Count() < 3)
                            {
                                CommandHandlerHelper.WriteOutputInfo(session, $"You must have a fellowship with at least 3 members to queue for a group fight");
                                return;
                            }

                            //For each player in the fellow, set the Team and add to queue
                            //If any players don't meet criteria, report that back and don't allow to join queue
                            List<string> failureMessages = new List<string>();
                            Guid teamGuid = Guid.NewGuid();
                            int maxOpposingTeamSize = int.TryParse(param2, out int result) ? result : 9;
                            if (maxOpposingTeamSize > 9)
                                maxOpposingTeamSize = 9;
                            if (maxOpposingTeamSize < 3)
                                maxOpposingTeamSize = 3;

                            foreach (var fellowMemberId in firstPlayerFellowship.FellowshipMembers.Keys.OrderBy(x => x == session.Player.CharacterTitleId))
                            {
                                var fellowMemberPlayer = PlayerManager.GetOnlinePlayer(fellowMemberId);
                                if (fellowMemberPlayer == null)
                                {
                                    continue;
                                }

                                string queueResultMsg = JoinArenaQueue(fellowMemberPlayer, eventType.ToLower(), out bool queueIsSuccess, teamGuid, maxOpposingTeamSize);
                                if (!queueIsSuccess)
                                {
                                    failureMessages.Add($"{fellowMemberPlayer.Character.Name}: {queueResultMsg}");
                                }
                            }

                            if (failureMessages.Count() > 0)
                            {
                                //Remove all from queue if anyone in the fellow failed
                                ArenaManager.RemoveTeamFromQueue(teamGuid);

                                string returnMessage = "Your team failed to queue for the following reasons...\n\n";
                                foreach (var msg in failureMessages)
                                {
                                    returnMessage += msg + "\n";
                                }

                                CommandHandlerHelper.WriteOutputInfo(session, returnMessage);
                                return;
                            }
                            else
                            {
                                var successMessage = "Your team has successfully queued for a group arena match with the following team members. Please ensure your entire team remains elegible as an online player killer who is not PK tagged.\n\n";
                                var globalMessage = $"{session.Player.Character.Name} has queued a new team of {firstPlayerFellowship.FellowshipMembers.Count()} players for a group arena match, accepting challenging teams with up to {maxOpposingTeamSize} players";
                                foreach (var fellowMemberId in firstPlayerFellowship.FellowshipMembers.Keys)
                                {
                                    var fellowMemberPlayer = PlayerManager.GetOnlinePlayer(fellowMemberId);
                                    if (fellowMemberPlayer == null)
                                    {
                                        continue;
                                    }

                                    successMessage += fellowMemberPlayer.Character.Name + "\n";
                                }
                                CommandHandlerHelper.WriteOutputInfo(session, successMessage);
                                PlayerManager.BroadcastToAll(new GameMessageSystemChat(globalMessage, ChatMessageType.Broadcast));
                                return;
                            }
                        }
                        else
                        {
                            CommandHandlerHelper.WriteOutputInfo(session, $"You must have a fellowship with at least 3 members to queue for a group fight");
                            return;
                        }

                        break;
                    }

                    string resultMsg = JoinArenaQueue(session.Player, eventType.ToLower(), out bool isSuccess);
                    if (resultMsg != null)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, resultMsg);
                        return;
                    }
                    break;

                case "cancel":

                    ArenaManager.PlayerCancel(session.Player.Character.Id);

                    break;

                case "forfeit":
                    CommandHandlerHelper.WriteOutputInfo(session, "Forfeit feature not yet supported, check back later");
                    break;

                case "observe":
                case "watch":
                    string eventIdParam = "";

                    if (!PropertyManager.GetBool("arena_allow_observers"))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"The arena observer feature is currently disabled");
                        return;
                    }

                    if (parameters.Length != 2)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Invalid parameters. The {actionType} command requires an EventID parameter to specify which event to join as an observer. Use the \"/arena info\" command to list all active arena events, including their EventID values.\nUsage: To watch an arena event as an observer /arena watch EventID");
                        return;
                    }

                    //Parse EventID param to int and verify it corresponds to an active event
                    int eventID = 0;
                    eventIdParam = parameters[1];
                    try
                    {
                        eventID = int.Parse(eventIdParam);
                    }
                    catch (Exception)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Invalid parameters. Invalid EventID value {eventIdParam}\nThe {actionType} command requires an EventID parameter to specify which event to join as an observer. Use the \"/arena info\" command to list all active arena events, including their EventID values.\nUsage: To watch an arena event as an observer /arena watch EventID");
                        return;
                    }

                    var arenaEvent = ArenaManager.GetActiveEvents().FirstOrDefault(x => x.Id == eventID);
                    if (arenaEvent != null)
                    {
                        ArenaManager.ObserveEvent(session.Player, eventID);
                    }
                    else
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Invalid parameters. EventID {eventIdParam} does not correspond to an active arena event\nThe {actionType} command requires an EventID parameter to specify which event to join as an observer. Use the \"/arena info\" command to list all active arena events, including their EventID values.\nUsage: To watch an arena event as an observer /arena watch EventID");
                        return;
                    }

                    break;

                case "info":

                    var queuedPlayers = ArenaManager.GetQueuedPlayers();
                    var queuedOnes = queuedPlayers.Where(x => x.EventType.ToLower().Equals("1v1"));
                    var queuedTwos = queuedPlayers.Where(x => x.EventType.ToLower().Equals("2v2"));
                    var queuedFFA = queuedPlayers.Where(x => x.EventType.ToLower().Equals("ffa"));
                    var queuedGroup = queuedPlayers.Where(x => x.EventType.ToLower().Equals("group"));
                    var queuedTugak = queuedPlayers.Where(x => x.EventType.ToLower().Equals("tugak"));
                    var longestOnesWait = queuedOnes.Count() > 0 ? (DateTime.Now - queuedOnes.Min(x => x.CreateDateTime)) : new TimeSpan(0);
                    var longestTwosWait = queuedTwos.Count() > 0 ? (DateTime.Now - queuedTwos.Min(x => x.CreateDateTime)) : new TimeSpan(0);
                    var longestFFAWait = queuedFFA.Count() > 0 ? (DateTime.Now - queuedFFA.Min(x => x.CreateDateTime)) : new TimeSpan(0);
                    var longestTugakWait = queuedTugak.Count() > 0 ? (DateTime.Now - queuedTugak.Min(x => x.CreateDateTime)) : new TimeSpan(0);

                    string queueInfo = $"Current Arena Queues\n  1v1: {queuedOnes.Count()} players queued with longest wait at {string.Format("{0:%h}h {0:%m}m {0:%s}s", longestOnesWait)}\n  2v2: {queuedTwos.Count()} players queued, with longest wait at {string.Format("{0:%h}h {0:%m}m {0:%s}s", longestTwosWait)}\n  FFA: {queuedFFA.Count()} players queued, with longest wait at {string.Format("{0:%h}h {0:%m}m {0:%s}s", longestFFAWait)}\n  Tugak: {queuedTugak.Count()} players queued, with longest wait at {string.Format("{0:%h}h {0:%m}m {0:%s}s", longestTugakWait)}\n  Group:";

                    var queuedGroupTeams = queuedGroup.Select(x => x.TeamGuid).Distinct();
                    foreach (var queuedTeam in queuedGroupTeams)
                    {
                        var teamMembers = queuedGroup.Where(x => x.TeamGuid == queuedTeam);
                        var leader = teamMembers.OrderBy(x => x.CreateDateTime).First();
                        queueInfo += $"\n\n    Team Leader: {leader.CharacterName}\n    Num Players: {teamMembers.Count()}\n    Max Opponents: {leader.MaxOpposingTeamSize}\n    Time Queued: {String.Format("{0:%h}h {0:%m}m {0:%s}s", DateTime.Now - leader.CreateDateTime)}";
                    }

                    var activeEvents = ArenaManager.GetActiveEvents();
                    var eventsOnes = activeEvents.Where(x => x.EventType.ToLower().Equals("1v1"));
                    var eventsTwos = activeEvents.Where(x => x.EventType.ToLower().Equals("2v2"));
                    var eventsFFA = activeEvents.Where(x => x.EventType.ToLower().Equals("ffa"));
                    var eventsGroup = activeEvents.Where(x => x.EventType.ToLower().Equals("group"));
                    var eventsTugak = activeEvents.Where(x => x.EventType.ToLower().Equals("tugak"));

                    string onesEventInfo = eventsOnes.Count() == 0 ? "No active events" : "";
                    foreach (var ev in eventsOnes)
                    {
                        onesEventInfo += $"\n    EventID: {(ev.Id < 1 ? "Pending" : ev.Id.ToString())}\n" +
                                         $"    Arena: {ArenaManager.GetArenaNameByLandblock(ev.Location)}\n" +
                                         $"    Players:\n    {ev.PlayersDisplay}\n" +
                                         $"    Time Remaining: {ev.TimeRemainingDisplay}\n";
                    }

                    string twosEventInfo = eventsTwos.Count() == 0 ? "No active events" : "";
                    foreach (var ev in eventsTwos)
                    {
                        twosEventInfo += $"\n    EventID: {(ev.Id < 1 ? "Pending" : ev.Id.ToString())}\n" +
                                         $"    Arena: {ArenaManager.GetArenaNameByLandblock(ev.Location)}\n" +
                                         $"    Players:\n    {ev.PlayersDisplay}\n" +
                                         $"    Time Remaining: {ev.TimeRemainingDisplay}\n";
                    }

                    string ffaEventInfo = eventsFFA.Count() == 0 ? "No active events" : "";
                    foreach (var ev in eventsFFA)
                    {
                        ffaEventInfo += $"\n    EventID: {(ev.Id < 1 ? "Pending" : ev.Id.ToString())}\n" +
                                         $"    Arena: {ArenaManager.GetArenaNameByLandblock(ev.Location)}\n" +
                                         $"    Players:\n    {ev.PlayersDisplay}\n" +
                                         $"    Time Remaining: {ev.TimeRemainingDisplay}\n";
                    }

                    string tugakEventInfo = eventsTugak.Count() == 0 ? "No active events" : "";
                    foreach (var ev in eventsTugak)
                    {
                        tugakEventInfo += $"\n    EventID: {(ev.Id < 1 ? "Pending" : ev.Id.ToString())}\n" +
                                         $"    Arena: {ArenaManager.GetArenaNameByLandblock(ev.Location)}\n" +
                                         $"    Players:\n    {ev.PlayersDisplay}\n" +
                                         $"    Time Remaining: {ev.TimeRemainingDisplay}\n";
                    }

                    string groupEventInfo = eventsGroup.Count() == 0 ? "No active events" : "";
                    foreach (var ev in eventsGroup)
                    {
                        groupEventInfo += $"\n    EventID: {(ev.Id < 1 ? "Pending" : ev.Id.ToString())}\n" +
                                         $"    Arena: {ArenaManager.GetArenaNameByLandblock(ev.Location)}\n" +
                                         $"    Players:\n    {ev.PlayersDisplay}\n" +
                                         $"    Time Remaining: {ev.TimeRemainingDisplay}\n";
                    }

                    string eventInfo = $"Active Arena Matches:\n  1v1: {onesEventInfo}\n  2v2: {twosEventInfo}\n  FFA: {ffaEventInfo}\n  Tugak: {tugakEventInfo}\n  Group: {groupEventInfo}\n";

                    CommandHandlerHelper.WriteOutputInfo(session, $"*********\n{queueInfo}\n\n{eventInfo}\n*********\n");
                    break;

                case "stats":

                    string returnMsg;
                    if (parameters.Count() >= 2)
                    {
                        string playerParam = "";
                        for (int i = 1; i < parameters.Length; i++)
                        {
                            playerParam += i == 1 ? parameters[i] : $" {parameters[i]}";
                        }

                        var targetPlayer = PlayerManager.GetAllPlayers().FirstOrDefault(x => x.Name.ToLower().Equals(playerParam.ToLower()));
                        if (targetPlayer != null)
                        {
                            var targetOnlinePlayer = PlayerManager.GetOnlinePlayer(targetPlayer.Guid);
                            var targetOfflinePlayer = PlayerManager.GetOfflinePlayer(targetPlayer.Guid);

                            returnMsg = GetArenaStats(targetOnlinePlayer != null ? targetOnlinePlayer.Character.Id : (targetOfflinePlayer != null ? targetOfflinePlayer.Biota.Id : 0), targetPlayer.Name);
                        }
                        else
                        {
                            returnMsg = $"Unable to find a player named {playerParam}";
                        }
                    }
                    else
                    {
                        returnMsg = GetArenaStats(session.Player.Character.Id, session.Player.Character.Name);
                    }

                    CommandHandlerHelper.WriteOutputInfo(session, returnMsg);
                    break;

                case "rank":

                    StringBuilder rankReturnMsg = new StringBuilder();
                    string eventTypeParam = "";
                    if (parameters.Count() >= 2)
                    {
                        eventTypeParam = parameters[1];
                    }

                    bool validParam = false;
                    if (eventTypeParam.ToLower().Equals("1v1") ||
                        eventTypeParam.ToLower().Equals("2v2") ||
                        eventTypeParam.ToLower().Equals("ffa") ||
                        eventTypeParam.ToLower().Equals("tugak"))
                    {
                        validParam = true;
                    }

                    if (!validParam)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, "Invalid Event Type Parameter\nUsage: /arena rank {eventType}\nExample: /arena rank 1v1");
                        break;
                    }

                    List<ArenaCharacterStats> topTen = DatabaseManager.Log.GetArenaTopRankedByEventType(eventTypeParam.ToLower());

                    rankReturnMsg.Append($"***** Top Ten {eventTypeParam.ToLower()} Players *****\n\n");
                    for (int i = 0; i < topTen.Count(); i++)
                    {
                        var currStats = topTen[i];
                        rankReturnMsg.Append($"  Rank #{i + 1} - {currStats.CharacterName}\n  Rank Points: {currStats.RankPoints}\n  Total Matches: {currStats.TotalMatches}\n  Total Wins: {currStats.TotalWins}\n  Total Draws: {currStats.TotalDraws}\n  Total Losses: {currStats.TotalLosses}\n\n");
                    }

                    rankReturnMsg.Append($"**********\n");
                    CommandHandlerHelper.WriteOutputInfo(session, rankReturnMsg.ToString());

                    break;

                default:
                    CommandHandlerHelper.WriteOutputInfo(session, $"Arena Commands...\n\n  To join a 1v1 arena match: /arena join\n\n  To join a specific type of arena match: /arena join eventType\n  (replace eventType with the string code for the type of match you want to join; 1v1, 2v2, FFA, Tugak or Group)\n\n  To leave an arena queue or stop observing a match: /arena cancel\n\n  To get info about players in an arena queue and active arena matches: /arena info\n\n  To get your current character's stats: /arena stats\n\n  To get a named character's stats: /arena stats characterName\n  (replace characterName with the target character's name)\n\n  To get rank leaderboard by event type: /arena rank eventType\n  (replace eventType with the string code for the type of match you want ranking for; 1v1, 2v2, Tugak or FFA)\n\n  To watch a match as a silent observer: /arena watch EventID\n  (use /arena info to get the EventID of an active arena match and use that value in the command)\n\n    To get this help file: /arena help\n");
                    return;
            }
        }

        private static string JoinArenaQueue(Player player, string eventType, out bool isSuccess, Guid? teamGuid = null, int maxOpposingTeamSize = 9)
        {
            //Blacklist specific players
            var blacklistString = PropertyManager.GetString("arenas_blacklist");
            if (!string.IsNullOrEmpty(blacklistString))
            {
                uint? monarchId = player.MonarchId;
                var playerAllegiance = AllegianceManager.GetAllegiance(player);
                if (playerAllegiance != null && playerAllegiance.MonarchId.HasValue)
                {
                    monarchId = playerAllegiance.MonarchId;
                }

                var blacklist = blacklistString.Split(',');
                foreach (var charIdString in blacklist)
                {
                    if (uint.TryParse(charIdString, out uint charId) && (player.Character.Id == charId || monarchId == charId))
                    {
                        isSuccess = false;
                        return "You are blacklisted from joining Arena events. Please contact an administrator if you believe this is an error.";
                    }
                }
            }

            var minLevel = PropertyManager.GetLong("arenas_min_level");
            if (player.Level < minLevel)
            {
                isSuccess = false;
                return $"You must be at least level {minLevel} to join an arena match";
            }

            if (player.IsArenaObserver ||
                player.IsPendingArenaObserver ||
                player.CloakStatus == CloakStatus.On)
            {
                isSuccess = false;
                return $"You cannot join an arena queue while you're watching an arena event. Use /arena cancel to stop watching the current event before you queue.";
            }

            if (!player.IsPK)
            {
                isSuccess = false;
                return $"You cannot join an arena queue until you are in a PK state";
            }

            if (player.PKTimerActive)
            {
                isSuccess = false;
                return $"You cannot join an arena queue while you are PK tagged";
            }

            uint? monarchId2 = player.MonarchId;
            string monarchName = player.Name;
            var playerAllegiance2 = AllegianceManager.GetAllegiance(player);
            if (playerAllegiance2 != null && playerAllegiance2.MonarchId.HasValue)
            {
                monarchId2 = playerAllegiance2.MonarchId;
                monarchName = playerAllegiance2.Monarch.Player.Name;
            }

            string returnMsg;
            if (!ArenaManager.AddPlayerToQueue(
                player.Character.Id,
                player.Character.Name,
                player.Level,
                eventType,
                monarchId2.HasValue ? monarchId2.Value : player.Character.Id,
                monarchName,
                player.Session.EndPoint?.Address?.ToString(),
                out returnMsg,
                teamGuid,
                maxOpposingTeamSize))
            {
                isSuccess = false;
                return returnMsg;
            }

            isSuccess = true;
            return $"You have successfully joined the {eventType} arena queue";
        }

        private static string GetArenaStats(uint characterId, string characterName)
        {
            return DatabaseManager.Log.GetArenaStatsByCharacterId(characterId, characterName);
        }

        public static bool CheckPlayerCommandRateLimit(Session session, int limitSeconds = 3)
        {
            if (session == null)
                return false;

            if (session.Player.LastPlayerCommandTimestamp.HasValue && Time.GetDateTimeFromTimestamp(session.Player.LastPlayerCommandTimestamp.Value) > DateTime.UtcNow.AddSeconds(-1 * limitSeconds))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"To prevent abuse, you can only issue this player command every {limitSeconds} seconds. Please try again later.");
                return false;
            }
            else
            {
                session.Player.LastPlayerCommandTimestamp = Time.GetUnixTime(DateTime.UtcNow);
                return true;
            }
        }

        [CommandHandler("pkquests", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "View your current PK quest progress")]
        public static void HandlePkQuests(Session session, params string[] parameters)
        {
            var player = session.Player;
            var sb = new StringBuilder();
            sb.AppendLine($"\n===== Your PK Quests =====");

            if (player.PkQuestList == null || player.PkQuestList.Count == 0)
            {
                sb.AppendLine("No active PK quests assigned.");
            }
            else
            {
                foreach (var pkQuest in player.PkQuestList)
                {
                    var quest = Entity.PKQuests.PKQuests.GetPkQuestByCode(pkQuest.QuestCode);
                    if (quest == null)
                        continue;

                    var status = pkQuest.IsCompleted ? "[COMPLETE]" : $"[{pkQuest.TaskDoneCount}/{quest.TaskCount}]";
                    var rewarded = pkQuest.RewardDelivered ? " (Rewarded)" : "";
                    sb.AppendLine($"  {status} {quest.Description}{rewarded}");
                }
            }

            session.Network.EnqueueSend(new GameMessageSystemChat(sb.ToString(), ChatMessageType.System));
        }
    }
}

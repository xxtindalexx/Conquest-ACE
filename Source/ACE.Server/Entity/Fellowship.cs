using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Entity
{
    /// <summary>
    /// CONQUEST: Entry in the fellowship waiting queue that persists through disconnects
    /// </summary>
    public class FellowshipQueueEntry
    {
        public uint CharacterId { get; set; }
        public string CharacterName { get; set; }
        public double QueuedTime { get; set; }  // Unix timestamp when queued

        /// <summary>
        /// Queue entries expire after 2 minutes of being offline
        /// </summary>
        public const int QueueTimeoutSeconds = 120;

        public FellowshipQueueEntry(uint characterId, string characterName)
        {
            CharacterId = characterId;
            CharacterName = characterName;
            QueuedTime = Time.GetUnixTime();
        }

        /// <summary>
        /// Check if this queue entry has expired (player offline for too long)
        /// </summary>
        public bool IsExpired()
        {
            var player = PlayerManager.GetOnlinePlayer(CharacterId);
            if (player != null)
            {
                // Player is online, reset the queue time and not expired
                QueuedTime = Time.GetUnixTime();
                return false;
            }

            // Player is offline - check if they've been offline too long
            var queuedAt = Time.GetDateTimeFromTimestamp(QueuedTime);
            var expiryTime = queuedAt.AddSeconds(QueueTimeoutSeconds);
            return DateTime.UtcNow > expiryTime;
        }
    }

    public class Fellowship
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The maximum # of fellowship members (default, can be overridden by server property)
        /// </summary>
        public static int MaxFellows = 14;

        /// <summary>
        /// CONQUEST: Get the configurable max fellowship size
        /// </summary>
        public int GetMaxFellows()
        {
            return (int)PropertyManager.GetLong("fellowship_max_members", MaxFellows);
        }

        /// <summary>
        /// CONQUEST: Check if a player is a recent departure (within 2 minutes)
        /// </summary>
        public bool IsRecentDeparture(uint playerGuid)
        {
            if (!DepartedMembers.TryGetValue(playerGuid, out var departureTime))
                return false;

            var departedAt = Time.GetDateTimeFromTimestamp(departureTime);
            var rejoinWindowSeconds = 120; // 2 minutes for all fellowships
            var rejoinWindow = departedAt.AddSeconds(rejoinWindowSeconds);

            return DateTime.UtcNow <= rejoinWindow;
        }

        public string FellowshipName;
        public uint FellowshipLeaderGuid;

        public bool DesiredShareXP;     // determined by the leader's 'ShareFellowshipExpAndLuminance' client option when fellowship is created
        public bool ShareLoot;          // determined by the leader's 'ShareFellowshipLoot' client option when fellowship is created

        public bool ShareXP;            // whether or not XP sharing is currently enabled, as determined by DesiredShareXP && level restrictions
        public bool EvenShare;          // true if all fellows are >= level 50, or all fellows are within 5 levels of the leader

        public bool Open;               // indicates if non-leaders can invite new fellowship members
        public bool IsLocked;           // only set through emotes. if a fellowship is locked, new fellowship members cannot be added

        public Dictionary<uint, WeakReference<Player>> FellowshipMembers;

        public Dictionary<uint, int> DepartedMembers;

        public Dictionary<string, FellowshipLockData> FellowshipLocks;

        public QuestManager QuestManager;

        /// <summary>
        /// CONQUEST: Queue of players waiting to join when a spot opens
        /// Persists through disconnects for up to 2 minutes
        /// </summary>
        public List<FellowshipQueueEntry> WaitingQueue;

        /// <summary>
        /// CONQUEST: Active vote kick session
        /// </summary>
        public VoteKickSession ActiveVoteKick;

        /// <summary>
        /// CONQUEST: Active vote leader session
        /// </summary>
        public VoteLeaderSession ActiveVoteLeader;

        /// <summary>
        /// Called when a player first creates a Fellowship
        /// </summary>
        public Fellowship(Player leader, string fellowshipName, bool shareXP)
        {
            DesiredShareXP = shareXP;
            ShareXP = shareXP;

            // get loot sharing from leader's character options
            ShareLoot = leader.GetCharacterOption(CharacterOption.ShareFellowshipLoot);

            FellowshipLeaderGuid = leader.Guid.Full;
            FellowshipName = fellowshipName;
            EvenShare = false;

            FellowshipMembers = new Dictionary<uint, WeakReference<Player>>() { { leader.Guid.Full, new WeakReference<Player>(leader) } };

            Open = false;

            QuestManager = new QuestManager(this);
            IsLocked = false;
            DepartedMembers = new Dictionary<uint, int>();
            FellowshipLocks = new Dictionary<string, FellowshipLockData>();
            WaitingQueue = new List<FellowshipQueueEntry>();
            ActiveVoteKick = null;
            ActiveVoteLeader = null;
        }

        /// <summary>
        /// Called when a player clicks the 'add fellow' button
        /// </summary>
        public void AddFellowshipMember(Player inviter, Player newMember)
        {
            if (inviter == null || newMember == null)
                return;

            if (IsLocked)
            {
                if (!DepartedMembers.TryGetValue(newMember.Guid.Full, out var timeDeparted))
                {
                    inviter.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(inviter.Session, WeenieErrorWithString.LockedFellowshipCannotRecruit_, newMember.Name));
                    //newMember.SendWeenieError(WeenieError.LockedFellowshipCannotRecruitYou);
                    return;
                }
                else
                {
                    // CONQUEST: Use 2-minute rejoin window for locked fellowships (consistent with IsRecentDeparture)
                    var timeLimit = Time.GetDateTimeFromTimestamp(timeDeparted).AddSeconds(120);
                    if (DateTime.UtcNow > timeLimit)
                    {
                        inviter.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(inviter.Session, WeenieErrorWithString.LockedFellowshipCannotRecruit_, newMember.Name));
                        //newMember.SendWeenieError(WeenieError.LockedFellowshipCannotRecruitYou);
                        return;
                    }
                }
            }

            // CONQUEST: Check if player is a recent departure (within 2 minutes) - they can rejoin even if full
            var isRecentDeparture = false;
            if (DepartedMembers.TryGetValue(newMember.Guid.Full, out var departureTime))
            {
                var departedAt = Time.GetDateTimeFromTimestamp(departureTime);
                var rejoinWindow = departedAt.AddSeconds(120); // 2 minute window
                if (DateTime.UtcNow <= rejoinWindow)
                {
                    isRecentDeparture = true;
                    DepartedMembers.Remove(newMember.Guid.Full); // Clear from departed list
                }
                else
                {
                    // Window expired, remove from departed list
                    DepartedMembers.Remove(newMember.Guid.Full);
                }
            }

            if (FellowshipMembers.Count >= GetMaxFellows() && !isRecentDeparture)
            {
                inviter.Session.Network.EnqueueSend(new GameEventWeenieError(inviter.Session, WeenieError.YourFellowshipIsFull));
                return;
            }

            // CONQUEST: Check if there are recent departures that should be prioritized for this spot
            // This prevents the leader from manually inviting someone else while a departed player has 2 minutes to rejoin
            if (!isRecentDeparture)
            {
                var recentDepartureCount = DepartedMembers.Count(kvp =>
                {
                    var departedAt = Time.GetDateTimeFromTimestamp(kvp.Value);
                    var rejoinWindow = departedAt.AddSeconds(120); // 2 minute window
                    return DateTime.UtcNow <= rejoinWindow;
                });

                // If adding this new member would take a spot reserved for recent departures, block it
                // Reserved spots = one per recent departure
                if (recentDepartureCount > 0 && FellowshipMembers.Count + 1 + recentDepartureCount > GetMaxFellows())
                {
                    inviter.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"A recently departed member has 2 minutes to rejoin. Please wait or invite them back.",
                        ChatMessageType.Broadcast));
                    return;
                }

                // CONQUEST: Check if there are people waiting in queue - manual adds must go through queue
                var queueCount = GetWaitingQueueCount();
                if (queueCount > 0)
                {
                    inviter.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"[FSHIP]: There are {queueCount} player(s) waiting in queue. {newMember.Name} must join the queue with 'fship add'.",
                        ChatMessageType.Broadcast));
                    return;
                }
            }

            if (newMember.Fellowship != null || FellowshipMembers.ContainsKey(newMember.Guid.Full))
            {
                inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{newMember.Name} is already a member of a Fellowship.", ChatMessageType.Broadcast));
            }
            else
            {
                if (newMember.GetCharacterOption(CharacterOption.AutomaticallyAcceptFellowshipRequests))
                {
                    AddConfirmedMember(inviter, newMember, true);
                }
                else
                {
                    if (!newMember.ConfirmationManager.EnqueueSend(new Confirmation_Fellowship(inviter.Guid, newMember.Guid), inviter.Name))
                    {
                        inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{newMember.Name} is busy.", ChatMessageType.Broadcast));
                    }
                }
            }
        }

        /// <summary>
        /// Finalizes the process of adding a player to the fellowship
        /// If the player doesn't have the 'automatically accept fellowship requests' option set,
        /// this would be after they responded to the popup window
        /// </summary>
        public void AddConfirmedMember(Player inviter, Player player, bool response)
        {
            if (inviter == null || inviter.Session == null || inviter.Session.Player == null || player == null) return;

            if (!response)
            {
                // player clicked 'no' on the fellowship popup
                inviter.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} declines your invite", ChatMessageType.Fellowship));
                inviter.Session.Network.EnqueueSend(new GameEventWeenieError(inviter.Session, WeenieError.FellowshipDeclined));

                // CONQUEST: Try the next person in queue if someone declines
                ProcessWaitingQueue();
                return;
            }

            if (FellowshipMembers.Count >= GetMaxFellows())
            {
                inviter.Session.Network.EnqueueSend(new GameEventWeenieError(inviter.Session, WeenieError.YourFellowshipIsFull));
                return;
            }

            FellowshipMembers.TryAdd(player.Guid.Full, new WeakReference<Player>(player));
            player.Fellowship = inviter.Fellowship;

            // CONQUEST: Immediately remove player from all other fellowship queues
            RemoveFromAllQueues(player.Guid.Full);

            CalculateXPSharing();

            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values.Where(i => i.Guid != player.Guid))
                member.Session.Network.EnqueueSend(new GameEventFellowshipUpdateFellow(member.Session, player, ShareXP));

            if (ShareLoot)
            {
                foreach (var member in fellowshipMembers.Values.Where(i => i.Guid != player.Guid))
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} has given you permission to loot his or her kills.", ChatMessageType.Broadcast));
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} may now loot your kills.", ChatMessageType.Broadcast));

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} has given you permission to loot his or her kills.", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} may now loot your kills.", ChatMessageType.Broadcast));
                }
            }

            UpdateAllMembers();

            if (inviter.CurrentMotionState.Stance == MotionStance.NonCombat) // only do this motion if inviter is at peace, other times motion is skipped.
                inviter.SendMotionAsCommands(MotionCommand.BowDeep, MotionStance.NonCombat);
        }

        /// <summary>
        /// CONQUEST: Add a player to the waiting queue when fellowship is full
        /// Queue position persists through disconnects for up to 2 minutes
        /// </summary>
        public int AddToWaitingQueue(Player player)
        {
            if (player == null) return -1;

            // Clean up expired entries first
            CleanupExpiredQueueEntries();

            // Check if player is already in queue
            for (int i = 0; i < WaitingQueue.Count; i++)
            {
                if (WaitingQueue[i].CharacterId == player.Guid.Full)
                {
                    // Player is already in queue - refresh their queue time and return position
                    WaitingQueue[i].QueuedTime = Time.GetUnixTime();
                    return i + 1;
                }
            }

            // Add to queue
            WaitingQueue.Add(new FellowshipQueueEntry(player.Guid.Full, player.Name));
            return WaitingQueue.Count;
        }

        /// <summary>
        /// CONQUEST: Remove expired queue entries (offline for more than 2 minutes)
        /// </summary>
        private void CleanupExpiredQueueEntries()
        {
            for (int i = WaitingQueue.Count - 1; i >= 0; i--)
            {
                if (WaitingQueue[i].IsExpired())
                {
                    WaitingQueue.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// CONQUEST: Remove a player from the waiting queue
        /// </summary>
        public bool RemoveFromWaitingQueue(Player player)
        {
            if (player == null) return false;
            return RemoveFromWaitingQueue(player.Guid.Full);
        }

        /// <summary>
        /// CONQUEST: Remove a player from the waiting queue by character ID
        /// </summary>
        public bool RemoveFromWaitingQueue(uint characterId)
        {
            for (int i = WaitingQueue.Count - 1; i >= 0; i--)
            {
                if (WaitingQueue[i].CharacterId == characterId)
                {
                    WaitingQueue.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// CONQUEST: Remove a player from ALL fellowship queues (called when they join a fellowship)
        /// </summary>
        public static void RemoveFromAllQueues(uint characterId)
        {
            // Iterate through all online players and check their fellowships
            var onlinePlayers = Managers.PlayerManager.GetAllOnline();
            var processedFellowships = new HashSet<uint>(); // Track by leader GUID to avoid duplicates

            foreach (var player in onlinePlayers)
            {
                if (player.Fellowship == null)
                    continue;

                // Skip if we already processed this fellowship
                if (processedFellowships.Contains(player.Fellowship.FellowshipLeaderGuid))
                    continue;

                processedFellowships.Add(player.Fellowship.FellowshipLeaderGuid);

                // Remove from this fellowship's queue
                player.Fellowship.RemoveFromWaitingQueue(characterId);
            }
        }

        /// <summary>
        /// CONQUEST: Process the waiting queue when a spot opens - invite the next player
        /// </summary>
        public void ProcessWaitingQueue()
        {
            if (WaitingQueue.Count == 0) return;
            if (FellowshipMembers.Count >= GetMaxFellows()) return;
            if (IsLocked) return;

            // CONQUEST: Clean up expired departures FIRST
            var expiredDepartures = DepartedMembers.Where(kvp =>
            {
                var departedAt = Time.GetDateTimeFromTimestamp(kvp.Value);
                var rejoinWindow = departedAt.AddSeconds(120);
                return DateTime.UtcNow > rejoinWindow;
            }).Select(kvp => kvp.Key).ToList();

            foreach (var key in expiredDepartures)
                DepartedMembers.Remove(key);

            // CONQUEST: Now check if any recent departures still exist (within 2 minutes)
            // If so, delay queue processing to give them time to rejoin
            var hasRecentDepartures = DepartedMembers.Any(kvp =>
            {
                var departedAt = Time.GetDateTimeFromTimestamp(kvp.Value);
                var rejoinWindow = departedAt.AddSeconds(120); // 2 minute window
                return DateTime.UtcNow <= rejoinWindow;
            });

            if (hasRecentDepartures)
            {
                return; // Don't process queue - wait for potential rejoin
            }

            // Clean up expired entries and find the first valid online player
            CleanupExpiredQueueEntries();

            // CONQUEST: Track index to skip offline-but-valid players and try the next one
            int queueIndex = 0;
            while (queueIndex < WaitingQueue.Count)
            {
                var queueEntry = WaitingQueue[queueIndex];

                // Look up the player by character ID
                var player = PlayerManager.GetOnlinePlayer(queueEntry.CharacterId);

                if (player == null)
                {
                    // Player is offline - check if their queue entry has expired
                    if (queueEntry.IsExpired())
                    {
                        WaitingQueue.RemoveAt(queueIndex);
                        continue; // Don't increment index, list shifted
                    }
                    // Player is offline but within timeout - skip to next in queue
                    // They keep their position and will be processed when they come back online
                    queueIndex++;
                    continue;
                }

                // Player is online - remove from queue and process
                WaitingQueue.RemoveAt(queueIndex);

                if (player.Session == null || player.Session.Player == null)
                    continue; // Player disconnected, skip

                if (player.Fellowship != null)
                {
                    // Player already joined another fellowship
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"[FSHIP]: You were next in queue for {FellowshipName}, but you're already in a fellowship.",
                        ChatMessageType.Broadcast));
                    continue;
                }

                // Found a valid player - get the leader to invite them
                var leader = GetLeader();
                if (leader == null)
                {
                    // No leader available, put player back at front of queue
                    WaitingQueue.Insert(0, queueEntry);
                    return;
                }

                // Notify the player they're being added
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"[FSHIP]: A spot opened in {FellowshipName}! Adding you now...",
                    ChatMessageType.Broadcast));

                // CONQUEST: Auto-add queued players - they already expressed intent to join by queuing
                // Skip the confirmation popup since they already requested to join
                AddConfirmedMember(leader, player, true);

                // CONQUEST: Check if fellowship is now full - if so, stop processing
                // Otherwise continue to add more queued players if multiple spots opened
                if (FellowshipMembers.Count >= GetMaxFellows())
                {
                    NotifyQueuePositions();
                    return;
                }

                // Continue processing - don't increment index since we removed an entry
                // The loop will pick up the next queued player
            }

            // Done processing queue - notify remaining members of their positions
            NotifyQueuePositions();
        }

        /// <summary>
        /// CONQUEST: Get the fellowship leader player object
        /// </summary>
        public Player GetLeader()
        {
            var members = GetFellowshipMembers();
            if (members.TryGetValue(FellowshipLeaderGuid, out var leader))
                return leader;
            return null;
        }

        /// <summary>
        /// CONQUEST: Notify all online players in queue of their current position
        /// </summary>
        public void NotifyQueuePositions()
        {
            CleanupExpiredQueueEntries();

            for (int i = 0; i < WaitingQueue.Count; i++)
            {
                var player = PlayerManager.GetOnlinePlayer(WaitingQueue[i].CharacterId);
                if (player?.Session != null)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"[FSHIP]: You are now #{i + 1} in queue for {FellowshipName}.",
                        ChatMessageType.Broadcast));
                }
            }
        }

        /// <summary>
        /// CONQUEST: Get current queue count (cleaning up expired entries)
        /// </summary>
        public int GetWaitingQueueCount()
        {
            CleanupExpiredQueueEntries();

            // Also remove entries for players who joined another fellowship
            for (int i = WaitingQueue.Count - 1; i >= 0; i--)
            {
                var player = PlayerManager.GetOnlinePlayer(WaitingQueue[i].CharacterId);
                if (player?.Fellowship != null)
                    WaitingQueue.RemoveAt(i);
            }

            return WaitingQueue.Count;
        }

        public void RemoveFellowshipMember(Player player, Player leader)
        {
            if (player == null) return;

            var fellowshipMembers = GetFellowshipMembers();

            if (!fellowshipMembers.ContainsKey(player.Guid.Full))
            {
                var leaderName = leader?.Name ?? "System";
                log.Warn($"{leaderName} tried to dismiss {player.Name} from the fellowship, but {player.Name} was not found in the fellowship");

                var done = true;

                if (player.Fellowship != null)
                {
                    if (player.Fellowship == this)
                    {
                        log.Warn($"{player.Name} still has a reference to this fellowship somehow. This shouldn't happen");
                        done = false;
                    }
                    else
                        log.Warn($"{player.Name} has a reference to a different fellowship. {leader.Name} is possibly sending crafted data!");
                }

                if (done) return;
            }

            foreach (var member in fellowshipMembers.Values)
            {
                member.Session.Network.EnqueueSend(new GameEventFellowshipDismiss(member.Session, player));
                member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} dismissed from fellowship", ChatMessageType.Fellowship));
            }

            FellowshipMembers.Remove(player.Guid.Full);
            player.Fellowship = null;

            // CONQUEST: Cancel any votes involving this player
            CancelVotesForPlayer(player.Guid.Full, player.Name);

            CalculateXPSharing();

            UpdateAllMembers();

            // CONQUEST: Check if anyone is waiting in queue
            ProcessWaitingQueue();
        }

        /// <summary>
        /// CONQUEST: Cancel any active votes if the relevant player leaves the fellowship
        /// </summary>
        private void CancelVotesForPlayer(uint playerGuid, string playerName)
        {
            // Cancel vote kick if the target left
            if (ActiveVoteKick != null && ActiveVoteKick.TargetGuid == playerGuid)
            {
                var fellowshipMembers = GetFellowshipMembers();
                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"[FSHIP]: Vote kick canceled - {playerName} left the fellowship.",
                        ChatMessageType.Broadcast));
                }
                ActiveVoteKick = null;
            }

            // Cancel vote leader if the candidate left
            if (ActiveVoteLeader != null && ActiveVoteLeader.CandidateGuid == playerGuid)
            {
                var fellowshipMembers = GetFellowshipMembers();
                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"[FSHIP]: Leadership vote canceled - {playerName} left the fellowship.",
                        ChatMessageType.Broadcast));
                }
                ActiveVoteLeader = null;
            }
        }

        private void UpdateAllMembers()
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
                member.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));
        }

        private void SendMessageAndUpdate(string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
            {
                member.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Fellowship));

                member.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));
            }
        }

        private void SendBroadcastAndUpdate(string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
            {
                member.Session.Network.EnqueueSend(new GameEventChannelBroadcast(member.Session, Channel.FellowBroadcast, "", message));

                member.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));
            }
        }

        public void BroadcastToFellow(string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
                member.Session.Network.EnqueueSend(new GameEventChannelBroadcast(member.Session, Channel.FellowBroadcast, "", message));
        }

        public void TellFellow(WorldObject sender, string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
                member.Session.Network.EnqueueSend(new GameEventChannelBroadcast(member.Session, Channel.Fellow, sender.Name, message));
        }

        private void SendWeenieErrorWithStringAndUpdate(WeenieErrorWithString error, string message)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var member in fellowshipMembers.Values)
            {
                member.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(member.Session, error, message));

                member.Session.Network.EnqueueSend(new GameEventFellowshipFullUpdate(member.Session));
            }
        }

        public void QuitFellowship(Player player, bool disband)
        {
            if (player == null) return;

            if (player.Guid.Full == FellowshipLeaderGuid)
            {
                if (disband)
                {
                    // CONQUEST: Notify online players in waiting queue that fellowship was disbanded
                    foreach (var queueEntry in WaitingQueue)
                    {
                        var queuedPlayer = PlayerManager.GetOnlinePlayer(queueEntry.CharacterId);
                        if (queuedPlayer?.Session != null)
                        {
                            queuedPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                $"[FSHIP]: {FellowshipName} has been disbanded. You have been removed from the queue.",
                                ChatMessageType.Broadcast));
                        }
                    }
                    WaitingQueue.Clear();

                    var fellowshipMembers = GetFellowshipMembers();

                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameEventFellowshipDisband(member.Session));

                        if (ShareLoot)
                        {
                            member.Session.Network.EnqueueSend(new GameMessageSystemChat("You no longer have permission to loot anyone else's kills.", ChatMessageType.Broadcast));

                            // you would expect this occur, but it did not in retail pcaps
                            //foreach (var fellow in fellowshipMembers.Values)
                            //    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"{fellow.Name} does not have permission to loot your kills.", ChatMessageType.Broadcast));
                        }

                        member.Fellowship = null;
                    }
                }
                else
                {
                    FellowshipMembers.Remove(player.Guid.Full);

                    // CONQUEST: Always track departure time for 2-minute rejoin window (not just locked fellowships)
                    var timestamp = (int)Time.GetUnixTime();
                    if (!DepartedMembers.TryAdd(player.Guid.Full, timestamp))
                        DepartedMembers[player.Guid.Full] = timestamp;

                    player.Fellowship = null;

                    // CONQUEST: Cancel any votes involving this player
                    CancelVotesForPlayer(player.Guid.Full, player.Name);

                    player.Session.Network.EnqueueSend(new GameEventFellowshipQuit(player.Session, player.Guid.Full));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("You no longer have permission to loot anyone else's kills.", ChatMessageType.Broadcast));

                    var fellowshipMembers = GetFellowshipMembers();

                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameEventFellowshipQuit(member.Session, player.Guid.Full));

                        if (ShareLoot)
                        {
                            member.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have lost permission to loot the kills of {player.Name}.", ChatMessageType.Broadcast));
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} does not have permission to loot your kills.", ChatMessageType.Broadcast));
                        }
                    }
                    AssignNewLeader(null, null);

                    CalculateXPSharing();

                    // CONQUEST: Process queue - it will delay if recent departures exist
                    ProcessWaitingQueue();
                }
            }
            else if (!disband)
            {
                FellowshipMembers.Remove(player.Guid.Full);

                // CONQUEST: Always track departure time for 2-minute rejoin window (not just locked fellowships)
                var timestamp = (int)Time.GetUnixTime();
                if (!DepartedMembers.TryAdd(player.Guid.Full, timestamp))
                    DepartedMembers[player.Guid.Full] = timestamp;

                // CONQUEST: Cancel any votes involving this player
                CancelVotesForPlayer(player.Guid.Full, player.Name);

                player.Session.Network.EnqueueSend(new GameEventFellowshipQuit(player.Session, player.Guid.Full));

                var fellowshipMembers = GetFellowshipMembers();

                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameEventFellowshipQuit(member.Session, player.Guid.Full));

                    if (ShareLoot)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have lost permission to loot the kills of {player.Name}.", ChatMessageType.Broadcast));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{member.Name} does not have permission to loot your kills.", ChatMessageType.Broadcast));
                    }
                }

                player.Fellowship = null;

                CalculateXPSharing();

                // CONQUEST: Process queue - it will delay if recent departures exist
                ProcessWaitingQueue();
            }
        }

        public void AssignNewLeader(Player oldLeader, Player newLeader)
        {
            if (newLeader != null)
            {
                FellowshipLeaderGuid = newLeader.Guid.Full;

                if (oldLeader != null)
                    oldLeader.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(oldLeader.Session, WeenieErrorWithString.YouHavePassedFellowshipLeadershipTo_, newLeader.Name));

                SendWeenieErrorWithStringAndUpdate(WeenieErrorWithString._IsNowLeaderOfFellowship, newLeader.Name);
            }
            else
            {
                // leader has dropped, assign new random leader
                var fellowshipMembers = GetFellowshipMembers();

                if (fellowshipMembers.Count == 0) return;

                var rng = ThreadSafeRandom.Next(0, fellowshipMembers.Count - 1);

                var fellowGuids = fellowshipMembers.Keys.ToList();

                FellowshipLeaderGuid = fellowGuids[rng];

                var newLeaderName = fellowshipMembers[FellowshipLeaderGuid].Name;

                if (oldLeader != null)
                    oldLeader.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(oldLeader.Session, WeenieErrorWithString.YouHavePassedFellowshipLeadershipTo_, newLeaderName));

                SendWeenieErrorWithStringAndUpdate(WeenieErrorWithString._IsNowLeaderOfFellowship, newLeaderName);
            }
        }

        public void UpdateOpenness(bool isOpen)
        {
            Open = isOpen;
            var openness = Open ? WeenieErrorWithString._IsNowOpenFellowship : WeenieErrorWithString._IsNowClosedFellowship;
            SendWeenieErrorWithStringAndUpdate(openness, FellowshipName);
        }

        public void UpdateLock(bool isLocked, string lockName)
        {
            // Unlocking a fellowship is not possible without disbanding in retail worlds, so in all likelihood, this is only firing for fellowships being locked by emotemanager

            IsLocked = isLocked;

            if (string.IsNullOrWhiteSpace(lockName))
                lockName = "Undefined";

            if (isLocked)
            {
                Open = false;

                DepartedMembers.Clear();

                // NOTE: Don't clear WaitingQueue here - fellowships auto-lock when full,
                // and we want queued players to remain in queue waiting for a spot to open

                var timestamp = Time.GetUnixTime();
                if (!FellowshipLocks.TryAdd(lockName, new FellowshipLockData(timestamp)))
                    FellowshipLocks[lockName].UpdateTimestamp(timestamp);

                SendBroadcastAndUpdate("Your fellowship is now locked.  You may not recruit new members.  If you leave the fellowship, you have 15 minutes to be recruited back into the fellowship.");
            }
            else
            {
                // Unlocking a fellowship is not possible without disbanding in retail worlds, so in all likelihood, this never occurs

                DepartedMembers.Clear();

                FellowshipLocks.Remove(lockName);

                SendBroadcastAndUpdate("Your fellowship is now unlocked.");
            }
        }

        /// <summary>
        /// Calculates fellowship XP sharing (ShareXP, EvenShare) from fellow levels
        /// </summary>
        private void CalculateXPSharing()
        {
            // CONQUEST: All members over level 1 can share XP equally regardless of level difference

            var fellows = GetFellowshipMembers();

            // If all members are over level 1, enable XP sharing with even distribution
            var allOverLevelOne = !fellows.Values.Any(f => (f.Level ?? 1) <= 1);

            if (allOverLevelOne)
            {
                ShareXP = DesiredShareXP;
                EvenShare = true;
                return;
            }

            // If anyone is level 1 or below, disable XP sharing
            ShareXP = false;
            EvenShare = false;
        }

        /// <summary>
        /// Splits XP amongst fellowship members, depending on XP type and fellow settings
        /// </summary>
        /// <param name="amount">The input amount of XP</param>
        /// <param name="xpType">The type of XP (quest XP is handled differently)</param>
        /// <param name="player">The fellowship member who originated the XP</param>
        public void SplitXp(ulong amount, XpType xpType, ShareType shareType, Player player)
        {
            // https://asheron.fandom.com/wiki/Announcements_-_2002/02_-_Fever_Dreams#Letter_to_the_Players_1

            var fellowshipMembers = GetFellowshipMembers();

            shareType &= ~ShareType.Fellowship;

            // CONQUEST: Pre-calculate which members are in range for dynamic scaling
            var membersInRange = new List<Player>();
            foreach (var member in fellowshipMembers.Values)
            {
                if (GetDistanceScalar(player, member, xpType) > 0)
                    membersInRange.Add(member);
            }

            // quest turn-ins: flat share (retail default)
            if (xpType == XpType.Quest && !PropertyManager.GetBool("fellow_quest_bonus"))
            {
                // CONQUEST: Only split among members in range
                if (membersInRange.Count == 0)
                    return;

                var perAmount = (long)amount / membersInRange.Count;

                foreach (var member in membersInRange)
                {
                    var fellowXpType = player == member ? XpType.Quest : XpType.Fellowship;

                    member.GrantXP(perAmount, fellowXpType, shareType);
                }
            }

            // divides XP evenly to all the sharable fellows within level range,
            // but with a significant boost to the amount of xp, based on # of fellowship members
            else if (EvenShare)
            {
                // CONQUEST: Scale XP bonus based on members actually in range, not total fellowship size
                if (membersInRange.Count == 0)
                    return;

                var totalAmount = (ulong)Math.Round(amount * GetMemberSharePercent(membersInRange.Count));

                foreach (var member in membersInRange)
                {
                    var fellowXpType = player == member ? xpType : XpType.Fellowship;

                    member.GrantXP((long)totalAmount, fellowXpType, shareType);
                }

                return;
            }

            // divides XP to all sharable fellows within level range
            // based on each fellowship member's level
            else
            {
                // CONQUEST: Only consider members in range for level-based distribution
                if (membersInRange.Count == 0)
                    return;

                var levelXPSum = membersInRange.Select(p => p.GetXPToNextLevel(p.Level.Value)).Sum();

                foreach (var member in membersInRange)
                {
                    var levelXPScale = (double)member.GetXPToNextLevel(member.Level.Value) / levelXPSum;

                    var playerTotal = (ulong)Math.Round(amount * levelXPScale);

                    var fellowXpType = player == member ? xpType : XpType.Fellowship;

                    member.GrantXP((long)playerTotal, fellowXpType, shareType);
                }
            }
        }

        /// <summary>
        /// Splits luminance amongst fellowship members, depending on XP type and fellow settings
        /// </summary>
        /// <param name="amount">The input amount of luminance</param>
        /// <param name="xpType">The type of luminance (quest luminance is handled differently)</param>
        /// <param name="player">The fellowship member who originated the luminance</param>
        public void SplitLuminance(ulong amount, XpType xpType, ShareType shareType, Player player)
        {
            // https://asheron.fandom.com/wiki/Announcements_-_2002/02_-_Fever_Dreams#Letter_to_the_Players_1

            var fellowshipMembers = GetFellowshipMembers();

            shareType &= ~ShareType.Fellowship;

            // CONQUEST: Pre-calculate which members are in range for dynamic scaling
            var membersInRange = new List<Player>();
            foreach (var member in fellowshipMembers.Values)
            {
                if (GetDistanceScalar(player, member, xpType) > 0)
                    membersInRange.Add(member);
            }

            // quest luminance: flat share (like quest XP)
            if (xpType == XpType.Quest && !PropertyManager.GetBool("fellow_quest_bonus"))
            {
                // CONQUEST: Only split among members in range
                if (membersInRange.Count == 0)
                    return;

                var perAmount = (long)amount / membersInRange.Count;

                foreach (var member in membersInRange)
                {
                    var fellowXpType = player == member ? XpType.Quest : XpType.Fellowship;

                    member.GrantLuminance(perAmount, fellowXpType, shareType);
                }
            }

            // divides luminance evenly to all the sharable fellows within level range,
            // but with a significant boost to the amount, based on # of fellowship members
            else if (EvenShare)
            {
                // CONQUEST: Scale luminance bonus based on members actually in range, not total fellowship size
                if (membersInRange.Count == 0)
                    return;

                var totalAmount = (ulong)Math.Round(amount * GetMemberSharePercent(membersInRange.Count));

                foreach (var member in membersInRange)
                {
                    var fellowXpType = player == member ? xpType : XpType.Fellowship;

                    member.GrantLuminance((long)totalAmount, fellowXpType, shareType);
                }

                return;
            }

            // divides luminance to all sharable fellows within level range
            // based on each fellowship member's level
            else
            {
                // CONQUEST: Only consider members in range for level-based distribution
                if (membersInRange.Count == 0)
                    return;

                var levelXPSum = membersInRange.Select(p => p.GetXPToNextLevel(p.Level.Value)).Sum();

                foreach (var member in membersInRange)
                {
                    var levelXPScale = (double)member.GetXPToNextLevel(member.Level.Value) / levelXPSum;

                    var playerTotal = (ulong)Math.Round(amount * levelXPScale);

                    var fellowXpType = player == member ? xpType : XpType.Fellowship;

                    member.GrantLuminance((long)playerTotal, fellowXpType, shareType);
                }
            }
        }

        internal double GetMemberSharePercent()
        {
            var fellowshipMembers = GetFellowshipMembers();
            return GetMemberSharePercent(fellowshipMembers.Count);
        }

        /// <summary>
        /// CONQUEST: Overload that accepts member count for dynamic scaling based on members in range
        /// </summary>
        internal double GetMemberSharePercent(int memberCount)
        {
            switch (memberCount)
            {
                case 1:
                    return 1.0;
                case 2:
                    return .75;
                case 3:
                    return .6;
                case 4:
                    return .55;
                case 5:
                    return .5;
                case 6:
                    return .45;
                case 7:
                    return .4;
                case 8:
                    return .35;
                case 9:
                    return .32;
                case 10:
                    return .3;
                case 11:
                    return .3;
                case 12:
                    return .3;
                case 13:
                    return .3;
                case 14:
                    return .3;
                default:
                    return .3; // 15+ members continue at 30%
            }
        }

        public const int MaxDistance = 600;

        /// <summary>
        /// Returns the amount to scale the XP for a fellow
        /// based on distance from the earner
        /// </summary>
        public double GetDistanceScalar(Player earner, Player fellow, XpType xpType)
        {
            if (earner == null || fellow == null)
                return 0.0f;

            if (xpType == XpType.Quest)
                return 1.0f;

            // https://asheron.fandom.com/wiki/Announcements_-_2004/01_-_Mirror,_Mirror#Rollout_Article

            // If they are indoors while you are outdoors, or vice-versa.
            if (earner.Location.Indoors != fellow.Location.Indoors)
                return 0.0f;

            // If you are both indoors but in different landblocks or different variations.
            if (earner.Location.Indoors && fellow.Location.Indoors &&
                (earner.Location.Landblock != fellow.Location.Landblock || earner.Location.Variation != fellow.Location.Variation))
                return 0.0f;

            // CONQUEST: If both players are in the same dungeon (indoor landblock AND same variation), full XP regardless of distance
            // This allows XP sharing across the entire dungeon even if one player is at the top and another at the bottom
            if (earner.Location.Indoors && fellow.Location.Indoors &&
                earner.Location.Landblock == fellow.Location.Landblock && earner.Location.Variation == fellow.Location.Variation)
                return 1.0f;

            // Outdoor distance scaling
            var dist = earner.Location.Distance2D(fellow.Location);

            if (dist >= MaxDistance * 2.0f)
                return 0.0f;

            if (dist <= MaxDistance)
                return 1.0f;

            var scalar = 1.0f - (dist - MaxDistance) / MaxDistance;

            return Math.Max(0.0f, scalar);
        }

        /// <summary>
        /// Returns fellows within sharing range
        /// CONQUEST: Same dungeon = always in range, outdoor uses distance scaling
        /// </summary>
        public List<Player> WithinRange(Player player, bool includeSelf = false)
        {
            var fellows = GetFellowshipMembers();

            var results = new List<Player>();

            foreach (var fellow in fellows.Values)
            {
                if (player == fellow && !includeSelf)
                    continue;

                // CONQUEST: If both players are in the same dungeon (indoor landblock AND same variation), always in range
                if (player.Location.Indoors && fellow.Location.Indoors &&
                    player.Location.Landblock == fellow.Location.Landblock && player.Location.Variation == fellow.Location.Variation)
                {
                    results.Add(fellow);
                    continue;
                }

                // If one is indoor and one is outdoor, not in range
                if (player.Location.Indoors != fellow.Location.Indoors)
                    continue;

                // If both are indoors but different landblocks or different variations, not in range
                if (player.Location.Indoors && fellow.Location.Indoors &&
                    (player.Location.Landblock != fellow.Location.Landblock || player.Location.Variation != fellow.Location.Variation))
                    continue;

                // Outdoor: use distance check
                var dist = player.Location.Distance2D(fellow.Location);
                if (dist <= MaxDistance)
                    results.Add(fellow);
            }
            return results;
        }

        /// <summary>
        /// Called when someone in the fellowship levels up
        /// </summary>
        public void OnFellowLevelUp(Player player)
        {
            CalculateXPSharing();

            var fellowshipMembers = GetFellowshipMembers();

            foreach (var fellow in fellowshipMembers.Values)
            {
                if (fellow == player)
                    continue;

                fellow.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} is now level {player.Level}!", ChatMessageType.Broadcast));
            }
        }

        public void OnVitalUpdate(Player player)
        {
            // cap max update interval?

            var fellowshipMembers = GetFellowshipMembers();

            foreach (var fellow in fellowshipMembers.Values)
            {
                if (fellow.FellowshipPanelOpen)
                    fellow.Session.Network.EnqueueSend(new GameEventFellowshipUpdateFellow(fellow.Session, player, ShareLoot, FellowUpdateType.Vitals));
            }
        }

        public void OnDeath(Player player)
        {
            var fellowshipMembers = GetFellowshipMembers();

            foreach (var fellow in fellowshipMembers.Values)
            {
                if (fellow != player)
                    fellow.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your fellow {player.Name} has died!", ChatMessageType.Broadcast));
            }
        }

        public Dictionary<uint, Player> GetFellowshipMembers()
        {
            var results = new Dictionary<uint, Player>();
            var dropped = new HashSet<uint>();

            foreach (var kvp in FellowshipMembers)
            {
                var playerGuid = kvp.Key;
                var playerRef = kvp.Value;

                playerRef.TryGetTarget(out var player);

                if (player != null && player.Session != null && player.Session.Player != null && player.Fellowship != null)
                    results.Add(playerGuid, player);
                else
                    dropped.Add(playerGuid);
            }

            // TODO: process dropped list
            if (dropped.Count > 0)
                ProcessDropList(FellowshipMembers, dropped);

            return results;
        }

        public void ProcessDropList(Dictionary<uint, WeakReference<Player>> fellowshipMembers, HashSet<uint> fellowGuids)
        {
            foreach (var fellowGuid in fellowGuids)
            {
                var offlinePlayer = PlayerManager.FindByGuid(fellowGuid);
                var offlineName = offlinePlayer != null ? offlinePlayer.Name : "Unknown";

                log.Warn($"Dropped fellow: {offlineName}");
                fellowshipMembers.Remove(fellowGuid);

                // CONQUEST: Track departure time so crashed players get 2-minute rejoin window
                // Always track, even for locked fellowships - they use the same DepartedMembers
                // dictionary but with a longer timeout (15 min for locked, 2 min for unlocked)
                var timestamp = (int)Time.GetUnixTime();
                if (!DepartedMembers.TryAdd(fellowGuid, timestamp))
                    DepartedMembers[fellowGuid] = timestamp;

                // CONQUEST: Cancel any votes involving this player
                CancelVotesForPlayer(fellowGuid, offlineName);
            }
            if (fellowGuids.Contains(FellowshipLeaderGuid))
                AssignNewLeader(null, null);

            CalculateXPSharing();
            UpdateAllMembers();
        }

        #region CONQUEST: Vote Kick System

        /// <summary>
        /// CONQUEST: Start a vote to kick a player from the fellowship
        /// </summary>
        public bool StartVoteKick(Player initiator, Player target)
        {
            if (initiator == null || target == null)
                return false;

            // Can't vote kick yourself
            if (initiator.Guid.Full == target.Guid.Full)
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You cannot vote to kick yourself.", ChatMessageType.Broadcast));
                return false;
            }

            // Can't kick the leader via vote - use /voteleader instead
            if (target.Guid.Full == FellowshipLeaderGuid)
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You cannot vote to kick the fellowship leader. Use '/voteleader' to vote for new leadership instead.", ChatMessageType.Broadcast));
                return false;
            }

            // Check if target is in fellowship
            if (!FellowshipMembers.ContainsKey(target.Guid.Full))
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {target.Name} is not in your fellowship.", ChatMessageType.Broadcast));
                return false;
            }

            // Check if there's already an active vote kick
            if (ActiveVoteKick != null && !ActiveVoteKick.IsExpired())
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: A vote kick for {ActiveVoteKick.TargetName} is already in progress. Wait for it to complete.", ChatMessageType.Broadcast));
                return false;
            }

            // Check if there's an active leadership vote
            if (ActiveVoteLeader != null && !ActiveVoteLeader.IsExpired())
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: A leadership vote is in progress. Wait for it to complete.", ChatMessageType.Broadcast));
                return false;
            }

            // Need at least 3 people for a vote (initiator, target, and at least one other voter)
            if (FellowshipMembers.Count < 3)
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: Fellowship must have at least 3 members to start a vote kick.", ChatMessageType.Broadcast));
                return false;
            }

            // Start the vote
            ActiveVoteKick = new VoteKickSession(target.Guid.Full, target.Name, initiator.Guid.Full, FellowshipMembers.Count);

            // Initiator automatically votes yes
            ActiveVoteKick.VotesYes.Add(initiator.Guid.Full);

            // Notify all fellowship members
            var fellowshipMembers = GetFellowshipMembers();
            foreach (var member in fellowshipMembers.Values)
            {
                if (member.Guid.Full == target.Guid.Full)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {initiator.Name} has started a vote to kick you from the fellowship! Members have 60 seconds to vote.", ChatMessageType.Broadcast));
                }
                else if (member.Guid.Full == initiator.Guid.Full)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: You started a vote to kick {target.Name}. Your vote: YES. Waiting for others... (60 seconds)", ChatMessageType.Broadcast));
                }
                else
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {initiator.Name} started a vote to kick {target.Name}. Type '/votekick yes' or '/votekick no' to vote. (60 seconds)", ChatMessageType.Broadcast));
                }
            }

            return true;
        }

        /// <summary>
        /// CONQUEST: Cast a vote in the active vote kick
        /// </summary>
        public void CastVote(Player voter, bool voteYes)
        {
            if (voter == null)
                return;

            // Must be in the fellowship to vote
            if (!FellowshipMembers.ContainsKey(voter.Guid.Full))
            {
                voter.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You are not in this fellowship.", ChatMessageType.Broadcast));
                return;
            }

            if (ActiveVoteKick == null || ActiveVoteKick.IsExpired())
            {
                voter.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: There is no active vote kick.", ChatMessageType.Broadcast));
                return;
            }

            // Can't vote if you're the target
            if (voter.Guid.Full == ActiveVoteKick.TargetGuid)
            {
                voter.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You cannot vote on your own kick.", ChatMessageType.Broadcast));
                return;
            }

            // Check if already voted
            if (ActiveVoteKick.VotesYes.Contains(voter.Guid.Full) || ActiveVoteKick.VotesNo.Contains(voter.Guid.Full))
            {
                voter.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You have already voted.", ChatMessageType.Broadcast));
                return;
            }

            // Cast vote
            if (voteYes)
                ActiveVoteKick.VotesYes.Add(voter.Guid.Full);
            else
                ActiveVoteKick.VotesNo.Add(voter.Guid.Full);

            voter.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Your vote: {(voteYes ? "YES" : "NO")}", ChatMessageType.Broadcast));

            // Check if vote is complete
            CheckVoteKickResult();
        }

        /// <summary>
        /// CONQUEST: Check if vote kick has enough votes to pass or fail
        /// Uses majority of votes CAST (not eligible voters) to handle AFK players
        /// </summary>
        public void CheckVoteKickResult()
        {
            if (ActiveVoteKick == null)
                return;

            var eligibleVoters = FellowshipMembers.Count - 1; // Exclude target
            var yesVotes = ActiveVoteKick.VotesYes.Count;
            var noVotes = ActiveVoteKick.VotesNo.Count;
            var totalVotes = yesVotes + noVotes;
            var remainingVoters = eligibleVoters - totalVotes;

            // Check if vote passed: yes > no AND mathematically impossible for no to catch up
            if (yesVotes > noVotes && yesVotes > (noVotes + remainingVoters))
            {
                // Vote passed - kick the player
                var fellowshipMembers = GetFellowshipMembers();

                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Vote passed ({yesVotes} yes, {noVotes} no). {ActiveVoteKick.TargetName} has been kicked from the fellowship.", ChatMessageType.Broadcast));
                }

                // Find and remove the target
                if (FellowshipMembers.TryGetValue(ActiveVoteKick.TargetGuid, out var targetRef) && targetRef.TryGetTarget(out var targetPlayer))
                {
                    RemoveFellowshipMember(targetPlayer, null);
                }

                ActiveVoteKick = null;
                return;
            }

            // Check if vote failed: no >= yes AND mathematically impossible for yes to win
            if (noVotes >= yesVotes && noVotes >= (yesVotes + remainingVoters))
            {
                var fellowshipMembers = GetFellowshipMembers();
                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Vote failed ({yesVotes} yes, {noVotes} no). {ActiveVoteKick.TargetName} stays in the fellowship.", ChatMessageType.Broadcast));
                }
                ActiveVoteKick = null;
                return;
            }

            // Check if everyone has voted
            if (totalVotes >= eligibleVoters)
            {
                var fellowshipMembers = GetFellowshipMembers();
                // Majority of cast votes wins
                if (yesVotes > noVotes)
                {
                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Vote passed ({yesVotes} yes, {noVotes} no). {ActiveVoteKick.TargetName} has been kicked from the fellowship.", ChatMessageType.Broadcast));
                    }

                    if (FellowshipMembers.TryGetValue(ActiveVoteKick.TargetGuid, out var targetRef) && targetRef.TryGetTarget(out var targetPlayer))
                    {
                        RemoveFellowshipMember(targetPlayer, null);
                    }
                }
                else
                {
                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Vote failed ({yesVotes} yes, {noVotes} no). {ActiveVoteKick.TargetName} stays in the fellowship.", ChatMessageType.Broadcast));
                    }
                }
                ActiveVoteKick = null;
            }
        }

        /// <summary>
        /// CONQUEST: Called periodically to check for expired votes
        /// Uses majority of votes CAST to determine outcome at timeout
        /// </summary>
        public void ProcessVoteKickTimeout()
        {
            if (ActiveVoteKick == null)
                return;

            if (ActiveVoteKick.IsExpired())
            {
                var yesVotes = ActiveVoteKick.VotesYes.Count;
                var noVotes = ActiveVoteKick.VotesNo.Count;

                var fellowshipMembers = GetFellowshipMembers();

                // Majority of cast votes wins
                if (yesVotes > noVotes)
                {
                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Vote passed ({yesVotes} yes, {noVotes} no). {ActiveVoteKick.TargetName} has been kicked.", ChatMessageType.Broadcast));
                    }

                    if (FellowshipMembers.TryGetValue(ActiveVoteKick.TargetGuid, out var targetRef) && targetRef.TryGetTarget(out var targetPlayer))
                    {
                        RemoveFellowshipMember(targetPlayer, null);
                    }
                }
                else
                {
                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Vote failed ({yesVotes} yes, {noVotes} no). {ActiveVoteKick.TargetName} stays in the fellowship.", ChatMessageType.Broadcast));
                    }
                }

                ActiveVoteKick = null;
            }
        }

        #endregion

        #region CONQUEST: Vote Leader System

        /// <summary>
        /// CONQUEST: Start a vote to transfer leadership to the initiator
        /// </summary>
        public bool StartVoteLeader(Player initiator)
        {
            if (initiator == null)
                return false;

            // Can't vote for leadership if you're already the leader
            if (initiator.Guid.Full == FellowshipLeaderGuid)
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You are already the fellowship leader.", ChatMessageType.Broadcast));
                return false;
            }

            // Check if initiator is in fellowship
            if (!FellowshipMembers.ContainsKey(initiator.Guid.Full))
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You are not in a fellowship.", ChatMessageType.Broadcast));
                return false;
            }

            // Check if there's already an active vote
            if (ActiveVoteLeader != null && !ActiveVoteLeader.IsExpired())
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: A vote for leadership is already in progress. Wait for it to complete.", ChatMessageType.Broadcast));
                return false;
            }

            // Check if there's an active kick vote
            if (ActiveVoteKick != null && !ActiveVoteKick.IsExpired())
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: A vote kick is in progress. Wait for it to complete.", ChatMessageType.Broadcast));
                return false;
            }

            // Need at least 2 people for a vote
            if (FellowshipMembers.Count < 2)
            {
                initiator.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: Fellowship must have at least 2 members to start a leadership vote.", ChatMessageType.Broadcast));
                return false;
            }

            // Start the vote
            ActiveVoteLeader = new VoteLeaderSession(initiator.Guid.Full, initiator.Name, FellowshipLeaderGuid, FellowshipMembers.Count);

            // Initiator automatically votes yes
            ActiveVoteLeader.VotesYes.Add(initiator.Guid.Full);

            // Notify all fellowship members
            var fellowshipMembers = GetFellowshipMembers();
            foreach (var member in fellowshipMembers.Values)
            {
                if (member.Guid.Full == initiator.Guid.Full)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: You started a vote to become the fellowship leader. Your vote: YES. Waiting for others... (60 seconds)", ChatMessageType.Broadcast));
                }
                else if (member.Guid.Full == FellowshipLeaderGuid)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {initiator.Name} has started a vote to take leadership of the fellowship! Type '/voteleader yes' or '/voteleader no' to vote. (60 seconds)", ChatMessageType.Broadcast));
                }
                else
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {initiator.Name} started a vote to become the new fellowship leader. Type '/voteleader yes' or '/voteleader no' to vote. (60 seconds)", ChatMessageType.Broadcast));
                }
            }

            return true;
        }

        /// <summary>
        /// CONQUEST: Cast a vote in the active vote leader
        /// </summary>
        public void CastVoteLeader(Player voter, bool voteYes)
        {
            if (voter == null)
                return;

            // Must be in the fellowship to vote
            if (!FellowshipMembers.ContainsKey(voter.Guid.Full))
            {
                voter.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You are not in this fellowship.", ChatMessageType.Broadcast));
                return;
            }

            if (ActiveVoteLeader == null || ActiveVoteLeader.IsExpired())
            {
                voter.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: There is no active leadership vote.", ChatMessageType.Broadcast));
                return;
            }

            // Check if already voted
            if (ActiveVoteLeader.VotesYes.Contains(voter.Guid.Full) || ActiveVoteLeader.VotesNo.Contains(voter.Guid.Full))
            {
                voter.Session.Network.EnqueueSend(new GameMessageSystemChat("[FSHIP]: You have already voted.", ChatMessageType.Broadcast));
                return;
            }

            // Cast vote
            if (voteYes)
                ActiveVoteLeader.VotesYes.Add(voter.Guid.Full);
            else
                ActiveVoteLeader.VotesNo.Add(voter.Guid.Full);

            voter.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Your vote: {(voteYes ? "YES" : "NO")}", ChatMessageType.Broadcast));

            // Check if vote is complete
            CheckVoteLeaderResult();
        }

        /// <summary>
        /// CONQUEST: Check if vote leader has enough votes to pass or fail
        /// </summary>
        public void CheckVoteLeaderResult()
        {
            if (ActiveVoteLeader == null)
                return;

            var eligibleVoters = FellowshipMembers.Count;
            var yesVotes = ActiveVoteLeader.VotesYes.Count;
            var noVotes = ActiveVoteLeader.VotesNo.Count;
            var totalVotes = yesVotes + noVotes;
            var remainingVoters = eligibleVoters - totalVotes;

            // Check if vote passed: yes > no AND mathematically impossible for no to catch up
            if (yesVotes > noVotes && yesVotes > (noVotes + remainingVoters))
            {
                // Vote passed - transfer leadership
                TransferLeadershipFromVote();
                return;
            }

            // Check if vote failed: no >= yes AND mathematically impossible for yes to win
            if (noVotes >= yesVotes && noVotes >= (yesVotes + remainingVoters))
            {
                var fellowshipMembers = GetFellowshipMembers();
                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Leadership vote failed ({yesVotes} yes, {noVotes} no). Leadership remains unchanged.", ChatMessageType.Broadcast));
                }
                ActiveVoteLeader = null;
                return;
            }

            // Check if everyone has voted
            if (totalVotes >= eligibleVoters)
            {
                var fellowshipMembers = GetFellowshipMembers();

                // Majority of cast votes wins
                if (yesVotes > noVotes)
                {
                    TransferLeadershipFromVote();
                }
                else
                {
                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Leadership vote failed ({yesVotes} yes, {noVotes} no). Leadership remains unchanged.", ChatMessageType.Broadcast));
                    }
                    ActiveVoteLeader = null;
                }
            }
        }

        /// <summary>
        /// CONQUEST: Transfer leadership after successful vote
        /// </summary>
        private void TransferLeadershipFromVote()
        {
            if (ActiveVoteLeader == null)
                return;

            var yesVotes = ActiveVoteLeader.VotesYes.Count;
            var noVotes = ActiveVoteLeader.VotesNo.Count;
            var candidateName = ActiveVoteLeader.CandidateName;
            var candidateGuid = ActiveVoteLeader.CandidateGuid;

            var fellowshipMembers = GetFellowshipMembers();

            // Find candidate player
            Player newLeader = null;
            if (FellowshipMembers.TryGetValue(candidateGuid, out var candidateRef) && candidateRef.TryGetTarget(out var candidate))
            {
                newLeader = candidate;
            }

            if (newLeader != null)
            {
                // Find old leader
                Player oldLeader = null;
                if (FellowshipMembers.TryGetValue(FellowshipLeaderGuid, out var leaderRef) && leaderRef.TryGetTarget(out var leader))
                {
                    oldLeader = leader;
                }

                // Transfer leadership
                AssignNewLeader(oldLeader, newLeader);

                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Leadership vote passed ({yesVotes} yes, {noVotes} no). {candidateName} is now the fellowship leader!", ChatMessageType.Broadcast));
                }
            }
            else
            {
                foreach (var member in fellowshipMembers.Values)
                {
                    member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Leadership vote passed but {candidateName} is no longer available. Leadership unchanged.", ChatMessageType.Broadcast));
                }
            }

            ActiveVoteLeader = null;
        }

        /// <summary>
        /// CONQUEST: Called periodically to check for expired leadership votes
        /// </summary>
        public void ProcessVoteLeaderTimeout()
        {
            if (ActiveVoteLeader == null)
                return;

            if (ActiveVoteLeader.IsExpired())
            {
                var yesVotes = ActiveVoteLeader.VotesYes.Count;
                var noVotes = ActiveVoteLeader.VotesNo.Count;

                // Majority of cast votes wins
                if (yesVotes > noVotes)
                {
                    TransferLeadershipFromVote();
                }
                else
                {
                    var fellowshipMembers = GetFellowshipMembers();
                    foreach (var member in fellowshipMembers.Values)
                    {
                        member.Session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Leadership vote timed out ({yesVotes} yes, {noVotes} no). Leadership remains unchanged.", ChatMessageType.Broadcast));
                    }
                    ActiveVoteLeader = null;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// CONQUEST: Tracks an active vote kick session
    /// </summary>
    public class VoteKickSession
    {
        public uint TargetGuid { get; set; }
        public string TargetName { get; set; }
        public uint InitiatorGuid { get; set; }
        public DateTime StartTime { get; set; }
        public int TotalMembers { get; set; }
        public HashSet<uint> VotesYes { get; set; }
        public HashSet<uint> VotesNo { get; set; }

        public const int VoteTimeoutSeconds = 60;

        public VoteKickSession(uint targetGuid, string targetName, uint initiatorGuid, int totalMembers)
        {
            TargetGuid = targetGuid;
            TargetName = targetName;
            InitiatorGuid = initiatorGuid;
            TotalMembers = totalMembers;
            StartTime = DateTime.UtcNow;
            VotesYes = new HashSet<uint>();
            VotesNo = new HashSet<uint>();
        }

        public bool IsExpired()
        {
            return DateTime.UtcNow > StartTime.AddSeconds(VoteTimeoutSeconds);
        }
    }

    /// <summary>
    /// CONQUEST: Tracks an active vote leader session
    /// </summary>
    public class VoteLeaderSession
    {
        public uint CandidateGuid { get; set; }
        public string CandidateName { get; set; }
        public uint CurrentLeaderGuid { get; set; }
        public DateTime StartTime { get; set; }
        public int TotalMembers { get; set; }
        public HashSet<uint> VotesYes { get; set; }
        public HashSet<uint> VotesNo { get; set; }

        public const int VoteTimeoutSeconds = 60;

        public VoteLeaderSession(uint candidateGuid, string candidateName, uint currentLeaderGuid, int totalMembers)
        {
            CandidateGuid = candidateGuid;
            CandidateName = candidateName;
            CurrentLeaderGuid = currentLeaderGuid;
            TotalMembers = totalMembers;
            StartTime = DateTime.UtcNow;
            VotesYes = new HashSet<uint>();
            VotesNo = new HashSet<uint>();
        }

        public bool IsExpired()
        {
            return DateTime.UtcNow > StartTime.AddSeconds(VoteTimeoutSeconds);
        }
    }

    public static class FellowshipExtensions
    {
        private static readonly HashComparer hashComparer = new HashComparer(32);

        public static void Write(this BinaryWriter writer, Dictionary<uint, int> departedFellows)
        {
            PackableHashTable.WriteHeader(writer, departedFellows.Count, hashComparer.NumBuckets);

            var sorted = new SortedDictionary<uint, int>(departedFellows, hashComparer);

            foreach (var departed in sorted)
            {
                writer.Write(departed.Key);
                writer.Write(departed.Value);
            }
        }
    }
}

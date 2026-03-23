using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Network.GameAction.Actions
{
    public static class GameActionTalkDirect
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [GameAction(GameActionType.TalkDirect)]
        public static void Handle(ClientMessage clientMessage, Session session)
        {
            var message = clientMessage.Payload.ReadString16L();
            var targetGuid = clientMessage.Payload.ReadUInt32();

            var creature = session.Player.CurrentLandblock?.GetObject(targetGuid) as Creature;
            if (creature == null)
            {
                var statusMessage = new GameEventWeenieError(session, WeenieError.CharacterNotAvailable);
                session.Network.EnqueueSend(statusMessage);
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"You tell {creature.Name}, \"{message}\"", ChatMessageType.OutgoingTell));

            if (creature is Player targetPlayer)
            {
                if (session.Player.IsGagged)
                {
                    session.Player.SendGagError();
                    return;
                }

                if (targetPlayer.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Tell))
                {
                    session.Network.EnqueueSend(new GameEventWeenieErrorWithString(session, WeenieErrorWithString.MessageBlocked_, $"{targetPlayer.Name} has you squelched."));
                    //log.Warn($"Tell from {session.Player.Name} (0x{session.Player.Guid.ToString()}) to {targetPlayer.Name} (0x{targetPlayer.Guid.ToString()}) blocked due to squelch");
                    return;
                }

                // CONQUEST: Auto-join fellowship by telling "xp" to any fellowship member
                if (message.Equals("xp", System.StringComparison.OrdinalIgnoreCase) && targetPlayer.Fellowship != null)
                {
                    var fellowship = targetPlayer.Fellowship;

                    // Check if sender is already in a fellowship
                    if (session.Player.Fellowship != null)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: You are already in a fellowship. Leave your current fellowship first.", ChatMessageType.Broadcast));
                        return;
                    }

                    // CONQUEST: Mules cannot join fellowships
                    if (session.Player.IsMule)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Mules cannot join fellowships.", ChatMessageType.Broadcast));
                        return;
                    }

                    // CONQUEST: Check if player is a recent departure - they can rejoin immediately, bypassing queue, lock, and landblock check
                    var isRecentDeparture = fellowship.IsRecentDeparture(session.Player.Guid.Full);

                    // CONQUEST: Landblock check - must be in same landblock as at least one fellowship member (recent departures bypass)
                    if (!isRecentDeparture)
                    {
                        var senderLandblock = session.Player.CurrentLandblock?.Id.Landblock;
                        var senderVariation = session.Player.Location?.Variation;

                        // Block if sender is in Marketplace
                        if (senderLandblock == 0x016C)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: You cannot join fellowships from the Marketplace.", ChatMessageType.Broadcast));
                            return;
                        }

                        bool inSameLandblock = false;
                        foreach (var memberEntry in fellowship.FellowshipMembers)
                        {
                            var member = PlayerManager.GetOnlinePlayer(memberEntry.Key);
                            if (member?.CurrentLandblock != null)
                            {
                                var memberLandblock = member.CurrentLandblock.Id.Landblock;
                                var memberVariation = member.Location?.Variation;

                                // Check landblock match and variation compatibility (same or one is null)
                                if (senderLandblock == memberLandblock &&
                                    (senderVariation == null || memberVariation == null || senderVariation == memberVariation))
                                {
                                    inSameLandblock = true;
                                    break;
                                }
                            }
                        }

                        if (!inSameLandblock)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: You must be in the same landblock as a fellowship member to join.", ChatMessageType.Broadcast));
                            return;
                        }
                    }

                    // Check if fellowship is locked or closed (recent departures can bypass this)
                    if (!isRecentDeparture)
                    {
                        if (fellowship.IsLocked)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {targetPlayer.Name}'s fellowship is locked and not accepting new members.", ChatMessageType.Broadcast));
                            return;
                        }
                        if (!fellowship.Open)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {targetPlayer.Name}'s fellowship is closed and not accepting new members.", ChatMessageType.Broadcast));
                            return;
                        }
                    }

                    // CONQUEST: Recent departures bypass the queue entirely - just send them straight to recruit
                    if (isRecentDeparture)
                    {
                        // Get the fellowship leader - only the leader can actually recruit
                        var leader = PlayerManager.GetOnlinePlayer(fellowship.FellowshipLeaderGuid);
                        if (leader == null)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Fellowship leader is not online. Cannot rejoin.", ChatMessageType.Broadcast));
                            return;
                        }
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Welcome back! Rejoining {leader.Name}'s fellowship...", ChatMessageType.Broadcast));
                        leader.FellowshipRecruit(session.Player);
                        return;
                    }

                    // CONQUEST: Process any pending queue entries first - this handles cases where
                    // departed member windows expired but no one joined/left to trigger queue processing
                    fellowship.ProcessWaitingQueue();

                    // Check if fellowship is full OR if there are people waiting in queue
                    // CONQUEST: Fix queue bypass - if people are waiting, new joiners must queue
                    var maxFellows = fellowship.GetMaxFellows();
                    var queueCount = fellowship.GetWaitingQueueCount();
                    if (fellowship.FellowshipMembers.Count >= maxFellows || queueCount > 0)
                    {
                        // Add to waiting queue
                        var position = fellowship.AddToWaitingQueue(session.Player);
                        if (fellowship.FellowshipMembers.Count >= maxFellows)
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat(
                                $"[FSHIP]: {targetPlayer.Name}'s fellowship is full ({fellowship.FellowshipMembers.Count}/{maxFellows}). You are #{position} in queue ({queueCount + 1} waiting).",
                                ChatMessageType.Broadcast));
                        }
                        else
                        {
                            session.Network.EnqueueSend(new GameMessageSystemChat(
                                $"[FSHIP]: {targetPlayer.Name}'s fellowship has {queueCount} player(s) waiting. You are #{position} in queue.",
                                ChatMessageType.Broadcast));
                        }
                        return;
                    }

                    session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Attempting to add you to {targetPlayer.Name}'s fellowship.", ChatMessageType.Broadcast));
                    targetPlayer.FellowshipRecruit(session.Player);
                    return;
                }

                var tell = new GameEventTell(targetPlayer.Session, message, session.Player.GetNameWithSuffix(), session.Player.Guid.Full, targetPlayer.Guid.Full, ChatMessageType.Tell);
                targetPlayer.Session.Network.EnqueueSend(tell);
            }
            else
                creature.EmoteManager.OnTalkDirect(session.Player, message);
        }
    }
}

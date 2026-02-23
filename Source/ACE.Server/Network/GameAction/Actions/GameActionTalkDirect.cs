using ACE.Common.Extensions;
using ACE.Entity.Enum;
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

                    // Check if fellowship is locked
                    if (fellowship.IsLocked)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {targetPlayer.Name}'s fellowship is locked and not accepting new members.", ChatMessageType.Broadcast));
                        return;
                    }

                    // Check if fellowship is full
                    if (fellowship.FellowshipMembers.Count >= ACE.Server.Entity.Fellowship.MaxFellows)
                    {
                        // CONQUEST: Add to waiting queue instead of rejecting
                        var position = fellowship.AddToWaitingQueue(session.Player);
                        var queueCount = fellowship.GetWaitingQueueCount();
                        session.Network.EnqueueSend(new GameMessageSystemChat(
                            $"[FSHIP]: {targetPlayer.Name}'s fellowship is full ({fellowship.FellowshipMembers.Count}/{ACE.Server.Entity.Fellowship.MaxFellows}). You are #{position} in queue. You will be notified when a spot opens.",
                            ChatMessageType.Broadcast));
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

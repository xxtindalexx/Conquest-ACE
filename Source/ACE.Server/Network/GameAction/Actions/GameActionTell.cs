using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using log4net;

namespace ACE.Server.Network.GameAction.Actions
{
    public static class GameActionTell
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [GameAction(GameActionType.Tell)]
        public static void Handle(ClientMessage clientMessage, Session session)
        {
            var message = clientMessage.Payload.ReadString16L(); // The client seems to do the trimming for us
            var target = clientMessage.Payload.ReadString16L(); // Needs to be trimmed because it may contain white spaces after the name and before the ,

            if (session.Player.IsGagged)
            {
                session.Player.SendGagError();
                return;
            }

            target = target.Trim();
            var targetPlayer = PlayerManager.GetOnlinePlayer(target);

            if (targetPlayer == null)
            {
                var statusMessage = new GameEventWeenieError(session, WeenieError.CharacterNotAvailable);
                session.Network.EnqueueSend(statusMessage);
                return;
            }

            if (session.Player != targetPlayer)
                session.Network.EnqueueSend(new GameMessageSystemChat($"You tell {targetPlayer.Name}, \"{message}\"", ChatMessageType.OutgoingTell));

            if (targetPlayer.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.Tell))
            {
                session.Network.EnqueueSend(new GameEventWeenieErrorWithString(session, WeenieErrorWithString.MessageBlocked_,$"{target} has you squelched."));
                //log.Warn($"Tell from {session.Player.Name} (0x{session.Player.Guid.ToString()}) to {targetPlayer.Name} (0x{targetPlayer.Guid.ToString()}) blocked due to squelch");
                return;
            }

            if (targetPlayer.IsAfk)
            {
                session.Network.EnqueueSend(new GameEventWeenieErrorWithString(session, WeenieErrorWithString.AFK, $"{targetPlayer.Name} is away: " + (string.IsNullOrWhiteSpace(targetPlayer.AfkMessage) ? WorldObjects.Player.DefaultAFKMessage : targetPlayer.AfkMessage)));
                //return;
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
                    session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: {targetPlayer.Name}'s fellowship is full ({fellowship.FellowshipMembers.Count}/{ACE.Server.Entity.Fellowship.MaxFellows}).", ChatMessageType.Broadcast));
                    return;
                }

                session.Network.EnqueueSend(new GameMessageSystemChat($"[FSHIP]: Attempting to add you to {targetPlayer.Name}'s fellowship.", ChatMessageType.Broadcast));
                targetPlayer.FellowshipRecruit(session.Player);
                return;
            }

            var tell = new GameEventTell(targetPlayer.Session, message, session.Player.GetNameWithSuffix(), session.Player.Guid.Full, targetPlayer.Guid.Full, ChatMessageType.Tell);
            targetPlayer.Session.Network.EnqueueSend(tell);
        }
    }
}

using System.Linq;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.Command.Handlers
{
    public static class AdminCommands_FellowshipRoll
    {
        /// <summary>
        /// Manages fellowship roll drops for creatures
        /// </summary>
        [CommandHandler("fellowshiprollmob", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Manages fellowship roll item drops for creatures",
            "/fellowshiprollmob add <mobWcid> <petWcid> <probability>\n" +
            "/fellowshiprollmob remove <mobWcid> <petWcid>\n" +
            "/fellowshiprollmob list <mobWcid>\n" +
            "/fellowshiprollmob clear <mobWcid>\n\n" +
            "Examples:\n" +
            "/fellowshiprollmob add 12345 5084 0.15  (15% chance to drop pet 5084 from mob 12345)\n" +
            "/fellowshiprollmob list 12345  (shows all fellowship roll drops for mob 12345)")]
        public static void HandleFellowshipRollMob(Session session, params string[] parameters)
        {
            if (parameters.Length < 2)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /fellowshiprollmob <add|remove|list|clear> <mobWcid> [petWcid] [probability]", ChatMessageType.Broadcast));
                return;
            }

            var action = parameters[0].ToLower();

            if (!uint.TryParse(parameters[1], out var mobWcid))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid mob WCID.", ChatMessageType.Broadcast));
                return;
            }

            switch (action)
            {
                case "add":
                    HandleAdd(session, mobWcid, parameters);
                    break;
                case "remove":
                    HandleRemove(session, mobWcid, parameters);
                    break;
                case "list":
                    HandleList(session, mobWcid);
                    break;
                case "clear":
                    HandleClear(session, mobWcid);
                    break;
                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown action: {action}. Use add, remove, list, or clear.", ChatMessageType.Broadcast));
                    break;
            }
        }

        [CommandHandler("frm", AccessLevel.Admin, CommandHandlerFlag.None, 0,
            "Shorthand for fellowshiprollmob",
            "/frm <add|remove|list|clear> <mobWcid> [petWcid] [probability]")]
        public static void HandleFRM(Session session, params string[] parameters)
        {
            HandleFellowshipRollMob(session, parameters);
        }

        private static void HandleAdd(Session session, uint mobWcid, string[] parameters)
        {
            if (parameters.Length < 4)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /frm add <mobWcid> <petWcid> <probability>", ChatMessageType.Broadcast));
                return;
            }

            if (!uint.TryParse(parameters[2], out var petWcid))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid pet WCID.", ChatMessageType.Broadcast));
                return;
            }

            if (!float.TryParse(parameters[3], out var probability) || probability < 0 || probability > 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid probability. Must be between 0 and 1 (e.g., 0.15 for 15%).", ChatMessageType.Broadcast));
                return;
            }

            // Verify the weenies exist
            var mobWeenie = DatabaseManager.World.GetCachedWeenie(mobWcid);
            if (mobWeenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Mob weenie {mobWcid} not found in database.", ChatMessageType.Broadcast));
                return;
            }

            var petWeenie = DatabaseManager.World.GetCachedWeenie(petWcid);
            if (petWeenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Pet weenie {petWcid} not found in database.", ChatMessageType.Broadcast));
                return;
            }

            // Add the emote to the world database
            var success = DatabaseManager.World.AddFellowshipRollDrop(mobWcid, petWcid, probability);

            if (success)
            {
                // Clear the weenie cache so it reloads with the new emote
                DatabaseManager.World.ClearCachedWeenie(mobWcid);

                var mobName = mobWeenie.GetName() ?? $"Mob {mobWcid}";
                var petName = petWeenie.GetName() ?? $"Pet {petWcid}";

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Added fellowship roll drop: {petName} ({petWcid}) from {mobName} ({mobWcid}) with {probability * 100:F1}% chance.",
                    ChatMessageType.Broadcast));

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    "Note: Existing spawned creatures will not be affected. New spawns will have the drop.",
                    ChatMessageType.Broadcast));
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    "Failed to add fellowship roll drop. Check server logs for details.",
                    ChatMessageType.Broadcast));
            }
        }

        private static void HandleRemove(Session session, uint mobWcid, string[] parameters)
        {
            if (parameters.Length < 3)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /frm remove <mobWcid> <petWcid>", ChatMessageType.Broadcast));
                return;
            }

            if (!uint.TryParse(parameters[2], out var petWcid))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid pet WCID.", ChatMessageType.Broadcast));
                return;
            }

            var success = DatabaseManager.World.RemoveFellowshipRollDrop(mobWcid, petWcid);

            if (success)
            {
                DatabaseManager.World.ClearCachedWeenie(mobWcid);

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Removed fellowship roll drop: Pet {petWcid} from Mob {mobWcid}.",
                    ChatMessageType.Broadcast));
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    "Failed to remove fellowship roll drop. It may not exist.",
                    ChatMessageType.Broadcast));
            }
        }

        private static void HandleList(Session session, uint mobWcid)
        {
            var drops = DatabaseManager.World.GetFellowshipRollDrops(mobWcid);

            if (drops == null || drops.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Mob {mobWcid} has no fellowship roll drops configured.",
                    ChatMessageType.Broadcast));
                return;
            }

            var mobWeenie = DatabaseManager.World.GetCachedWeenie(mobWcid);
            var mobName = mobWeenie?.GetName() ?? $"Mob {mobWcid}";

            session.Network.EnqueueSend(new GameMessageSystemChat(
                $"Fellowship roll drops for {mobName} ({mobWcid}):",
                ChatMessageType.Broadcast));

            foreach (var drop in drops)
            {
                var petWeenie = DatabaseManager.World.GetCachedWeenie(drop.PetWcid);
                var petName = petWeenie?.GetName() ?? $"Unknown";

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"  - {petName} ({drop.PetWcid}): {drop.Probability * 100:F1}% chance",
                    ChatMessageType.Broadcast));
            }
        }

        private static void HandleClear(Session session, uint mobWcid)
        {
            var count = DatabaseManager.World.ClearFellowshipRollDrops(mobWcid);

            if (count > 0)
            {
                DatabaseManager.World.ClearCachedWeenie(mobWcid);

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Removed {count} fellowship roll drop(s) from mob {mobWcid}.",
                    ChatMessageType.Broadcast));
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Mob {mobWcid} has no fellowship roll drops to clear.",
                    ChatMessageType.Broadcast));
            }
        }
    }

    // Helper extension for getting weenie name
    public static class WeenieExtensions_FellowshipRoll
    {
        public static string GetName(this ACE.Entity.Models.Weenie weenie)
        {
            if (weenie?.PropertiesString != null &&
                weenie.PropertiesString.TryGetValue(ACE.Entity.Enum.Properties.PropertyString.Name, out var name))
            {
                return name;
            }
            return null;
        }
    }
}

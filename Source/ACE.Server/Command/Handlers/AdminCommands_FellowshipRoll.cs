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
            "/fellowshiprollmob add <mobWcid> <itemWcid> <rarity> <probability>\n" +
            "/fellowshiprollmob remove <mobWcid> <itemWcid>\n" +
            "/fellowshiprollmob list\n" +
            "/fellowshiprollmob clear <mobWcid>\n\n" +
            "Rarity: 1=Common, 2=Rare, 3=Legendary, 4=Mythic, 0=Direct\n" +
            "For direct (0): itemWcid = pet weenie. For egg rarities (1-4): itemWcid = egg weenie.\n\n" +
            "Examples:\n" +
            "/fellowshiprollmob add 12345 2123456 1 0.15  (15% common egg WCID 2123456)\n" +
            "/fellowshiprollmob add 12345 2123458 3 0.01  (1% legendary egg WCID 2123458)\n" +
            "/fellowshiprollmob add 12345 2123459 4 0.001  (0.1% mythic egg WCID 2123459)\n" +
            "/fellowshiprollmob add 12345 5099 0 0.05  (5% direct drop of pet 5099, no egg)\n" +
            "/fellowshiprollmob list  (shows all fellowship roll drops)")]
        public static void HandleFellowshipRollMob(Session session, params string[] parameters)
        {
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /fellowshiprollmob <add|remove|list|clear> <mobWcid> [petWcid] [probability]", ChatMessageType.Broadcast));
                return;
            }

            var action = parameters[0].ToLower();

            switch (action)
            {
                case "add":
                    if (parameters.Length < 5)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /frm add <mobWcid> <itemWcid> <rarity> <probability>", ChatMessageType.Broadcast));
                        session.Network.EnqueueSend(new GameMessageSystemChat("Rarity: 0=Direct, 1=Common, 2=Rare, 3=Legendary, 4=Mythic", ChatMessageType.Broadcast));
                        session.Network.EnqueueSend(new GameMessageSystemChat("For direct (0): itemWcid = pet. For egg rarities (1-4): itemWcid = egg.", ChatMessageType.Broadcast));
                        return;
                    }
                    if (!uint.TryParse(parameters[1], out var addMobWcid))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Invalid mob WCID.", ChatMessageType.Broadcast));
                        return;
                    }
                    HandleAdd(session, addMobWcid, parameters);
                    break;
                case "remove":
                    if (parameters.Length < 3)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /frm remove <mobWcid> <itemWcid>", ChatMessageType.Broadcast));
                        return;
                    }
                    if (!uint.TryParse(parameters[1], out var removeMobWcid))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Invalid mob WCID.", ChatMessageType.Broadcast));
                        return;
                    }
                    HandleRemove(session, removeMobWcid, parameters);
                    break;
                case "list":
                    HandleListAll(session);
                    break;
                case "clear":
                    if (parameters.Length < 2)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /frm clear <mobWcid>", ChatMessageType.Broadcast));
                        return;
                    }
                    if (!uint.TryParse(parameters[1], out var clearMobWcid))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Invalid mob WCID.", ChatMessageType.Broadcast));
                        return;
                    }
                    HandleClear(session, clearMobWcid);
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

        private static string RarityNumberToString(int rarity)
        {
            return rarity switch
            {
                0 => "direct",
                1 => "common",
                2 => "rare",
                3 => "legendary",
                4 => "mythic",
                _ => null
            };
        }

        private static void HandleAdd(Session session, uint mobWcid, string[] parameters)
        {
            if (parameters.Length < 5)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /frm add <mobWcid> <itemWcid> <rarity> <probability>", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat("Rarity: 0=Direct, 1=Common, 2=Rare, 3=Legendary, 4=Mythic", ChatMessageType.Broadcast));
                session.Network.EnqueueSend(new GameMessageSystemChat("For direct (0): itemWcid = pet. For egg rarities (1-4): itemWcid = egg.", ChatMessageType.Broadcast));
                return;
            }

            if (!uint.TryParse(parameters[2], out var itemWcid))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid item WCID (pet or egg).", ChatMessageType.Broadcast));
                return;
            }

            if (!int.TryParse(parameters[3], out var rarityNum) || rarityNum < 0 || rarityNum > 4)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid rarity. Must be: 0=Direct, 1=Common, 2=Rare, 3=Legendary, 4=Mythic", ChatMessageType.Broadcast));
                return;
            }

            var rarity = RarityNumberToString(rarityNum);

            if (!float.TryParse(parameters[4], out var probability) || probability < 0 || probability > 1)
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

            var itemWeenie = DatabaseManager.World.GetCachedWeenie(itemWcid);
            if (itemWeenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Item weenie {itemWcid} not found in database.", ChatMessageType.Broadcast));
                return;
            }

            // Add the emote to the world database
            var success = DatabaseManager.World.AddFellowshipRollDrop(mobWcid, itemWcid, rarity, probability);

            if (success)
            {
                // Clear the weenie cache so it reloads with the new emote
                DatabaseManager.World.ClearCachedWeenie(mobWcid);

                var mobName = mobWeenie.GetName() ?? $"Mob {mobWcid}";
                var itemName = itemWeenie.GetName() ?? $"Item {itemWcid}";
                var itemType = (rarity == "direct") ? "pet" : "egg";

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Added fellowship roll drop: {itemName} ({itemWcid}) {itemType} from {mobName} ({mobWcid}) - Rarity: {rarity}, Chance: {probability * 100:F1}%",
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
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /frm remove <mobWcid> <itemWcid>", ChatMessageType.Broadcast));
                return;
            }

            if (!uint.TryParse(parameters[2], out var itemWcid))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Invalid item WCID.", ChatMessageType.Broadcast));
                return;
            }

            var success = DatabaseManager.World.RemoveFellowshipRollDrop(mobWcid, itemWcid);

            if (success)
            {
                DatabaseManager.World.ClearCachedWeenie(mobWcid);

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Removed fellowship roll drop: Item {itemWcid} from Mob {mobWcid}.",
                    ChatMessageType.Broadcast));
            }
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    "Failed to remove fellowship roll drop. It may not exist.",
                    ChatMessageType.Broadcast));
            }
        }

        private static void HandleListAll(Session session)
        {
            var allDrops = DatabaseManager.World.GetAllFellowshipRollDrops();

            if (allDrops == null || allDrops.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat(
                    "No fellowship roll drops configured.",
                    ChatMessageType.Broadcast));
                return;
            }

            // Group by mob WCID
            var groupedByMob = allDrops.GroupBy(d => d.MobWcid).OrderBy(g => g.Key);

            foreach (var mobGroup in groupedByMob)
            {
                var mobWcid = mobGroup.Key;
                var mobWeenie = DatabaseManager.World.GetCachedWeenie(mobWcid);
                var mobName = mobWeenie?.GetName() ?? $"Unknown Mob";

                session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"{mobName} ({mobWcid}):",
                    ChatMessageType.Broadcast));

                foreach (var drop in mobGroup)
                {
                    var itemWeenie = DatabaseManager.World.GetCachedWeenie(drop.PetWcid);
                    var itemName = itemWeenie?.GetName() ?? $"Unknown";
                    var itemType = (drop.Rarity == "direct") ? "[Pet]" : "[Egg]";

                    session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"  - {itemName} ({drop.PetWcid}) {itemType}: {drop.Rarity} - {drop.Probability * 100:F1}% chance",
                        ChatMessageType.Broadcast));
                }
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

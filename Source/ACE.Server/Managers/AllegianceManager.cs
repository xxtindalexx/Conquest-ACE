using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;
using System;
using System.Collections.Generic;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Allegiance helper methods
    /// </summary>
    public class AllegianceManager
    {
        /// <summary>
        /// A mapping of all loaded Allegiance GUIDs => their Allegiances
        /// </summary>
        public static readonly Dictionary<ObjectGuid, Allegiance> Allegiances = new Dictionary<ObjectGuid, Allegiance>();

        /// <summary>
        /// A mapping of all Players on the server => their AllegianceNodes
        /// </summary>
        public static readonly Dictionary<ObjectGuid, AllegianceNode> Players = new Dictionary<ObjectGuid, AllegianceNode>();

        /// <summary>
        /// Returns the monarch for a player
        /// </summary>
        public static IPlayer GetMonarch(IPlayer player)
        {
            if (player.MonarchId == null)
                return player;

            var monarch = PlayerManager.FindByGuid(player.MonarchId.Value);

            return monarch ?? player;
        }

        /// <summary>
        /// Returns the full allegiance structure for any player
        /// </summary>
        /// <param name="player">A player at any level of an allegiance</param>
        public static Allegiance GetAllegiance(IPlayer player)
        {
            if (player == null) return null;

            var monarch = GetMonarch(player);

            if (monarch == null) return null;

            // is this allegiance already loaded / cached?
            if (Players.ContainsKey(monarch.Guid))
                return Players[monarch.Guid].Allegiance;

            // try to load biota
            var allegianceID = DatabaseManager.Shard.BaseDatabase.GetAllegianceID(monarch.Guid.Full);
            var biota = allegianceID != null ? DatabaseManager.Shard.BaseDatabase.GetBiota(allegianceID.Value) : null;

            Allegiance allegiance;

            if (biota != null)
            {
                var entityBiota = ACE.Database.Adapter.BiotaConverter.ConvertToEntityBiota(biota);

                allegiance = new Allegiance(entityBiota);
            }
            else
                allegiance = new Allegiance(monarch.Guid);

            if (allegiance.TotalMembers == 1)
                return null;

            if (biota == null)
            {
                allegiance = WorldObjectFactory.CreateNewWorldObject("allegiance") as Allegiance;
                allegiance.MonarchId = monarch.Guid.Full;
                allegiance.Init(monarch.Guid);

                allegiance.SaveBiotaToDatabase();
            }

            AddPlayers(allegiance);

            //if (!Allegiances.ContainsKey(allegiance.Guid))
                //Allegiances.Add(allegiance.Guid, allegiance);
            Allegiances[allegiance.Guid] = allegiance;

            return allegiance;
        }

        /// <summary>
        /// Returns the AllegianceNode for a Player
        /// </summary>
        public static AllegianceNode GetAllegianceNode(IPlayer player)
        {
            Players.TryGetValue(player.Guid, out var allegianceNode);
            return allegianceNode;
        }

        /// <summary>
        /// Returns a list of all players under a monarch
        /// </summary>
        public static List<IPlayer> FindAllPlayers(ObjectGuid monarchGuid)
        {
            return PlayerManager.FindAllByMonarch(monarchGuid);
        }

        /// <summary>
        /// Loads the Allegiance and AllegianceNode for a Player
        /// </summary>
        public static void LoadPlayer(IPlayer player)
        {
            if (player == null) return;

            player.Allegiance = GetAllegiance(player);
            player.AllegianceNode = GetAllegianceNode(player);

            // TODO: update chat channels for online players here?
        }

        /// <summary>
        /// Called when a player joins/exits an Allegiance
        /// </summary>
        public static void Rebuild(Allegiance allegiance)
        {
            if (allegiance == null) return;

            RemoveCache(allegiance);

            // rebuild allegiance
            allegiance = GetAllegiance(allegiance.Monarch.Player);

            // relink players
            foreach (var member in allegiance.Members.Keys)
            {
                var player = PlayerManager.FindByGuid(member);
                if (player == null) continue;

                LoadPlayer(player);
            }

            // update dynamic properties
            allegiance.UpdateProperties();
        }

        /// <summary>
        /// Appends the Players lookup table with the members of an Allegiance
        /// </summary>
        public static void AddPlayers(Allegiance allegiance)
        {
            foreach (var member in allegiance.Members)
            {
                var player = member.Key;
                var allegianceNode = member.Value;

                if (!Players.ContainsKey(player))
                    Players.Add(player, allegianceNode);
                else
                    Players[player] = allegianceNode;
            }
        }

        /// <summary>
        /// Removes an Allegiance from the Players lookup table cache
        /// </summary>
        public static void RemoveCache(Allegiance allegiance)
        {
            foreach (var member in allegiance.Members)
                Players.Remove(member.Key);
        }

        /// <summary>
        /// The maximum amount of leadership / loyalty
        /// </summary>
        public static float SkillCap = 291.0f;

        /// <summary>
        /// The maximum amount of realtime hours sworn to patron
        /// </summary>
        public static float RealCap = 730.0f;

        /// <summary>
        /// The maximum amount of in-game hours sworn to patron
        /// </summary>
        public static float GameCap = 720.0f;

        // This function can be called from multi-threaded operations
        // We must add thread safety to prevent AllegianceManager corruption
        // We must also protect against cross-thread operations on vassal/patron (non-concurrent collections)
        public static void PassXP(AllegianceNode vassalNode, ulong amount, bool direct, bool luminance = false)
        {
            WorldManager.EnqueueAction(new ActionEventDelegate(ActionType.AllegianceManager_DoPassXP, () => DoPassXP(vassalNode, amount, direct, luminance)));
        }

        private static void DoPassXP(AllegianceNode vassalNode, ulong amount, bool direct, bool luminance = false, int depth = 1)
        {
            // CONQUEST: Simplified XP/Luminance passup system
            // - Direct patron (level 1): receives 25% of earned XP/Lum
            // - Each subsequent level (2-3): receives 25% of what was passed to previous level (75% reduction)
            // - Levels 4+: receives 1% of what was passed to previous level

            var patronNode = vassalNode.Patron;
            if (patronNode == null)
                return;

            var vassal = vassalNode.Player;
            var patron = patronNode.Player;

            if (!vassal.ExistedBeforeAllegianceXpChanges)
                return;

            if (patron.GetProperty(PropertyBool.IsMule).HasValue && patron.GetProperty(PropertyBool.IsMule).Value == true)
            {
                return;
            }
            if (vassal.GetProperty(PropertyBool.IsMule).HasValue && vassal.GetProperty(PropertyBool.IsMule).Value == true)
            {
                return;
            }

            // Calculate passup percentage based on depth
            double passupPercentage;
            if (depth <= 3)
            {
                // Levels 1-3: 25% passup
                passupPercentage = 0.25;
            }
            else
            {
                // Levels 4+: 1% passup
                passupPercentage = 0.01;
            }

            var generatedAmount = (uint)(amount * passupPercentage);
            var passupAmount = generatedAmount;

            // DEBUG: Log passup calculation
            //Console.WriteLine($"[PASSUP] Depth: {depth}, Vassal: {vassal.Name}, Patron: {patron.Name}, Amount In: {amount}, Percentage: {passupPercentage}, Amount Out: {passupAmount}, Luminance: {luminance}");

            if (luminance)
            {
                // Apply luminance multiplier if configured
                var lumMult = PropertyManager.GetDouble("lum_passup_mult", 1.0); // Default to 1.0 (no reduction) for new system
                generatedAmount = (uint)(generatedAmount * lumMult);
                passupAmount = (uint)(passupAmount * lumMult);
            }

            if (passupAmount > 0 && luminance == true)
            {
                vassal.AllegianceLumGenerated += generatedAmount;

                patron.AllegianceLumCached += passupAmount;
                var onlinePatron = PlayerManager.GetOnlinePlayer(patron.Guid);
                if (onlinePatron != null)
                {
                    onlinePatron.AddAllegianceLum();
                }
                // call recursively with incremented depth
                DoPassXP(patronNode, passupAmount, false, luminance, depth + 1);
            }

            if (passupAmount > 0 && luminance == false)
            {
                vassal.AllegianceXPGenerated += generatedAmount;

                if (PropertyManager.GetBool("offline_xp_passup_limit"))
                    patron.AllegianceXPCached = Math.Min(patron.AllegianceXPCached + passupAmount, uint.MaxValue);
                else
                    patron.AllegianceXPCached += passupAmount;

                var onlinePatron = PlayerManager.GetOnlinePlayer(patron.Guid);
                if (onlinePatron != null)
                    onlinePatron.AddAllegianceXP();

                // call recursively with incremented depth
                DoPassXP(patronNode, passupAmount, false, luminance, depth + 1);
            }
        }

        /// <summary>
        /// Updates the Allegiance tree structure when a new player joins
        /// </summary>
        /// <param name="vassal">The vassal swearing into the Allegiance</param>
        public static void OnSwearAllegiance(Player vassal)
        {
            if (vassal == null) return;

            // was this vassal previously a Monarch?
            if (vassal.Allegiance != null)
                RemoveCache(vassal.Allegiance);

            // rebuild the new combined structure
            var allegiance = GetAllegiance(vassal);
            Rebuild(allegiance);

            LoadPlayer(vassal);

            // maintain approved vassals list
            if (allegiance != null && allegiance.HasApprovedVassal(vassal.Guid.Full))
                allegiance.RemoveApprovedVassal(vassal.Guid.Full);
        }

        /// <summary>
        /// Updates the Allegiance tree structure when a member leaves
        /// </summary>
        /// <param name="self">The player initiating the break request</param>
        /// <param name="target">The patron or vassal of the self player</param>
        public static void OnBreakAllegiance(IPlayer self, IPlayer target)
        {
            // remove the previous allegiance structure
            if (self != null)   // ??
                RemoveCache(self.Allegiance);

            // rebuild for self and target
            var selfAllegiance = GetAllegiance(self);
            var targetAllegiance = GetAllegiance(target);

            Rebuild(selfAllegiance);
            Rebuild(targetAllegiance);

            LoadPlayer(self);
            LoadPlayer(target);

            HandleNoAllegiance(self);
            HandleNoAllegiance(target);
        }

        public static void HandleNoAllegiance(IPlayer player)
        {
            if (player == null || player.Allegiance != null)
                return;

            var onlinePlayer = PlayerManager.GetOnlinePlayer(player.Guid);

            var updated = false;

            if (player.MonarchId != null)
            {
                player.UpdateProperty(PropertyInstanceId.Monarch, null, true);

                updated = true;
            }

            if (player.AllegianceRank != null)
            {
                player.AllegianceRank = null;

                if (onlinePlayer != null)
                    onlinePlayer.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(onlinePlayer, PropertyInt.AllegianceRank, 0));

                updated = true;
            }

            if (updated)
                player.SaveBiotaToDatabase();

            if (onlinePlayer != null)
                onlinePlayer.Session.Network.EnqueueSend(new GameEventAllegianceUpdate(onlinePlayer.Session, onlinePlayer.Allegiance, onlinePlayer.AllegianceNode), new GameEventAllegianceAllegianceUpdateDone(onlinePlayer.Session));
        }

        public static Allegiance FindAllegiance(uint allegianceID)
        {
            Allegiances.TryGetValue(new ObjectGuid(allegianceID), out var allegiance);
            return allegiance;
        }

        // This function is called from a database callback.
        // We must add thread safety to prevent AllegianceManager corruption
        public static void HandlePlayerDelete(uint playerGuid)
        {
            WorldManager.EnqueueAction(new ActionEventDelegate(ActionType.AllegianceManager_DoHandlePlayerDelete, () => DoHandlePlayerDelete(playerGuid)));
        }

        private static void DoHandlePlayerDelete(uint playerGuid)
        {
            var player = PlayerManager.FindByGuid(playerGuid);
            if (player == null)
            {
                Console.WriteLine($"AllegianceManager.HandlePlayerDelete({playerGuid:X8}): couldn't find player guid");
                return;
            }
            var allegiance = GetAllegiance(player);

            if (allegiance == null) return;

            allegiance.Members.TryGetValue(player.Guid, out var allegianceNode);

            var players = new List<IPlayer>() { player };

            if (player.PatronId != null)
            {
                var patron = PlayerManager.FindByGuid(player.PatronId.Value);

                if (patron != null)
                    players.Add(patron);
            }

            player.PatronId = null;
            player.UpdateProperty(PropertyInstanceId.Monarch, null, true);

            // vassals now become monarchs...
            foreach (var vassalNode in allegianceNode.Vassals.Values)
            {
                var vassal = PlayerManager.FindByGuid(vassalNode.PlayerGuid);

                if (vassal == null) continue;

                vassal.PatronId = null;
                vassal.UpdateProperty(PropertyInstanceId.Monarch, null, true);

                // walk the allegiance tree from this node, update monarch ids
                vassalNode.Walk((node) =>
                {
                    node.Player.UpdateProperty(PropertyInstanceId.Monarch, vassalNode.PlayerGuid.Full, true);

                    node.Player.SaveBiotaToDatabase();

                }, false);

                players.Add(vassal);
            }

            RemoveCache(allegiance);

            // rebuild for those directly involved
            foreach (var p in players)
                Rebuild(GetAllegiance(p));

            foreach (var p in players)
                LoadPlayer(p);

            foreach (var p in players)
                HandleNoAllegiance(p);

            // save immediately?
            foreach (var p in players)
                p.SaveBiotaToDatabase();

            foreach (var p in players)
            {
                Player.CheckAllegianceHouse(p.Guid);

                var newAllegiance = GetAllegiance(p);
                if (newAllegiance != null)
                    newAllegiance.Monarch.Walk((node) => Player.CheckAllegianceHouse(node.PlayerGuid), false);
            }
        }
    }
}

using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Database.Models.World;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Managers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using static ACE.Server.Entity.Confirmation_Custom;
using ACE.Server.Network;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Manages active fellowship rolls for items
    /// </summary>
    public class FellowshipRollManager
    {
        private static readonly ConcurrentDictionary<string, FellowshipRollInstance> ActiveRolls = new ConcurrentDictionary<string, FellowshipRollInstance>();

        public static FellowshipRollInstance GetRoll(string rollId)
        {
            ActiveRolls.TryGetValue(rollId, out var roll);
            return roll;
        }

        public static string InitiateRoll(Creature creature, uint itemWcid, Player killer, Fellowship fellowship)
        {
            // Generate unique roll ID
            var rollId = Guid.NewGuid().ToString();

            // Get all fellowship members within range who contributed damage
            var eligibleMembers = GetEligibleMembers(creature, killer, fellowship);

            if (eligibleMembers.Count == 0)
            {
                // No eligible members, give to killer
                AwardItemToPlayer(killer, itemWcid);
                return null;
            }

            // Create the roll instance
            var rollInstance = new FellowshipRollInstance
            {
                RollId = rollId,
                ItemWcid = itemWcid,
                CreatureName = creature.Name,
                EligiblePlayers = eligibleMembers,
                Responses = new ConcurrentDictionary<uint, RollResponse>(),
                StartTime = DateTime.UtcNow
            };

            // Add to active rolls
            ActiveRolls.TryAdd(rollId, rollInstance);

            // Send confirmation dialogs to all eligible members
            SendRollConfirmations(rollInstance);

            // Schedule timeout check
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(30.0);
            actionChain.AddAction(WorldManager.DelayManager, ActionType.FellowshipRollManager_ProcessTimeout, () => ProcessRollTimeout(rollId));
            actionChain.EnqueueChain();

            return rollId;
        }

        private static List<Player> GetEligibleMembers(Creature creature, Player killer, Fellowship fellowship)
        {
            var eligiblePlayers = new List<Player>();

            // Get fellowship members within range
            var fellowsInRange = fellowship.WithinRange(killer);

            foreach (var fellow in fellowsInRange)
            {
                // Check if player contributed damage
                if (creature.DamageHistory.TotalDamage.TryGetValue(fellow.Guid, out var damageInfo))
                {
                    if (damageInfo.TotalDamage > 0)
                    {
                        eligiblePlayers.Add(fellow);
                    }
                }
            }

            return eligiblePlayers;
        }

        private static void SendRollConfirmations(FellowshipRollInstance rollInstance)
        {
            var itemName = GetItemName(rollInstance.ItemWcid);

            foreach (var player in rollInstance.EligiblePlayers)
            {
                var confirmation = new Confirmation_FellowshipRoll(player.Guid, rollInstance.RollId, itemName);
                player.ConfirmationManager.EnqueueSend(confirmation, $"Do you want to roll for {itemName}?");
            }

            // Broadcast to fellowship
            var fellowship = rollInstance.EligiblePlayers.FirstOrDefault()?.Fellowship;
            fellowship?.BroadcastToFellow($"A fellowship roll has been initiated for {itemName} from {rollInstance.CreatureName}!");
        }

        private static void ProcessRollTimeout(string rollId)
        {
            if (!ActiveRolls.TryRemove(rollId, out var rollInstance))
                return;

            // Process final results
            ProcessRollResults(rollInstance);
        }

        public static void RegisterResponse(string rollId, Player player, bool wantsToRoll, bool timeout)
        {
            var rollInstance = GetRoll(rollId);
            if (rollInstance == null) return;

            var response = new RollResponse
            {
                Player = player,
                WantsToRoll = !timeout && wantsToRoll,
                RollValue = 0,
                Timestamp = DateTime.UtcNow
            };

            // If player wants to roll, generate random 1-100
            if (response.WantsToRoll)
            {
                response.RollValue = ThreadSafeRandom.Next(1, 100);
            }

            rollInstance.Responses.TryAdd(player.Guid.Full, response);

            // Broadcast the response
            var fellowship = player.Fellowship;
            if (fellowship != null)
            {
                if (response.WantsToRoll)
                {
                    fellowship.BroadcastToFellow($"{player.Name} rolled {response.RollValue} for {GetItemName(rollInstance.ItemWcid)}");
                }
                else
                {
                    fellowship.BroadcastToFellow($"{player.Name} passed on {GetItemName(rollInstance.ItemWcid)}");
                }
            }

            // Check if all players have responded
            if (rollInstance.Responses.Count >= rollInstance.EligiblePlayers.Count)
            {
                // All responded, process immediately
                if (ActiveRolls.TryRemove(rollId, out var instance))
                {
                    ProcessRollResults(instance);
                }
            }
        }

        private static void ProcessRollResults(FellowshipRollInstance rollInstance)
        {
            // Get all players who rolled (not passed)
            var rollers = rollInstance.Responses.Values
                .Where(r => r.WantsToRoll && r.RollValue > 0)
                .OrderByDescending(r => r.RollValue)
                .ToList();

            var itemName = GetItemName(rollInstance.ItemWcid);
            var fellowship = rollInstance.EligiblePlayers.FirstOrDefault()?.Fellowship;

            if (rollers.Count == 0)
            {
                // Everyone passed, try to give to first eligible player
                var fallbackWinner = rollInstance.EligiblePlayers.FirstOrDefault();

                if (fallbackWinner != null)
                {
                    fellowship?.BroadcastToFellow($"All players passed on {itemName}. Attempting to award to {fallbackWinner.Name} by default.");
                    

                    if (!AwardItemToPlayer(fallbackWinner, rollInstance.ItemWcid))
                    {
                        fellowship?.BroadcastToFellow($"{fallbackWinner.Name}'s inventory was full. {itemName} was not awarded.");
                    }
                }
            }
            else
            {
                // Try to award to rollers in order from highest to lowest
                bool awarded = false;

                foreach (var roller in rollers)
                {
                    var player = roller.Player;

                    if (!awarded)
                    {
                        // This is the current highest roller who hasn't been tried yet
                        fellowship?.BroadcastToFellow($"{player.Name} won {itemName} with a roll of {roller.RollValue}!");

                        if (AwardItemToPlayer(player, rollInstance.ItemWcid))
                        {
                            // Successfully awarded
                            awarded = true;
                        }
                        else
                        {
                            // Inventory was full, notify and try next
                            fellowship?.BroadcastToFellow($"{player.Name}'s inventory was full. Passing to next highest roller...");
                        }
                    }
                }

                if (!awarded)
                {
                    // No one could receive the item
                    fellowship?.BroadcastToFellow($"No one had inventory space for {itemName}. Item was not awarded.");
                }
            }
        }

        private static bool AwardItemToPlayer(Player player, uint itemWcid)
        {
            var wo = WorldObjectFactory.CreateNewWorldObject(itemWcid);
            if (wo == null)
            {
                player.SendMessage($"Error creating item {itemWcid}. Please contact an administrator.");
                return false;
            }

            // Try to add to inventory
            if (player.TryCreateInInventoryWithNetworking(wo))
            {
                player.SendMessage($"You received {wo.Name}!");
                return true;
            }
            else
            {
                // Inventory full - item will pass to next highest roller
                player.SendMessage($"You won {wo.Name} but your inventory was full. Item passed to next highest roller.");

                // Destroy the created object since we couldn't give it to the player
                wo.Destroy();
                return false;
            }
        }

        private static string GetItemName(uint wcid)
        {
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie?.PropertiesString != null && weenie.PropertiesString.TryGetValue(PropertyString.Name, out var name))
                return name;
                        return $"Item({wcid})";
        }

        public static void Shutdown()
        {
            ActiveRolls.Clear();
        }
    }

    public class FellowshipRollInstance
    {
        public string RollId { get; set; }
        public uint ItemWcid { get; set; }
        public string CreatureName { get; set; }
        public List<Player> EligiblePlayers { get; set; }
        public ConcurrentDictionary<uint, RollResponse> Responses { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class RollResponse
    {
        public Player Player { get; set; }
        public bool WantsToRoll { get; set; }
        public int RollValue { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

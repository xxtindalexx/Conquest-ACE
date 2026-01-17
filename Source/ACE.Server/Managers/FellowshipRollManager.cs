using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Managers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using static ACE.Server.Entity.Confirmation_Custom;

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
            // ConfirmationManager handles 30-second timeouts internally for each player
            SendRollConfirmations(rollInstance);

            return rollId;
        }

        private static List<Player> GetEligibleMembers(Creature creature, Player killer, Fellowship fellowship)
        {
            var eligiblePlayers = new List<Player>();

            // Get fellowship members within range (includeSelf: true to include the killer)
            var fellowsInRange = fellowship.WithinRange(killer, includeSelf: true);

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
                // Send confirmation directly - ConfirmationManager handles timeout internally
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

                    if (AwardItemToPlayer(fallbackWinner, rollInstance.ItemWcid))
                    {
                        // Successfully awarded - broadcast globally
                        PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{fallbackWinner.Name} has obtained {itemName}!", ChatMessageType.Broadcast));
                    }
                    else
                    {
                        fellowship?.BroadcastToFellow($"{fallbackWinner.Name}'s inventory was full. {itemName} was not awarded.");
                    }
                }
            }
            else
            {
                // Get highest roll value
                var highestRoll = rollers.First().RollValue;

                // Find all players with the highest roll (to handle ties)
                var topRollers = rollers.Where(r => r.RollValue == highestRoll).ToList();

                // If there's a tie, randomly select winner
                bool wasTie = topRollers.Count > 1;
                RollResponse winner;

                if (wasTie)
                {
                    var tiedNames = string.Join(", ", topRollers.Select(r => r.Player.Name));
                    fellowship?.BroadcastToFellow($"Tie detected! {tiedNames} all rolled {highestRoll}. Randomly selecting winner...");

                    // Randomly select from tied players
                    var randomIndex = ThreadSafeRandom.Next(0, topRollers.Count - 1);
                    winner = topRollers[randomIndex];

                    fellowship?.BroadcastToFellow($"{winner.Player.Name} won the tiebreaker!");
                }
                else
                {
                    winner = topRollers.First();
                }

                // Award to winner
                bool awarded = false;

                // Try winner first
                fellowship?.BroadcastToFellow($"{winner.Player.Name} won {itemName} with a roll of {winner.RollValue}!");

                if (AwardItemToPlayer(winner.Player, rollInstance.ItemWcid))
                {
                    // Successfully awarded - broadcast globally
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{winner.Player.Name} has obtained {itemName}!", ChatMessageType.Broadcast));
                    awarded = true;
                }
                else
                {
                    // Winner's inventory was full
                    fellowship?.BroadcastToFellow($"{winner.Player.Name}'s inventory was full.");

                    // If there was a tie, try other tied players first
                    if (wasTie)
                    {
                        var otherTiedPlayers = topRollers.Where(r => r != winner).ToList();

                        foreach (var tiedPlayer in otherTiedPlayers)
                        {
                            if (!awarded)
                            {
                                fellowship?.BroadcastToFellow($"Passing to {tiedPlayer.Player.Name} (also rolled {highestRoll})...");

                                if (AwardItemToPlayer(tiedPlayer.Player, rollInstance.ItemWcid))
                                {
                                    // Successfully awarded - broadcast globally
                                    PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{tiedPlayer.Player.Name} has obtained {itemName}!", ChatMessageType.Broadcast));
                                    awarded = true;
                                }
                                else
                                {
                                    fellowship?.BroadcastToFellow($"{tiedPlayer.Player.Name}'s inventory was also full.");
                                }
                            }
                        }

                        // If all tied players had full inventories, notify
                        if (!awarded)
                        {
                            fellowship?.BroadcastToFellow($"All tied players had full inventories. Passing to next highest roller...");
                        }
                    }

                    // Try remaining rollers (lower rolls) if not awarded yet
                    if (!awarded)
                    {
                        var lowerRollers = rollers.Where(r => r.RollValue < highestRoll).ToList();

                        foreach (var roller in lowerRollers)
                        {
                            if (!awarded)
                            {
                                fellowship?.BroadcastToFellow($"Attempting to award to {roller.Player.Name} (rolled {roller.RollValue})...");

                                if (AwardItemToPlayer(roller.Player, rollInstance.ItemWcid))
                                {
                                    // Successfully awarded - broadcast globally
                                    PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{roller.Player.Name} has obtained {itemName}!", ChatMessageType.Broadcast));
                                    awarded = true;
                                }
                                else
                                {
                                    fellowship?.BroadcastToFellow($"{roller.Player.Name}'s inventory was full. Passing to next highest roller...");
                                }
                            }
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

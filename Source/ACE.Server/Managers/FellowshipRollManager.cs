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

        public static string InitiateRoll(Creature creature, uint itemWcid, string rarity, Player killer, Fellowship fellowship)
        {
            // Generate unique roll ID
            var rollId = Guid.NewGuid().ToString();

            // Default to "direct" if rarity is null or empty
            if (string.IsNullOrEmpty(rarity))
                rarity = "direct";

            // Get all fellowship members within range who contributed damage
            // Pass rarity to filter out players at mystery egg weekly limit
            var eligibleMembers = GetEligibleMembers(creature, killer, fellowship, rarity);

            if (eligibleMembers.Count == 0)
            {
                // No eligible members, give to killer
                AwardItemToPlayer(killer, itemWcid, rarity);
                return null;
            }

            // CONQUEST: If only 1 eligible player, skip the roll popup and award directly
            if (eligibleMembers.Count == 1)
            {
                var soloWinner = eligibleMembers.First();
                if (AwardItemToPlayer(soloWinner, itemWcid, rarity))
                {
                    var itemName = GetItemName(itemWcid);
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{soloWinner.Name} has obtained {itemName}!", ChatMessageType.Broadcast));
                }
                return null;
            }

            // Create the roll instance
            var rollInstance = new FellowshipRollInstance
            {
                RollId = rollId,
                ItemWcid = itemWcid,
                Rarity = rarity,
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

        private static List<Player> GetEligibleMembers(Creature creature, Player killer, Fellowship fellowship, string rarity = "direct")
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
                        // CONQUEST: For mystery eggs (non-direct), check weekly limit BEFORE allowing roll
                        if (rarity != "direct")
                        {
                            if (!CheckWeeklyMysteryEggLimit(fellow, out var limitMessage))
                            {
                                fellow.SendMessage(limitMessage);
                                continue; // Skip this player - they're at their weekly limit
                            }
                        }

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

                    if (AwardItemToPlayer(fallbackWinner, rollInstance.ItemWcid, rollInstance.Rarity))
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

                if (AwardItemToPlayer(winner.Player, rollInstance.ItemWcid, rollInstance.Rarity))
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

                                if (AwardItemToPlayer(tiedPlayer.Player, rollInstance.ItemWcid, rollInstance.Rarity))
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

                                if (AwardItemToPlayer(roller.Player, rollInstance.ItemWcid, rollInstance.Rarity))
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

        /// <summary>
        /// Checks if player can obtain another mystery egg
        /// - Admins: No limit (exempt for testing)
        /// - Exempt accounts: 4 per week per IP (double the normal limit)
        /// - Non-exempt accounts: 2 per week per IP (shared across all accounts on that IP)
        /// Also checks player-level properties as a redundant safety measure
        /// </summary>
        private static bool CheckWeeklyMysteryEggLimit(Player player, out string message)
        {
            message = null;

            // CONQUEST: Admins are exempt from weekly limit for testing purposes
            if (player.IsAdmin || player.IsArch || player.IsSentinel)
                return true;

            var currentTime = (long)Time.GetUnixTime();

            // Check if this account is exempt from multibox restrictions
            var isExempt = DatabaseManager.Authentication.IsAccountMultiboxExempt(player.Account.AccountId);

            // Both exempt and non-exempt use IP-based tracking
            // Exempt accounts get double the limit (4 instead of 2)
            var weeklyLimit = isExempt ? 4 : 2;

            // === Player Property Check (redundant safety) ===
            var lastResetTime = player.GetProperty(PropertyInt64.LastMysteryEggWeeklyResetTime) ?? 0;
            var playerEggCount = player.GetProperty(PropertyInt.MysteryEggsObtainedThisWeek) ?? 0;

            // Reset if more than 7 days have passed
            if (currentTime - lastResetTime > 604800) // 604800 seconds = 7 days
            {
                playerEggCount = 0;
                player.SetProperty(PropertyInt.MysteryEggsObtainedThisWeek, 0);
                player.SetProperty(PropertyInt64.LastMysteryEggWeeklyResetTime, currentTime);
            }

            // Check player-level limit (use same limit as IP-based)
            if (playerEggCount >= weeklyLimit)
            {
                var timeSinceReset = currentTime - lastResetTime;
                var secondsRemaining = 604800 - timeSinceReset;
                var days = secondsRemaining / 86400;
                var hours = (secondsRemaining % 86400) / 3600;
                var minutes = (secondsRemaining % 3600) / 60;

                var exemptNote = isExempt ? " (Exempt account: 4/week limit)" : "";
                message = $"You have already obtained {weeklyLimit} Mystery Eggs this week on this character. You can obtain another in {days}d {hours}h {minutes}m.{exemptNote}";
                return false;
            }

            // === IP-based Check ===
            return CheckIpMysteryEggLimit(player, currentTime, weeklyLimit, isExempt, out message);
        }

        /// <summary>
        /// Per-IP mystery egg limit check
        /// Non-exempt accounts: limit of 2 per week
        /// Exempt accounts: limit of 4 per week
        /// </summary>
        private static bool CheckIpMysteryEggLimit(Player player, long currentTime, int weeklyLimit, bool isExempt, out string message)
        {
            message = null;

            // Get player's IP address from session
            var ipAddress = player.Session?.EndPoint?.Address?.ToString();

            if (string.IsNullOrEmpty(ipAddress))
            {
                // Can't determine IP - deny the egg to be safe
                message = "Unable to verify IP address for mystery egg limit tracking.";
                return false;
            }

            // Get or create IP tracking record
            var tracking = DatabaseManager.Shard.GetOrCreateMysteryEggIpTracking(ipAddress);

            // Check if 7 days have passed (the GetOrCreate method handles the reset)
            var timeSinceWeekStart = currentTime - tracking.WeekStartTime;

            // Check if IP has reached weekly limit
            if (tracking.EggsObtained >= weeklyLimit)
            {
                var secondsRemaining = 604800 - timeSinceWeekStart;
                var days = secondsRemaining / 86400;
                var hours = (secondsRemaining % 86400) / 3600;
                var minutes = (secondsRemaining % 3600) / 60;
                var seconds = secondsRemaining % 60;

                var exemptNote = isExempt ? " (Exempt account: 4/week limit)" : "";
                message = $"Your IP has already obtained {weeklyLimit} Mystery Eggs this week. You can obtain another in {days}d {hours}h {minutes}m {seconds}s.{exemptNote}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Increments the weekly mystery egg counter
        /// Both exempt and non-exempt accounts use IP-based tracking
        /// Exempt accounts have a limit of 4, non-exempt have a limit of 2
        /// Also tracks on player properties as a redundant check
        /// </summary>
        private static void IncrementMysteryEggCounter(Player player)
        {
            var currentTime = (long)Time.GetUnixTime();

            // === Player Property Tracking ===
            var lastResetTime = player.GetProperty(PropertyInt64.LastMysteryEggWeeklyResetTime) ?? 0;
            var playerEggCount = player.GetProperty(PropertyInt.MysteryEggsObtainedThisWeek) ?? 0;

            // Reset if more than 7 days have passed
            if (currentTime - lastResetTime > 604800) // 604800 seconds = 7 days
            {
                playerEggCount = 0;
                player.SetProperty(PropertyInt64.LastMysteryEggWeeklyResetTime, currentTime);
            }

            // Increment player-level counter
            playerEggCount++;
            player.SetProperty(PropertyInt.MysteryEggsObtainedThisWeek, playerEggCount);

            // === IP-based Tracking ===
            var ipAddress = player.Session?.EndPoint?.Address?.ToString();
            if (string.IsNullOrEmpty(ipAddress))
                return;

            // Check if this account is exempt from multibox restrictions
            var isExempt = DatabaseManager.Authentication.IsAccountMultiboxExempt(player.Account.AccountId);
            var weeklyLimit = isExempt ? 4 : 2;

            // Increment IP-based counter
            DatabaseManager.Shard.IncrementMysteryEggIpCount(ipAddress);

            // Get the updated count to inform the player
            var tracking = DatabaseManager.Shard.GetMysteryEggIpTracking(ipAddress);
            var newCount = tracking?.EggsObtained ?? 1;
            var remaining = weeklyLimit - newCount;

            var exemptNote = isExempt ? " [Exempt account]" : "";
            if (remaining > 0)
                player.SendMessage($"Mystery Eggs obtained this week (your IP): {newCount}/{weeklyLimit} ({remaining} remaining){exemptNote}");
            else
                player.SendMessage($"Mystery Eggs obtained this week (your IP): {newCount}/{weeklyLimit} (limit reached){exemptNote}");
        }

        private static bool AwardItemToPlayer(Player player, uint itemWcid, string rarity)
        {
            // Check weekly mystery egg limit (only for eggs, not direct pet drops)
            if (rarity != "direct")
            {
                if (!CheckWeeklyMysteryEggLimit(player, out var limitMessage))
                {
                    player.SendMessage(limitMessage);
                    return false; // Skip this player, pass to next roller
                }
            }

            var wo = WorldObjectFactory.CreateNewWorldObject(itemWcid);
            if (wo == null)
            {
                player.SendMessage($"Error creating item {itemWcid}. Please contact an administrator.");
                return false;
            }

            // If this is an egg (not direct), set the EggRarity property
            if (rarity != "direct")
            {
                int rarityValue = rarity.ToLower() switch
                {
                    "common" => 1,
                    "rare" => 2,
                    "legendary" => 3,
                    "mythic" => 4,
                    _ => 1  // Default to common if unknown
                };

                wo.SetProperty(PropertyInt.EggRarity, rarityValue);
            }
            else
            {
                // CONQUEST: For direct pet drops, read PetRarity from weenie and apply ratings/underlay
                var petRarity = wo.GetProperty(PropertyInt.PetRarity);
                if (petRarity != null && petRarity.Value >= 1 && petRarity.Value <= 4)
                {
                    // Set icon underlay based on rarity
                    wo.IconUnderlayId = petRarity.Value switch
                    {
                        1 => 0x06003355, // Common
                        2 => 0x06003353, // Rare
                        3 => 0x06003356, // Legendary
                        4 => 0x06003354, // Mythic
                        _ => null
                    };

                    // Apply random rating bonuses based on rarity
                    AssignRandomPetRatings(wo, petRarity.Value);
                }
            }

            // Try to add to inventory
            if (player.TryCreateInInventoryWithNetworking(wo))
            {
                var itemType = (rarity == "direct") ? "pet" : "mystery egg";
                player.SendMessage($"You received {wo.Name} ({itemType})!");

                // Increment weekly counter for mystery eggs (not for direct drops)
                if (rarity != "direct")
                {
                    IncrementMysteryEggCounter(player);
                }

                return true;
            }
            else
            {
                // Inventory full
                player.SendMessage($"You won {wo.Name} but your inventory was full. Item was not awarded.");

                // Destroy the created object since we couldn't give it to the player
                wo.Destroy();
                return false;
            }
        }

        /// <summary>
        /// CONQUEST: Assigns random rating bonuses to a pet device based on its rarity tier
        /// Common: No bonuses
        /// Rare: +1 to one random rating
        /// Legendary/Mythic: 50% chance for +2 to one rating, 50% chance for +1 to two different ratings
        /// </summary>
        private static void AssignRandomPetRatings(WorldObject pet, int rarity)
        {
            // Common pets get no rating bonuses
            if (rarity == 1)
                return;

            // Available ratings to choose from
            var availableRatings = new List<PropertyInt>
            {
                PropertyInt.PetBonusDamageRating,
                PropertyInt.PetBonusDamageReductionRating,
                PropertyInt.PetBonusCritDamageRating,
                PropertyInt.PetBonusCritDamageReductionRating
            };

            if (rarity == 2) // Rare: +1 to one random rating
            {
                var chosenRating = availableRatings[ThreadSafeRandom.Next(0, availableRatings.Count - 1)];
                pet.SetProperty(chosenRating, 1);
            }
            else if (rarity == 3 || rarity == 4) // Legendary or Mythic
            {
                // 50/50 chance: +2 to one rating OR +1 to two different ratings
                var option = ThreadSafeRandom.Next(0, 1); // 0 or 1

                if (option == 0) // +2 to one rating
                {
                    var chosenRating = availableRatings[ThreadSafeRandom.Next(0, availableRatings.Count - 1)];
                    pet.SetProperty(chosenRating, 2);
                }
                else // +1 to two different ratings
                {
                    // Pick first rating
                    var firstIndex = ThreadSafeRandom.Next(0, availableRatings.Count - 1);
                    var firstRating = availableRatings[firstIndex];
                    pet.SetProperty(firstRating, 1);

                    // Remove first rating from list and pick second
                    availableRatings.RemoveAt(firstIndex);
                    var secondRating = availableRatings[ThreadSafeRandom.Next(0, availableRatings.Count - 1)];
                    pet.SetProperty(secondRating, 1);
                }
            }
        }

        /// <summary>
        /// Public method for awarding items directly (when not in fellowship)
        /// Still applies weekly mystery egg limits
        /// </summary>
        public static bool AwardItemToPlayerDirect(Player player, uint itemWcid, string rarity)
        {
            // Default to "direct" if rarity is null or empty
            if (string.IsNullOrEmpty(rarity))
                rarity = "direct";

            if (AwardItemToPlayer(player, itemWcid, rarity))
            {
                // Successfully awarded - broadcast globally
                var itemName = GetItemName(itemWcid);
                PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{player.Name} has obtained {itemName}!", ChatMessageType.Broadcast));
                return true;
            }

            return false;
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
        public string Rarity { get; set; }  // common, rare, legendary, mythic, or direct
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

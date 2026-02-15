using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class MysteryEgg : WorldObject
    {
        // CONQUEST: Cached pet weenies by rarity - populated at server startup
        private static Dictionary<int, List<uint>> _petWeenieCache = null;
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Initialize the pet weenie cache - call this at server startup
        /// </summary>
        public static void InitializePetCache()
        {
            lock (_cacheLock)
            {
                _petWeenieCache = new Dictionary<int, List<uint>>();

                // Query database once and cache results
                var allWeenies = DatabaseManager.World.GetAllWeenies();
                var petCount = 0;

                foreach (var weenie in allWeenies)
                {
                    if (weenie.WeeniePropertiesInt == null) continue;

                    var petRarityProp = weenie.WeeniePropertiesInt
                        .FirstOrDefault(p => p.Type == (int)PropertyInt.PetRarity);

                    if (petRarityProp != null)
                    {
                        var petRarity = petRarityProp.Value;
                        if (!_petWeenieCache.ContainsKey(petRarity))
                            _petWeenieCache[petRarity] = new List<uint>();

                        _petWeenieCache[petRarity].Add(weenie.ClassId);
                        petCount++;
                    }
                }

                Console.WriteLine($"[MysteryEgg] Pet cache initialized with {petCount} pets across {_petWeenieCache.Count} rarity tiers.");
            }
        }

        /// <summary>
        /// Add a pet weenie to the cache dynamically (for newly created pets)
        /// </summary>
        public static bool AddPetToCache(uint weenieId, int rarity)
        {
            if (_petWeenieCache == null)
                InitializePetCache();

            lock (_cacheLock)
            {
                if (!_petWeenieCache.ContainsKey(rarity))
                    _petWeenieCache[rarity] = new List<uint>();

                if (_petWeenieCache[rarity].Contains(weenieId))
                    return false; // Already in cache

                _petWeenieCache[rarity].Add(weenieId);
                return true;
            }
        }

        /// <summary>
        /// Remove a pet weenie from the cache
        /// </summary>
        public static bool RemovePetFromCache(uint weenieId, int rarity)
        {
            if (_petWeenieCache == null)
                return false;

            lock (_cacheLock)
            {
                if (_petWeenieCache.TryGetValue(rarity, out var pets))
                    return pets.Remove(weenieId);

                return false;
            }
        }

        /// <summary>
        /// Get cache statistics for admin commands
        /// </summary>
        public static string GetCacheStats()
        {
            if (_petWeenieCache == null)
                return "Pet cache not initialized.";

            lock (_cacheLock)
            {
                var stats = new System.Text.StringBuilder();
                stats.AppendLine("Mystery Egg Pet Cache:");

                // Display each rarity tier in order
                for (int rarity = 1; rarity <= 4; rarity++)
                {
                    var rarityName = rarity switch
                    {
                        1 => "Common",
                        2 => "Rare",
                        3 => "Legendary",
                        4 => "Mythic",
                        _ => $"Rarity {rarity}"
                    };

                    stats.AppendLine($"{rarityName}:");

                    if (_petWeenieCache.TryGetValue(rarity, out var pets) && pets.Count > 0)
                    {
                        foreach (var weenieId in pets.OrderBy(w => w))
                        {
                            stats.AppendLine($"  {weenieId}");
                        }
                    }
                    else
                    {
                        stats.AppendLine("  (none)");
                    }
                }

                return stats.ToString();
            }
        }

        /// <summary>
        /// Gets pets by rarity from cache (public accessor for admin commands)
        /// </summary>
        public static List<uint> GetPetsByRarityPublic(int rarity)
        {
            return GetPetsByRarity(rarity);
        }

        /// <summary>
        /// Gets pets by rarity from cache
        /// </summary>
        private static List<uint> GetPetsByRarity(int rarity)
        {
            // Initialize cache if not yet done (fallback, should be done at startup)
            if (_petWeenieCache == null)
                InitializePetCache();

            lock (_cacheLock)
            {
                return _petWeenieCache.TryGetValue(rarity, out var pets) ? pets : null;
            }
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public MysteryEgg(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public MysteryEgg(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        /// <summary>
        /// Handles the mystery egg hatching after 7 days
        /// </summary>
        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            {
                player.SendWeenieError(WeenieError.YoureTooBusy);
                return;
            }

            // Get the egg's rarity (1=Common, 2=Rare, 3=Legendary, 4=Mythic)
            var eggRarity = GetProperty(PropertyInt.EggRarity);
            if (eggRarity == null)
            {
                player.SendMessage("This egg appears to be inert and cannot hatch.");
                return;
            }

            // Check if egg is ready to hatch (uses Lifespan system now)
            var remainingLifespan = GetRemainingLifespan();
            if (remainingLifespan > 0)
            {
                var daysRemaining = remainingLifespan / 86400.0;
                player.SendMessage($"This egg is not ready to hatch yet. It needs {daysRemaining:F1} more days to mature.");
                return;
            }

            // Get pet weenies for this rarity from cache
            var eligiblePetIds = GetPetsByRarity(eggRarity.Value);
            if (eligiblePetIds == null || eligiblePetIds.Count == 0)
            {
                player.SendMessage($"No pets found for this rarity tier. Please contact an administrator.");
                return;
            }

            // Randomly select a pet weenie ID
            var randomIndex = ThreadSafeRandom.Next(0, eligiblePetIds.Count - 1);
            var selectedPetWcid = eligiblePetIds[randomIndex];

            // Create the pet and give to player
            var pet = WorldObjectFactory.CreateNewWorldObject(selectedPetWcid);
            if (pet == null)
            {
                player.SendMessage($"Error creating pet (WCID: {selectedPetWcid}). Please contact an administrator.");
                return;
            }

            // Set pet rarity on the device so it can be read when summoning
            pet.SetProperty(PropertyInt.PetRarity, eggRarity.Value);

            // Set icon underlay based on rarity
            // Common: 0x06003355, Rare: 0x06003353, Legendary: 0x06003356, Mythic: 0x06003354
            pet.IconUnderlayId = eggRarity.Value switch
            {
                1 => 0x06003355, // Common
                2 => 0x06003353, // Rare
                3 => 0x06003356, // Legendary
                4 => 0x06003354, // Mythic
                _ => null
            };

            // Assign random rating bonuses based on rarity
            AssignRandomPetRatings(pet, eggRarity.Value);

            // CONQUEST: Apply random palette to the pet device for visual variety
            var petDevice = pet as PetDevice;
            if (petDevice?.PetClass != null)
            {
                ACE.Server.Managers.PetPaletteManager.ApplyRandomPalette(pet, (uint)petDevice.PetClass.Value);
            }

            // Try to add to inventory
            if (player.TryCreateInInventoryWithNetworking(pet))
            {
                var rarityName = eggRarity.Value switch
                {
                    1 => "Common",
                    2 => "Rare",
                    3 => "Legendary",
                    4 => "Mythic",
                    _ => "Unknown"
                };

                player.SendMessage($"Your {rarityName} Mystery Egg hatched into {pet.Name}!");

                // CONQUEST: Add quest stamp for tracking pet acquisition (RarityName + PetName without spaces)
                var petNameNoSpaces = pet.Name?.Replace(" ", "") ?? "Unknown";
                var questStamp = $"{rarityName}{petNameNoSpaces}";
                player.QuestManager.Stamp(questStamp);

                // Global broadcast for Legendary and Mythic
                if (eggRarity.Value >= 3)
                {
                    var broadcastMsg = $"{player.Name}'s {rarityName} Mystery Egg hatched into {pet.Name}!";
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat(broadcastMsg, ChatMessageType.Broadcast));
                }

                // Consume the egg
                player.TryConsumeFromInventoryWithNetworking(this, 1);
            }
            else
            {
                // Inventory full
                player.SendMessage($"Your inventory is full! Make space before hatching this egg.");

                // Destroy the created pet since we couldn't give it
                pet.Destroy();
            }
        }

        /// <summary>
        /// CONQUEST: Auto-hatch the egg when lifespan expires
        /// Called from Container_Tick when the egg's timer runs out
        /// Static method to work with any WorldObject that has EggRarity property
        /// </summary>
        public static void TryAutoHatch(WorldObject egg, Player player)
        {
            if (egg == null || player == null)
                return;

            // Get the egg's rarity
            var eggRarity = egg.GetProperty(PropertyInt.EggRarity);
            if (eggRarity == null)
            {
                // Invalid egg - just delete it
                player.TryConsumeFromInventoryWithNetworking(egg, 1);
                return;
            }

            // Get pet weenies for this rarity from cache
            var eligiblePetIds = GetPetsByRarity(eggRarity.Value);
            if (eligiblePetIds == null || eligiblePetIds.Count == 0)
            {
                player.SendMessage($"Your Mystery Egg tried to hatch but no pets were found for this rarity tier. Please contact an administrator.");
                // Extend lifespan by 1 hour so it doesn't spam
                egg.Lifespan = (egg.Lifespan ?? 0) + 3600;
                return;
            }

            // Randomly select a pet weenie ID
            var randomIndex = ThreadSafeRandom.Next(0, eligiblePetIds.Count - 1);
            var selectedPetWcid = eligiblePetIds[randomIndex];

            // Create the pet
            var pet = WorldObjectFactory.CreateNewWorldObject(selectedPetWcid);
            if (pet == null)
            {
                player.SendMessage($"Your Mystery Egg tried to hatch but failed to create pet (WCID: {selectedPetWcid}). Please contact an administrator.");
                egg.Lifespan = (egg.Lifespan ?? 0) + 3600;
                return;
            }

            // Set pet rarity on the device so it can be read when summoning
            pet.SetProperty(PropertyInt.PetRarity, eggRarity.Value);

            // Set icon underlay based on rarity
            // Common: 0x06003355, Rare: 0x06003353, Legendary: 0x06003356, Mythic: 0x06003354
            pet.IconUnderlayId = eggRarity.Value switch
            {
                1 => 0x06003355, // Common
                2 => 0x06003353, // Rare
                3 => 0x06003356, // Legendary
                4 => 0x06003354, // Mythic
                _ => null
            };

            // Assign random rating bonuses based on rarity
            AssignRandomPetRatings(pet, eggRarity.Value);

            // CONQUEST: Apply random palette to the pet device for visual variety
            var petDevice = pet as PetDevice;
            if (petDevice?.PetClass != null)
            {
                ACE.Server.Managers.PetPaletteManager.ApplyRandomPalette(pet, (uint)petDevice.PetClass.Value);
            }

            // Try to add to inventory
            if (player.TryCreateInInventoryWithNetworking(pet))
            {
                var rarityName = eggRarity.Value switch
                {
                    1 => "Common",
                    2 => "Rare",
                    3 => "Legendary",
                    4 => "Mythic",
                    _ => "Unknown"
                };

                player.SendMessage($"Your {rarityName} Mystery Egg has hatched into {pet.Name}!");

                // CONQUEST: Add quest stamp for tracking pet acquisition (RarityName + PetName without spaces)
                var petNameNoSpaces = pet.Name?.Replace(" ", "") ?? "Unknown";
                var questStamp = $"{rarityName}{petNameNoSpaces}";
                player.QuestManager.Stamp(questStamp);

                // Global broadcast for Legendary and Mythic
                if (eggRarity.Value >= 3)
                {
                    var broadcastMsg = $"{player.Name}'s {rarityName} Mystery Egg hatched into {pet.Name}!";
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat(broadcastMsg, ChatMessageType.Broadcast));
                }

                // Consume the egg
                player.TryConsumeFromInventoryWithNetworking(egg, 1);
            }
            else
            {
                // Inventory full - notify player and extend lifespan slightly so it tries again later
                player.SendMessage($"Your Mystery Egg is ready to hatch but your inventory is full! Make space soon.");
                // Extend lifespan by 5 minutes to try again
                egg.Lifespan = (egg.Lifespan ?? 0) + 300;

                // Destroy the created pet since we couldn't give it
                pet.Destroy();
            }
        }

        /// <summary>
        /// Randomly assigns rating bonuses to a pet based on its rarity tier
        /// Rare: +1 to one random rating
        /// Legendary/Mythic: 50% chance for +2 to one rating, 50% chance for +1 to two different ratings
        /// </summary>
        public static void AssignRandomPetRatings(WorldObject pet, int rarity)
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
    }
}

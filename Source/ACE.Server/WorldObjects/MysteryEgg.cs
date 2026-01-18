using System;
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

            // Check if 7 days have passed since creation
            var creationTime = GetProperty(PropertyInt.CreationTimestamp) ?? 0;
            var currentTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var daysElapsed = (currentTime - creationTime) / 86400.0; // 86400 seconds in a day
            var daysRemaining = 7.0 - daysElapsed;

            if (daysRemaining > 0)
            {
                player.SendMessage($"This egg is not ready to hatch yet. It needs {daysRemaining:F1} more days to mature.");
                return;
            }

            // Get all pet weenies with matching rarity
            var allWeenies = DatabaseManager.World.GetAllWeenies();
            var eligiblePets = allWeenies
                .Where(w => w.WeeniePropertiesInt != null && w.WeeniePropertiesInt.Any(p => p.Type == (int)PropertyInt.PetRarity && p.Value == eggRarity.Value))
                .ToList();

            if (eligiblePets.Count == 0)
            {
                player.SendMessage($"No pets found for this rarity tier. Please contact an administrator.");
                return;
            }

            // Randomly select a pet
            var randomIndex = ThreadSafeRandom.Next(0, eligiblePets.Count - 1);
            var selectedPet = eligiblePets[randomIndex];

            // Create the pet and give to player
            var pet = WorldObjectFactory.CreateNewWorldObject(selectedPet.ClassId);
            if (pet == null)
            {
                player.SendMessage($"Error creating pet (WCID: {selectedPet.ClassId}). Please contact an administrator.");
                return;
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
    }
}

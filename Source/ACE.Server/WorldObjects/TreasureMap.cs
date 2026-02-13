using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Position = ACE.Entity.Position;

namespace ACE.Server.WorldObjects
{
    public partial class TreasureMap : GenericObject
    {
        public TreasureMap(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
        }

        public TreasureMap(Biota biota) : base(biota)
        {
        }

        private List<uint> TreasureChests = new List<uint>()
        {
            90000112,
            90000113,
            90000116,
        };

        public static WorldObject TryCreateTreasureMap(Weenie creatureWeenie)
        {
            if (creatureWeenie.WeenieType != WeenieType.Creature)
                return null;

            var creature = WorldObjectFactory.CreateNewWorldObject(creatureWeenie) as Creature;

            if (creature == null)
                return null;

            var treasure = TryCreateTreasureMap(creature);  // Call the new simplified version for the treasure map creation
            creature.Destroy();

            return treasure;
        }

        public static WorldObject TryCreateTreasureMap(Creature creature)
        {
            if (creature == null)
                return null;

            // Retry mechanism to keep trying until valid coordinates are found
            float randomLatitude = 0, randomLongitude = 0;
            bool validCoordinates = false;

            // Loop to keep retrying until valid coordinates are found
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // Generate random coordinates between -90 and 90 for both latitude and longitude
                randomLatitude = ThreadSafeRandom.Next(-90, 90);
                randomLongitude = ThreadSafeRandom.Next(-90, 90);

                //Console.WriteLine($"[DEBUG] Attempting to generate coordinates: {randomLatitude}, {randomLongitude}");

                // Validate coordinates
                if (AreCoordinatesValid(randomLatitude, randomLongitude))
                {
                    validCoordinates = true;
                    break; // Valid coordinates found, break out of the loop
                }
                else
                {
                    //Console.WriteLine("[DEBUG] Invalid coordinates, retrying...");
                }
            }

            // If no valid coordinates were found after retries, return null
            if (!validCoordinates)
            {
                //Console.WriteLine("[DEBUG] No valid coordinates found after retries.");
                return null;
            }

            // Create the treasure map world object
            var wo = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.treasureMap);
            if (wo == null)
                return null;

            // Set the map's name, description, and coordinates
            wo.Name = $"{creature.Name}'s Treasure Map";
            wo.LongDesc = $"This map was found in the corpse of a level {creature.Level} {creature.Name}. It leads to a hidden treasure.";

            // Assign the random coordinates to the treasure map
            wo.EWCoordinates = randomLongitude;
            wo.NSCoordinates = randomLatitude;

            // Store the creature type for quest stamp on pickup
            if (creature.CreatureType.HasValue)
                wo.SetProperty(PropertyInt.CreatureType, (int)creature.CreatureType.Value);

            return wo;
        }

        public static bool AreCoordinatesValid(float latitude, float longitude)
        {
            // Generate a Position object with the given latitude and longitude
            var position = new Position((float)latitude, (float)longitude, null);

            // Convert the position to map coordinates
            var mapCoords = position.GetMapCoords();

            if (mapCoords == null)
            {
                //Console.WriteLine($"[DEBUG] Invalid coordinates: Latitude {latitude}, Longitude {longitude}. Coordinates are outside the map bounds.");
                return false;  // If the coordinates are not valid map coordinates, return false
            }

            // Optionally, you can add additional checks for specific coordinate ranges
            // For example, ensuring the coordinates fall within the playable area:
            // Latitude: -102 to +102 map units, Longitude: -102 to +102 map units
            if (mapCoords.Value.X < -102 || mapCoords.Value.X > 102 || mapCoords.Value.Y < -102 || mapCoords.Value.Y > 102)
            {
                //Console.WriteLine($"[DEBUG] Coordinates are out of bounds on the map. Coordinates: {mapCoords.Value.X}, {mapCoords.Value.Y}");
                return false;
            }

            // Use LScape.get_landcell to check if the generated coordinates are blocked (by water, building, etc.)
            var landcell = LScape.get_landcell(position.GetCell(), null) as SortCell;
            if (landcell == null || landcell.has_building() || landcell.CurLandblock.WaterType == LandDefs.WaterType.EntirelyWater)
            {
                //Console.WriteLine($"[DEBUG] Coordinates are blocked by water or building at Latitude {latitude}, Longitude {longitude}.");
                return false;  // Coordinates are blocked, return false
            }

            // Ensure the location is walkable
            var pos = new Physics.Common.Position();
            var location = pos.ACEPosition();
            if (!location.IsWalkable())
            {
               // Console.WriteLine($"[DEBUG] Coordinates are not walkable at Latitude {latitude}, Longitude {longitude}.");
                return false;  // If the coordinates are not walkable, return false
            }

            // Coordinates are valid, return true
            //Console.WriteLine($"[DEBUG] Valid coordinates found: Latitude {latitude}, Longitude {longitude}.");
            return true;
        }

        public static bool WieldingShovel(Player player)
        {
            return player.QuestManager.GetCurrentSolves("WieldingShovel") >= 1;
        }

        /// <summary>
        /// Gives treasure map rewards to player based on probability table
        /// 100% 5 conquest coins, 100% 3 MMDs, then probability rolls for optional rewards
        /// Min 3 reward types, Max 5 reward types
        /// </summary>
        private void GiveLootToPlayer(Player player)
        {
            var rewardsGiven = new List<(uint wcid, int quantity, string name)>();

            // GUARANTEED REWARDS (100% chance)
            rewardsGiven.Add((13370001, 5, "Conquest Coins"));      // 100% 5 conquest coins
            rewardsGiven.Add((20630, 3, "Trade Notes (250,000)")); // 100% 3 MMDs

            // OPTIONAL REWARDS (probability-based)
            var optionalRewards = new List<(uint wcid, int quantity, string name, int chance)>
            {
                (13370003, 3, "Soul Fragments", 25),           // 25% 3 soul fragments
                (29295, 1, "Blank Augmentation Gem", 15),      // 15% 1 xp aug gem
                (43901, 25, "Promissory Notes", 15),           // 15% 25 prom notes
                (7299, 10, "Diamond Scarabs", 15),             // 15% 10 diamond scarabs
                (46423, 1, "Stipend", 5),                      // 5% 1 stipend
                (13370001, 50, "Conquest Coins (Bonus)", 1)    // 1% 50 conquest coins (separate from guaranteed)
            };

            // Roll for each optional reward
            foreach (var reward in optionalRewards)
            {
                var roll = ThreadSafeRandom.Next(1, 100); // 1-100
                if (roll <= reward.chance)
                {
                    rewardsGiven.Add((reward.wcid, reward.quantity, reward.name));
                }
            }

            // Enforce min/max reward type constraints
            if (rewardsGiven.Count < 3)
            {
                // Need to add more rewards to reach minimum of 3 types
                var remainingRewards = optionalRewards
                    .Where(opt => !rewardsGiven.Any(r => r.wcid == opt.wcid && r.quantity == opt.quantity))
                    .ToList();

                while (rewardsGiven.Count < 3 && remainingRewards.Count > 0)
                {
                    var randomIndex = ThreadSafeRandom.Next(0, remainingRewards.Count - 1);
                    var bonusReward = remainingRewards[randomIndex];
                    rewardsGiven.Add((bonusReward.wcid, bonusReward.quantity, bonusReward.name));
                    remainingRewards.RemoveAt(randomIndex);
                }
            }
            else if (rewardsGiven.Count > 5)
            {
                // Cap at 5 reward types - remove lowest priority items (keep guaranteed + highest % rolls)
                // Remove from the end (lowest % chances) until we have 5
                while (rewardsGiven.Count > 5)
                {
                    rewardsGiven.RemoveAt(rewardsGiven.Count - 1);
                }
            }

            // Give all rewards to player
            foreach (var reward in rewardsGiven)
            {
                var loot = WorldObjectFactory.CreateNewWorldObject(reward.wcid);
                if (loot != null)
                {
                    if (loot is Stackable)
                        loot.SetStackSize(reward.quantity);

                    if (player.TryCreateInInventoryWithNetworking(loot))
                    {
                        var quantityText = reward.quantity > 1 ? $"({reward.quantity}) " : "";
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                            $"You have received {quantityText}{reward.name}!",
                            ChatMessageType.System));
                    }
                    else
                    {
                        loot.Destroy();
                    }
                }
            }
        }


        public override void ActOnUse(WorldObject activator)
        {
            //Console.WriteLine($"[DEBUG] ActOnUse called for {this.Name} by {activator.Name}");
            if (!(activator is Player player))
                return;

            // Prevent spam-clicking during treasure digging
            if (player.IsBusy)
                return;

            // Check if player is wielding a shovel before proceeding
            if (!WieldingShovel(player))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You must be wielding Tiny Tina's shovel to search for treasure.", ChatMessageType.System));
                return;
            }

            //Console.WriteLine("Treasure map used by player: " + player.Name);  // Debug log to verify ActOnUse is triggered

            player.SyncLocation(null);
            player.EnqueueBroadcast(new GameMessageUpdatePosition(player));

            var position = new Position((float)(NSCoordinates ?? 0f), (float)(EWCoordinates ?? 0f), null);
            //Console.WriteLine($"Treasure map coordinates: {position.CellX}, {position.CellY}");
            position.AdjustMapCoords();

            var distance = Math.Abs(position.GetLargestOffset(player.Location));
           // Console.WriteLine($"Distance from player: {distance}");

            if (distance > 5000)
            {
                //Console.WriteLine("[DEBUG] Distance is greater than 5000, starting animation...");
                var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.Reading);
                var actionChain = new ActionChain();
                actionChain.AddAction(player, ActionType.TreasureMap_ReadingAnimation, () =>
                {
                    player.IsBusy = true;
                    player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Reading));
                });
                actionChain.AddDelaySeconds(animTime + 1);
                actionChain.AddAction(player, ActionType.TreasureMap_ShowLocation, () =>
                {
                    string directions;
                    string name;
                    var entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)position.Landblock).FirstOrDefault();
                    if (entryLandblock != null)
                    {
                        name = entryLandblock.Name;
                        directions = $"{entryLandblock.Directions} {entryLandblock.Reference}";
                    }
                    else
                    {
                        name = $"an unknown location({position.Landblock})";
                        directions = "";
                    }

                    var message = $"The treasure map points to {name} {directions}.";
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.System));

                    player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));
                    player.IsBusy = false;
                });
                actionChain.EnqueueChain();
            }
            else
            {
                //Console.WriteLine("[DEBUG] Distance is within 5000, checking damage status...");

                if (distance > 2 || !DamageMod.HasValue)
                {
                    //Console.WriteLine("[DEBUG] DamageMod is null or distance > 2, triggering scan horizon animation...");
                    var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.ScanHorizon);
                    var actionChain = new ActionChain();
                    actionChain.AddAction(player, ActionType.TreasureMap_ScanHorizon, () =>
                    {
                        player.IsBusy = true;
                        player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.ScanHorizon));
                    });
                    actionChain.AddDelaySeconds(animTime);
                    actionChain.AddAction(player, ActionType.TreasureMap_ShowDirection, () =>
                    {
                        player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));

                        var direction = player.Location.GetCardinalDirectionsTo(position);

                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The treasure map points {(direction == "" ? "at" : $"{direction} of")} your current location.", ChatMessageType.System));

                        if (distance <= 2 && !DamageMod.HasValue)
                        {
                            //Console.WriteLine("[DEBUG] DamageMod set to 1, triggering cheer animation...");
                            DamageMod = 1;
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Cheer));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));
                        }
                        else
                            DamageMod = null;

                        player.IsBusy = false;
                    });
                    actionChain.EnqueueChain();
                }
                else
                {
                    if (!Damage.HasValue)
                        Damage = 0;

                    //Console.WriteLine("[DEBUG] Damage counter: " + Damage);
                    if (Damage < 7)
                    {
                        string msg;
                        if (Damage == 0)
                        {
                            msg = "You start to dig for treasure!";
                            //Console.WriteLine("[DEBUG] Starting to dig for treasure!");
                        }
                        else
                        {
                            msg = "You continue to dig for treasure!";
                            //Console.WriteLine("[DEBUG] Continuing to dig for treasure!");
                        }

                        Damage++;

                        var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.Pickup);
                        var actionChain = new ActionChain();
                        actionChain.AddAction(player, ActionType.TreasureMap_Digging, () =>
                        {
                            player.IsBusy = true;
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.PointDown));
                        });
                        actionChain.AddDelaySeconds(animTime);
                        actionChain.AddAction(player, ActionType.TreasureMap_Digging, () =>
                        {
                            player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.HitLeather1, 1.0f));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));
                            player.EnqueueBroadcast(new GameMessageSystemChat(msg, ChatMessageType.System));
                            var visibleCreatures = player.PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();
                            foreach (var creature in visibleCreatures)
                            {
                                if (!creature.IsDead && !creature.IsAwake)
                                    player.AlertMonster(creature);
                            }

                            player.IsBusy = false;
                        });
                        actionChain.EnqueueChain();
                    }
                    else
                    {
                        //Console.WriteLine("[DEBUG] Damage counter has reached 7, creating treasure chest...");

                        var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.Pickup);
                        var actionChain = new ActionChain();
                        actionChain.AddAction(player, ActionType.TreasureMap_FoundTreasure, () =>
                        {
                            player.IsBusy = true;
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Pickup));
                        });
                        actionChain.AddDelaySeconds(animTime);
                        actionChain.AddAction(player, ActionType.TreasureMap_FoundTreasure, () =>
                        {
                            player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.HitPlate1, 1.0f));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));
                            player.EnqueueBroadcast(new GameMessageSystemChat("You found the buried treasure!", ChatMessageType.System));

                            // After the message is shown, give loot to the player
                            GiveLootToPlayer(player);  // Call to give loot directly to the player's inventory

                            // Remove the treasure map from the player's inventory
                            if (!player.TryConsumeFromInventoryWithNetworking(this, 1))
                            {
                                //Console.WriteLine("[DEBUG] Failed to remove treasure map from player's inventory.");
                            }
                            else
                            {
                               // Console.WriteLine("[DEBUG] Treasure map successfully removed from player's inventory.");
                            }

                            player.IsBusy = false;
                        });
                        actionChain.EnqueueChain();
                    }
                }
            }
        }
    }
}

using System;
using System.Threading;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        private readonly bool biotaOriginatedFromDatabase;

        // DB queue monitoring - static fields for rate limiting Discord alerts
        private static readonly object dbQueueAlertLock = new object();
        private static DateTime lastDbQueueAlert = DateTime.MinValue;
        private static int dbQueueAlertsThisMinute = 0;

        public DateTime LastRequestedDatabaseSave { get; protected set; }

        /// <summary>
        /// This variable is set to true when a change is made, and set to false before a save is requested.<para />
        /// The primary use for this is to trigger save on add/modify/remove of properties.
        /// </summary>
        public bool ChangesDetected { get; set; }

        /// <summary>
        /// Best practice says you should use this lock any time you read/write the Biota.<para />
        /// However, it's only a requirement to do this for properties/collections that will be modified after the initial biota has been created.<para />
        /// There are several properties/collections of the biota that are simply duplicates of the original weenie and are never changed. You wouldn't need to use this lock to read those collections.<para />
        /// <para />
        /// For absolute maximum performance, if you're willing to assume (and risk) the following:<para />
        ///  - that the biota in the database will not be modified (in a way that adds or removes properties) outside of ACE while ACE is running with a reference to that biota<para />
        ///  - that the biota will only be read/modified by a single thread in ACE<para />
        /// You can remove the lock usage for any Get/GetAll Property functions. You would simply use it for Set/Remove Property functions because each of these could end up adding/removing to the collections.<para />
        /// The critical thing is that the collections are not added to or removed from while Entity Framework is iterating over them.<para />
        /// Mag-nus 2018-08-19
        /// </summary>
        public readonly ReaderWriterLockSlim BiotaDatabaseLock = new ReaderWriterLockSlim();

        public bool BiotaOriginatedFromOrHasBeenSavedToDatabase()
        {
            return biotaOriginatedFromDatabase || LastRequestedDatabaseSave != DateTime.MinValue;
        }

        /// <summary>
        /// This will set the LastRequestedDatabaseSave to UtcNow and ChangesDetected to false.<para />
        /// If enqueueSave is set to true, DatabaseManager.Shard.SaveBiota() will be called for the biota.<para />
        /// Set enqueueSave to false if you want to perform all the normal routines for a save but not the actual save. This is useful if you're going to collect biotas in bulk for bulk saving.
        /// </summary>
        public virtual void SaveBiotaToDatabase(bool enqueueSave = true)
        {
            // Make sure all of our positions in the biota are up to date with our current cached values.
            foreach (var kvp in positionCache)
            {
                if (kvp.Value != null)
                    Biota.SetPosition(kvp.Key, kvp.Value, BiotaDatabaseLock);
            }

            LastRequestedDatabaseSave = DateTime.UtcNow;
            ChangesDetected = false;

            if (enqueueSave)
            {
                CheckpointTimestamp = Time.GetUnixTime();
                //DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, null);
                DatabaseManager.Shard.SaveBiota(Biota, BiotaDatabaseLock, result =>
                {
                    if (!result)
                    {
                        if (this is Player player)
                        {
                            // This will trigger a boot on next player tick
                            player.BiotaSaveFailed = true;
                        }
                    }

                    // Check database queue size and alert if threshold exceeded
                    CheckDatabaseQueueSize();
                });
            }
        }

        /// <summary>
        /// This will set the LastRequestedDatabaseSave to MinValue and ChangesDetected to true.<para />
        /// If enqueueRemove is set to true, DatabaseManager.Shard.RemoveBiota() will be called for the biota.<para />
        /// Set enqueueRemove to false if you want to perform all the normal routines for a remove but not the actual removal. This is useful if you're going to collect biotas in bulk for bulk removing.
        /// </summary>
        public void RemoveBiotaFromDatabase(bool enqueueRemove = true)
        {
            // If this entity doesn't exist in the database, let's not queue up work unnecessary database work.
            if (!BiotaOriginatedFromOrHasBeenSavedToDatabase())
            {
                ChangesDetected = true;
                return;
            }

            LastRequestedDatabaseSave = DateTime.MinValue;
            ChangesDetected = true;

            if (enqueueRemove)
                DatabaseManager.Shard.RemoveBiota(Biota.Id, null);
        }

        /// <summary>
        /// A static that should persist to the shard may be a hook with an item, or a house that's been purchased, or a housing chest that isn't empty, etc...<para />
        /// If the world object originated from the database or has been saved to the database, this will also return true.
        /// </summary>
        public bool IsStaticThatShouldPersistToShard()
        {
            if (!Guid.IsStatic())
                return false;

            if (BiotaOriginatedFromOrHasBeenSavedToDatabase())
                return true;

            if (WeenieType == WeenieType.SlumLord && this is SlumLord slumlord)
            {
                if (slumlord.House != null && slumlord.House.HouseOwner.HasValue && slumlord.House.HouseOwner != 0)
                    return true;
            }

            if (WeenieType == WeenieType.House && this is House house)
            {
                if (house.HouseOwner.HasValue && house.HouseOwner != 0)
                    return true;
            }

            if ((WeenieType == WeenieType.Hook || WeenieType == WeenieType.Storage) && this is Container container)
            {
                if (container.Inventory.Count > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// This will filter out the following:<para />
        /// Ammunition and Spell projectiles.<para />
        /// Monster corpses.<para />
        /// Missiles that haven't been saved to the shard yet.<para />
        /// If the world object originated from the database or has been saved to the database, this will also return true.
        /// </summary>
        /// <returns></returns>
        public bool IsDynamicThatShouldPersistToShard()
        {
            if (!Guid.IsDynamic())
                return false;

            if (BiotaOriginatedFromOrHasBeenSavedToDatabase())
                return true;

            // Don't save generators, and items that were generated by a generator
            // If the item was generated by a generator and then picked up by a player, the wo.Generator property would be set to null.
            if (IsGenerator || Generator != null)
                return false;

            if (WeenieType == WeenieType.Missile || WeenieType == WeenieType.Ammunition || WeenieType == WeenieType.ProjectileSpell || WeenieType == WeenieType.GamePiece
                || WeenieType == WeenieType.Pet || WeenieType == WeenieType.CombatPet)
                return false;

            if (WeenieType == WeenieType.Corpse && this is Corpse corpse && corpse.IsMonster)
                return false;

            if (WeenieType == WeenieType.Portal && this is Portal portal && portal.IsGateway)
                return false;

            // Missiles are unique. The only missiles that are persistable are ones that already exist in the database.
            // TODO: See if we can remove this check by catching the WeenieType above.
            var missile = Missile;
            if (missile.HasValue && missile.Value)
            {
                log.Warn($"Missile: WeenieClassId: {WeenieClassId}, Name: {Name}, WeenieType: {WeenieType}, detected in IsDynamicThatShouldPersistToShard() that wasn't caught by prior check.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Monitors database queue size and sends Discord alerts when threshold is exceeded
        /// </summary>
        private static void CheckDatabaseQueueSize()
        {
            var queueThreshold = PropertyManager.GetLong("db_queue_alert_threshold").Item;
            if (queueThreshold <= 0)
                return;  // Monitoring disabled

            var queueCount = DatabaseManager.Shard.QueueCount;
            if (queueCount <= queueThreshold)
                return;  // Queue size acceptable

            lock (dbQueueAlertLock)
            {
                var now = DateTime.UtcNow;

                // Reset counter every minute
                if ((now - lastDbQueueAlert).TotalMinutes >= 1)
                {
                    dbQueueAlertsThisMinute = 0;
                }

                // Check rate limit
                var maxAlerts = PropertyManager.GetLong("db_queue_discord_max_alerts_per_minute").Item;
                if (maxAlerts <= 0 || dbQueueAlertsThisMinute >= maxAlerts)
                    return;

                // Check Discord is configured
                if (!ConfigManager.Config.Chat.EnableDiscordConnection ||
                    ConfigManager.Config.Chat.PerformanceAlertsChannelId <= 0)
                    return;

                try
                {
                    var msg = $"🔴 **DB QUEUE HIGH**: Queue count at **{queueCount}** (threshold: {queueThreshold}). Potential save delays and item loss risk!";

                    DiscordChatManager.SendDiscordMessage("DB DIAGNOSTICS", msg,
                        ConfigManager.Config.Chat.PerformanceAlertsChannelId);

                    dbQueueAlertsThisMinute++;
                    lastDbQueueAlert = now;
                }
                catch
                {
                    // Silently fail on Discord errors to avoid log spam
                }
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories.Enum;


namespace ACE.Server.Managers
{
    /// <summary>
    /// CONQUEST: Champion Mutation System
    /// Manages the wcid→tier cache for determining champion mutation tiers based on creature death treasure.
    /// </summary>
    public static class ChampionManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Cache mapping creature wcid to their treasure tier (1-8)
        /// Built on server startup from weenie DeathTreasureType -> treasure_death.Tier lookup
        /// </summary>
        private static readonly ConcurrentDictionary<uint, int> wcidToTierCache = new ConcurrentDictionary<uint, int>();

        /// <summary>
        /// Whether the cache has been initialized
        /// </summary>
        private static bool isInitialized = false;

        /// <summary>
        /// Initializes the wcid→tier cache on server startup.
        /// This should be called after weenies and treasure death tables are cached.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized)
                return;

            log.Info("[CHAMPION] Building wcid to tier cache...");
            var startTime = DateTime.UtcNow;

            int count = 0;
            int creatureCount = 0;

            // Get all weenies from the cache
            var weenieCache = DatabaseManager.World;

            // We need to iterate through all creature weenies
            // Unfortunately we need to load them to check DeathTreasureType
            using (var context = new ACE.Database.Models.World.WorldDbContext())
            {
                // Get all creature weenies with a DeathTreasureType
                var creatureWeenies = context.Weenie
                    .Where(w => w.Type == (int)ACE.Entity.Enum.WeenieType.Creature)
                    .Select(w => new { w.ClassId, DeathTreasure = w.WeeniePropertiesDID.FirstOrDefault(p => p.Type == (ushort)PropertyDataId.DeathTreasureType) })
                    .Where(w => w.DeathTreasure != null)
                    .ToList();

                creatureCount = creatureWeenies.Count;

                foreach (var weenie in creatureWeenies)
                {
                    var deathTreasureType = weenie.DeathTreasure?.Value ?? 0;
                    if (deathTreasureType == 0)
                        continue;

                    // Look up the treasure death to get the tier
                    var treasureDeath = DatabaseManager.World.GetCachedDeathTreasure(deathTreasureType);
                    if (treasureDeath == null)
                        continue;

                    // Cache the tier for this wcid
                    wcidToTierCache[weenie.ClassId] = treasureDeath.Tier;
                    count++;
                }
            }

            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            log.Info($"[CHAMPION] Built wcid to tier cache: {count} creatures mapped from {creatureCount} total creatures in {elapsed:F2}s");

            isInitialized = true;
        }

        /// <summary>
        /// Gets the treasure tier for a given wcid.
        /// Returns 0 if the wcid is not in the cache.
        /// </summary>
        public static int GetTierForWcid(uint wcid)
        {
            if (wcidToTierCache.TryGetValue(wcid, out var tier))
                return tier;

            // If not in cache, try to look it up directly
            var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
            if (weenie == null)
                return 0;

            // Access PropertiesDID dictionary directly (ACE.Entity.Models.Weenie uses dictionaries, not extension methods)
            if (weenie.PropertiesDID == null || !weenie.PropertiesDID.TryGetValue(PropertyDataId.DeathTreasureType, out var deathTreasureType))
                return 0;

            var treasureDeath = DatabaseManager.World.GetCachedDeathTreasure(deathTreasureType);
            if (treasureDeath == null)
                return 0;

            // Cache it for future lookups
            wcidToTierCache[wcid] = treasureDeath.Tier;
            return treasureDeath.Tier;
        }

        /// <summary>
        /// Determines the champion mutation tier based on the creature's treasure tier.
        /// The mutation tier can roll from 1 up to the creature's treasure tier.
        /// </summary>
        public static int RollChampionTier(uint wcid)
        {
            var maxTier = GetTierForWcid(wcid);
            if (maxTier <= 0)
                return 1; // Default to tier 1 if no treasure tier found

            // Roll a tier from 1 to maxTier
            return ACE.Common.ThreadSafeRandom.Next(1, maxTier);
        }

        /// <summary>
        /// Rolls for champion mutations based on the tier.
        /// Returns the mutation flags to apply.
        /// </summary>
        public static ChampionMutationType RollMutations(int tier)
        {
            var mutations = ChampionMutationType.None;

            // Always roll one primary mutation type (offensive, defensive, or critical)
            var primaryRoll = ACE.Common.ThreadSafeRandom.Next(0, 2);
            switch (primaryRoll)
            {
                case 0:
                    mutations |= ChampionMutationTypeExtensions.GetOffensiveMutation(tier);
                    break;
                case 1:
                    mutations |= ChampionMutationTypeExtensions.GetDefensiveMutation(tier);
                    break;
                case 2:
                    mutations |= ChampionMutationTypeExtensions.GetCriticalMutation(tier);
                    break;
            }

            // 30% chance for an additional mutation
            if (ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f) < 0.30f)
            {
                var secondaryRoll = ACE.Common.ThreadSafeRandom.Next(0, 2);
                // Make sure we don't duplicate the primary
                while (secondaryRoll == primaryRoll)
                    secondaryRoll = ACE.Common.ThreadSafeRandom.Next(0, 2);

                switch (secondaryRoll)
                {
                    case 0:
                        mutations |= ChampionMutationTypeExtensions.GetOffensiveMutation(tier);
                        break;
                    case 1:
                        mutations |= ChampionMutationTypeExtensions.GetDefensiveMutation(tier);
                        break;
                    case 2:
                        mutations |= ChampionMutationTypeExtensions.GetCriticalMutation(tier);
                        break;
                }
            }

            // 10% chance for Rich mutation
            if (ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f) < 0.10f)
                mutations |= ChampionMutationType.Rich;

            // 10% chance for Tempest mutation (SpellChain/SplitArrow)
            if (ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f) < 0.10f)
                mutations |= ChampionMutationType.Tempest;

            // 5% chance for Enraged mutation (boss mechanics)
            if (ACE.Common.ThreadSafeRandom.Next(0.0f, 1.0f) < 0.05f)
                mutations |= ChampionMutationType.Enraged;

            return mutations;
        }

        /// <summary>
        /// Gets the health bonus multiplier for a given champion tier.
        /// Scales from +25% at T1 to +100% at T8.
        /// </summary>
        public static float GetHealthBonusMultiplier(int tier)
        {
            // Linear scaling: T1 = 1.25, T8 = 2.0
            // Formula: 1.0 + (0.25 + (tier - 1) * 0.107)
            // Simplified: 1.0 + 0.143 + tier * 0.107
            return 1.0f + 0.25f + (tier - 1) * (0.75f / 7f);
        }

        /// <summary>
        /// Gets the XP/Luminance bonus multiplier for a given champion tier.
        /// Scales from +25% at T1 to +100% at T8.
        /// </summary>
        public static float GetXpBonusMultiplier(int tier)
        {
            return GetHealthBonusMultiplier(tier);
        }

        /// <summary>
        /// Gets the loot quality modifier for a given champion tier.
        /// </summary>
        public static float GetLootQualityMod(int tier, ChampionMutationType mutations)
        {
            // Base loot quality bonus per tier
            float baseMod = 0.05f * tier; // +5% per tier

            // Rich mutation adds extra loot quality
            if (mutations.HasFlag(ChampionMutationType.Rich))
                baseMod += 0.25f; // +25% additional

            return baseMod;
        }

        /// <summary>
        /// Gets the size scale multiplier for champions (+15% as requested)
        /// </summary>
        public static float GetSizeScale()
        {
            return 1.15f;
        }

        /// <summary>
        /// Gets the cache statistics for debugging
        /// </summary>
        public static string GetCacheStats()
        {
            return $"ChampionManager: {wcidToTierCache.Count} wcids cached";
        }
    }
}

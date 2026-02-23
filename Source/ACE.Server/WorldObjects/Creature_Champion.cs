using System;

using log4net;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories.Enum;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// CONQUEST: Champion Mutation System
    /// Handles applying champion mutations to creatures
    /// </summary>
    partial class Creature
    {
        private static readonly ILog championLog = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Whether this creature is a champion
        /// </summary>
        public bool IsChampion
        {
            get => GetProperty(PropertyBool.IsChampion) ?? false;
            set { if (value) SetProperty(PropertyBool.IsChampion, value); else RemoveProperty(PropertyBool.IsChampion); }
        }

        /// <summary>
        /// The champion tier (1-8)
        /// </summary>
        public int? ChampionTier
        {
            get => GetProperty(PropertyInt.ChampionTier);
            set { if (value.HasValue) SetProperty(PropertyInt.ChampionTier, value.Value); else RemoveProperty(PropertyInt.ChampionTier); }
        }

        /// <summary>
        /// The champion mutation flags
        /// </summary>
        public ChampionMutationType ChampionMutations
        {
            get => (ChampionMutationType)(GetProperty(PropertyInt.ChampionMutationFlags) ?? 0);
            set { if (value != ChampionMutationType.None) SetProperty(PropertyInt.ChampionMutationFlags, (int)value); else RemoveProperty(PropertyInt.ChampionMutationFlags); }
        }

        /// <summary>
        /// Applies champion mutations to this creature.
        /// Called when spawned from a generator with ChampionEnabled.
        /// </summary>
        public void ApplyChampionMutation()
        {
            // Roll the champion tier based on creature's treasure tier
            var tier = ChampionManager.RollChampionTier(WeenieClassId);
            if (tier <= 0)
                tier = 1;

            // Roll for mutations
            var mutations = ChampionManager.RollMutations(tier);

            // Apply the champion status
            IsChampion = true;
            ChampionTier = tier;
            ChampionMutations = mutations;

            // Generate the prefix name
            var prefix = ChampionMutationTypeExtensions.GetChampionPrefix(mutations);
            Name = $"{prefix} {Name}";

            // Apply stat bonuses
            ApplyChampionStatBonuses(tier, mutations);

            // Apply visual effects
            ApplyChampionVisuals();

            // Apply special mutation abilities
            ApplyChampionAbilities(tier, mutations);

            championLog.Debug($"[CHAMPION] Created {Name} (Tier {tier}) with mutations: {mutations}");
        }

        /// <summary>
        /// Applies stat bonuses based on tier and mutations
        /// </summary>
        private void ApplyChampionStatBonuses(int tier, ChampionMutationType mutations)
        {
            // Health bonus (scales from +25% at T1 to +100% at T8)
            var healthMod = ChampionManager.GetHealthBonusMultiplier(tier);
            SetProperty(PropertyFloat.ChampionHealthMod, healthMod);

            // Apply the health multiplier
            // Note: MaxValue = StartingValue + Ranks + AttributeBonus
            // We multiply StartingValue by healthMod, then set Current to the new MaxValue
            if (Health != null)
            {
                var newStartingValue = (uint)(Health.StartingValue * healthMod);
                Health.StartingValue = newStartingValue;
                // After modifying StartingValue, MaxValue is recalculated - set Current to full
                Health.Current = Health.MaxValue;
            }

            // XP/Luminance bonus (same scaling as health)
            var xpMod = ChampionManager.GetXpBonusMultiplier(tier);
            SetProperty(PropertyFloat.ChampionXpMod, xpMod);
            SetProperty(PropertyFloat.ChampionLuminanceMod, xpMod);

            // Loot quality modifier
            var lootMod = ChampionManager.GetLootQualityMod(tier, mutations);
            SetProperty(PropertyFloat.ChampionLootQualityMod, lootMod);

            // Apply damage rating from offensive mutations
            int damageRating = 0;
            for (int i = 0; i < 8; i++)
            {
                if (mutations.HasFlag((ChampionMutationType)(1 << i)))
                {
                    damageRating = i + 1;
                    break;
                }
            }
            if (damageRating > 0)
            {
                SetProperty(PropertyInt.ChampionBonusDamageRating, damageRating);
                var currentDR = GetProperty(PropertyInt.DamageRating) ?? 0;
                SetProperty(PropertyInt.DamageRating, currentDR + damageRating);
            }

            // Apply defense rating from defensive mutations
            int defenseRating = 0;
            for (int i = 0; i < 8; i++)
            {
                if (mutations.HasFlag((ChampionMutationType)(1 << (i + 8))))
                {
                    defenseRating = i + 1;
                    break;
                }
            }
            if (defenseRating > 0)
            {
                SetProperty(PropertyInt.ChampionBonusDefenseRating, defenseRating);
                var currentDefense = GetProperty(PropertyInt.DamageResistRating) ?? 0;
                SetProperty(PropertyInt.DamageResistRating, currentDefense + defenseRating);
            }

            // Apply crit damage rating from critical mutations
            int critRating = 0;
            for (int i = 0; i < 8; i++)
            {
                if (mutations.HasFlag((ChampionMutationType)(1 << (i + 16))))
                {
                    critRating = i + 1;
                    break;
                }
            }
            if (critRating > 0)
            {
                SetProperty(PropertyInt.ChampionBonusCritDamageRating, critRating);
                var currentCrit = GetProperty(PropertyInt.CritDamageRating) ?? 0;
                SetProperty(PropertyInt.CritDamageRating, currentCrit + critRating);
            }
        }

        /// <summary>
        /// Applies visual effects to champions (size increase, aetheria surge)
        /// </summary>
        private void ApplyChampionVisuals()
        {
            // Apply size increase (+15%)
            var currentScale = GetProperty(PropertyFloat.DefaultScale) ?? 1.0f;
            SetProperty(PropertyFloat.DefaultScale, currentScale * ChampionManager.GetSizeScale());

            // Note: Aetheria surge visual effect will need to be applied
            // This may require playing a looping particle effect
            // For now, we'll set the radar blip to a distinctive color
            SetProperty(PropertyInt.RadarBlipColor, (int)ACE.Entity.Enum.RadarColor.Red);
        }

        /// <summary>
        /// Applies special abilities based on mutations and tier
        /// Damage and damage reduction scale with tier
        /// </summary>
        private void ApplyChampionAbilities(int tier, ChampionMutationType mutations)
        {
            // Tempest mutation - grants SpellChain and SplitArrow
            if (mutations.HasFlag(ChampionMutationType.Tempest))
            {
                SetProperty(PropertyBool.SplitArrows, true);
                // Scale targets by tier: T1-T3 = 2, T4-T5 = 3, T6-T8 = 4
                var splitArrowCount = tier <= 3 ? 2 : (tier <= 5 ? 3 : 4);
                SetProperty(PropertyInt.SplitArrowCount, splitArrowCount);
                SetProperty(PropertyInt.SpellChainTargets, splitArrowCount);
                SetProperty(PropertyInt.SpellChainDamagePercent, 100); // Full damage to chain targets
            }

            // Enraged mutation - enable enrage mechanics with tier-scaled values
            if (mutations.HasFlag(ChampionMutationType.Enraged))
            {
                SetProperty(PropertyBool.CanEnrage, true);

                // Enrage threshold scales inversely with tier (higher tier = earlier enrage)
                // T1: 50%, T8: 65%
                var enrageThreshold = 0.5f + (tier - 1) * 0.02f;
                SetProperty(PropertyFloat.EnrageThreshold, enrageThreshold);

                // Damage multiplier scales with tier: T1 = 1.10, T8 = 1.45
                var damageMultiplier = 1.10f + (tier - 1) * 0.05f;
                SetProperty(PropertyFloat.EnrageDamageMultiplier, damageMultiplier);

                // Damage reduction scales with tier: T1 = 5%, T8 = 25%
                var damageReduction = 0.05f + (tier - 1) * 0.028f;
                SetProperty(PropertyFloat.EnrageDamageReduction, damageReduction);

                // === LEAP ATTACK ===
                SetProperty(PropertyBool.EnrageLeapEnabled, true);
                // Leap interval decreases with tier (more frequent): T1 = 35s, T8 = 20s
                var leapInterval = 35.0f - (tier - 1) * 2.0f;
                SetProperty(PropertyFloat.EnrageLeapInterval, leapInterval);
                // Leap damage scales with tier: T1 = 1000, T8 = 4500
                var leapDamage = 1000 + (tier - 1) * 500;
                SetProperty(PropertyInt.EnrageLeapBaseDamage, leapDamage);
                SetProperty(PropertyFloat.EnrageLeapRadius, 5.0f);
                SetProperty(PropertyFloat.EnrageLeapMinRange, 8.0f);
                SetProperty(PropertyFloat.EnrageLeapMaxRange, 30.0f);
                // Warning time decreases with tier: T1 = 4s, T8 = 2.5s
                var warningTime = 4.0f - (tier - 1) * 0.2f;
                SetProperty(PropertyFloat.EnrageLeapWarningTime, warningTime);

                // === MIRROR IMAGE ===
                SetProperty(PropertyBool.EnrageMirrorImageEnabled, true);
                // Mirror spawn interval decreases with tier: T1 = 60s, T8 = 30s
                var mirrorInterval = 60.0f - (tier - 1) * 4.0f;
                SetProperty(PropertyFloat.EnrageMirrorImageInterval, mirrorInterval);
                // Number of clones scales with tier: T1-T2 = 1, T3-T5 = 2, T6-T8 = 3
                var mirrorCount = tier <= 2 ? 1 : (tier <= 5 ? 2 : 3);
                SetProperty(PropertyInt.EnrageMirrorImageCount, mirrorCount);
                SetProperty(PropertyFloat.EnrageMirrorImageSpawnRadius, 5.0f);
                // Clone health % scales with tier: T1 = 15%, T8 = 35%
                var mirrorHealthPercent = 15 + (tier - 1) * 3;
                SetProperty(PropertyInt.EnrageMirrorImageHealthPercent, mirrorHealthPercent);
                // Clone damage % scales with tier: T1 = 20%, T8 = 50%
                var mirrorDamagePercent = 20 + (tier - 1) * 4;
                SetProperty(PropertyInt.EnrageMirrorImageDamagePercent, mirrorDamagePercent);
                // Higher tiers can be immune during clones
                if (tier >= 6)
                    SetProperty(PropertyBool.EnrageMirrorImmuneDuringClones, true);

                // === AOE HOTSPOTS ===
                SetProperty(PropertyBool.CanAOE, true);
                SetProperty(PropertyBool.EnragedHotspot, true);
            }
        }

        /// <summary>
        /// Gets the XP multiplier for champion kills
        /// </summary>
        public float GetChampionXpMultiplier()
        {
            if (!IsChampion)
                return 1.0f;

            return (float)(GetProperty(PropertyFloat.ChampionXpMod) ?? 1.0f);
        }

        /// <summary>
        /// Gets the luminance multiplier for champion kills
        /// </summary>
        public float GetChampionLuminanceMultiplier()
        {
            if (!IsChampion)
                return 1.0f;

            return (float)(GetProperty(PropertyFloat.ChampionLuminanceMod) ?? 1.0f);
        }

        /// <summary>
        /// Gets the loot quality modifier for champions
        /// </summary>
        public float GetChampionLootQualityMod()
        {
            if (!IsChampion)
                return 0.0f;

            return (float)(GetProperty(PropertyFloat.ChampionLootQualityMod) ?? 0.0f);
        }

        /// <summary>
        /// Broadcasts champion spawn message to nearby players
        /// </summary>
        public void AnnounceChampionSpawn()
        {
            if (!IsChampion)
                return;

            var tier = ChampionTier ?? 1;
            var msg = tier >= 6
                ? $"A powerful {Name} has appeared!"
                : $"A {Name} has appeared!";

            // Broadcast to nearby players
            var nearbyPlayers = GetPlayersInRange(100.0);
            foreach (var player in nearbyPlayers)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            }
        }
    }
}

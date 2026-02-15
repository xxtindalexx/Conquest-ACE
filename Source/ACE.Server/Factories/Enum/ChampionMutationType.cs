using System;

namespace ACE.Server.Factories.Enum
{
    /// <summary>
    /// CONQUEST: Champion Mutation System
    /// Bitflags for the types of mutations a champion creature can have.
    /// Champions can have multiple mutations stacked.
    /// </summary>
    [Flags]
    public enum ChampionMutationType
    {
        None = 0,

        // Offensive Mutations - Add damage rating based on tier
        Menacing    = 1 << 0,   // T1: +1 DR
        Fierce      = 1 << 1,   // T2: +2 DR
        Vicious     = 1 << 2,   // T3: +3 DR
        Deadly      = 1 << 3,   // T4: +4 DR
        Ruthless    = 1 << 4,   // T5: +5 DR
        Merciless   = 1 << 5,   // T6: +6 DR
        Brutal      = 1 << 6,   // T7: +7 DR
        Devastating = 1 << 7,   // T8: +8 DR

        // Defensive Mutations - Add defense rating based on tier
        Stalwart    = 1 << 8,   // T1: +1 Defense
        Hardened    = 1 << 9,   // T2: +2 Defense
        Protected   = 1 << 10,  // T3: +3 Defense
        Sturdy      = 1 << 11,  // T4: +4 Defense
        Solid       = 1 << 12,  // T5: +5 Defense
        Fortified   = 1 << 13,  // T6: +6 Defense
        Ironclad    = 1 << 14,  // T7: +7 Defense
        Adamant     = 1 << 15,  // T8: +8 Defense

        // Critical Mutations - Add crit damage rating based on tier
        Sharp       = 1 << 16,  // T1: +1 Crit DR
        Keen        = 1 << 17,  // T2: +2 Crit DR
        Piercing    = 1 << 18,  // T3: +3 Crit DR
        Razor       = 1 << 19,  // T4: +4 Crit DR
        Acute       = 1 << 20,  // T5: +5 Crit DR
        Critical    = 1 << 21,  // T6: +6 Crit DR
        Lethal      = 1 << 22,  // T7: +7 Crit DR
        Relentless  = 1 << 23,  // T8: +8 Crit DR

        // Special Mutations - Unique abilities
        Rich        = 1 << 24,  // Better loot drop chance
        Tempest     = 1 << 25,  // SpellChain/SplitArrow abilities
        Enraged     = 1 << 26,  // Uses enrage mechanics (leap, mirror images)
    }

    public static class ChampionMutationTypeExtensions
    {
        // Offensive mutation names by tier (1-8)
        public static readonly string[] OffensiveNames = { "Menacing", "Fierce", "Vicious", "Deadly", "Ruthless", "Merciless", "Brutal", "Devastating" };

        // Defensive mutation names by tier (1-8)
        public static readonly string[] DefensiveNames = { "Stalwart", "Hardened", "Protected", "Sturdy", "Solid", "Fortified", "Ironclad", "Adamant" };

        // Critical mutation names by tier (1-8)
        public static readonly string[] CriticalNames = { "Sharp", "Keen", "Piercing", "Razor", "Acute", "Critical", "Lethal", "Relentless" };

        /// <summary>
        /// Gets the offensive mutation flag for a given tier (1-8)
        /// </summary>
        public static ChampionMutationType GetOffensiveMutation(int tier)
        {
            if (tier < 1 || tier > 8) return ChampionMutationType.None;
            return (ChampionMutationType)(1 << (tier - 1));
        }

        /// <summary>
        /// Gets the defensive mutation flag for a given tier (1-8)
        /// </summary>
        public static ChampionMutationType GetDefensiveMutation(int tier)
        {
            if (tier < 1 || tier > 8) return ChampionMutationType.None;
            return (ChampionMutationType)(1 << (tier - 1 + 8));
        }

        /// <summary>
        /// Gets the critical mutation flag for a given tier (1-8)
        /// </summary>
        public static ChampionMutationType GetCriticalMutation(int tier)
        {
            if (tier < 1 || tier > 8) return ChampionMutationType.None;
            return (ChampionMutationType)(1 << (tier - 1 + 16));
        }

        /// <summary>
        /// Gets the prefix name for a champion based on its mutations
        /// </summary>
        public static string GetChampionPrefix(ChampionMutationType mutations)
        {
            var prefixes = new System.Collections.Generic.List<string>();

            // Check offensive mutations (highest tier first)
            for (int i = 7; i >= 0; i--)
            {
                if (mutations.HasFlag((ChampionMutationType)(1 << i)))
                {
                    prefixes.Add(OffensiveNames[i]);
                    break;
                }
            }

            // Check defensive mutations (highest tier first)
            for (int i = 7; i >= 0; i--)
            {
                if (mutations.HasFlag((ChampionMutationType)(1 << (i + 8))))
                {
                    prefixes.Add(DefensiveNames[i]);
                    break;
                }
            }

            // Check critical mutations (highest tier first)
            for (int i = 7; i >= 0; i--)
            {
                if (mutations.HasFlag((ChampionMutationType)(1 << (i + 16))))
                {
                    prefixes.Add(CriticalNames[i]);
                    break;
                }
            }

            // Check special mutations
            if (mutations.HasFlag(ChampionMutationType.Rich))
                prefixes.Add("Rich");
            if (mutations.HasFlag(ChampionMutationType.Tempest))
                prefixes.Add("Tempest");
            if (mutations.HasFlag(ChampionMutationType.Enraged))
                prefixes.Add("Enraged");

            return prefixes.Count > 0 ? string.Join(" ", prefixes) : "Champion";
        }
    }
}

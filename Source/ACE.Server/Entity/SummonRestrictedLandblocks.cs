using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    /// <summary>
    /// CONQUEST: Combat pet landblock restriction list and property overrides
    /// </summary>
    public static class SummonRestrictedLandblocks
    {
        private static HashSet<ushort> _restrictedLandblocks;
        private static Dictionary<ushort, string> _restrictedLandblockLabels;
        private static string _cachedPropertyString = string.Empty;

        private static void EnsureLoaded()
        {
            var propString = PropertyManager.GetString("summon_restricted_landblocks") ?? string.Empty;

            if (_restrictedLandblocks != null && propString.Equals(_cachedPropertyString))
                return;

            _cachedPropertyString = propString;
            _restrictedLandblocks = new HashSet<ushort>();
            _restrictedLandblockLabels = new Dictionary<ushort, string>();

            if (string.IsNullOrWhiteSpace(propString))
                return;

            foreach (var entry in propString.Split(','))
            {
                var trimmed = entry.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                string label = null;
                var colonIndex = trimmed.IndexOf(':');
                var landblockPart = trimmed;

                if (colonIndex >= 0)
                {
                    landblockPart = trimmed.Substring(0, colonIndex).Trim();
                    label = trimmed.Substring(colonIndex + 1).Trim();
                }

                if (!TryParseLandblock(landblockPart, out ushort landblock))
                    continue;

                _restrictedLandblocks.Add(landblock);

                if (!string.IsNullOrEmpty(label))
                    _restrictedLandblockLabels[landblock] = label;
            }
        }

        /// <summary>
        /// CONQUEST: Parses a landblock ID from hex (0x0066) or decimal format
        /// </summary>
        public static bool TryParseLandblock(string input, out ushort landblock)
        {
            landblock = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ushort.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out landblock);

            return ushort.TryParse(input, out landblock);
        }

        /// <summary>
        /// CONQUEST: Returns true if the landblock is in the restricted list
        /// </summary>
        public static bool IsRestricted(ushort landblock)
        {
            EnsureLoaded();
            return _restrictedLandblocks.Contains(landblock);
        }

        /// <summary>
        /// CONQUEST: Returns false if combat pet summoning is blocked on this landblock
        /// </summary>
        public static bool CanSummonCombatPet(ushort landblock)
        {
            if (!IsRestricted(landblock))
                return true;

            return !PropertyManager.GetBool("summon_combat_pet_block_in_restricted_landblocks");
        }

        /// <summary>
        /// CONQUEST: Returns display name for a restricted landblock (admin label, DB name, or unnamed)
        /// </summary>
        public static string GetDisplayName(ushort landblock)
        {
            EnsureLoaded();

            if (_restrictedLandblockLabels.TryGetValue(landblock, out var label) && !string.IsNullOrEmpty(label))
                return label;

            var dbName = Landblock.GetLandblockName(landblock);
            if (!string.IsNullOrEmpty(dbName))
                return dbName;

            return "(unnamed)";
        }

        /// <summary>
        /// CONQUEST: Returns all restricted landblocks sorted for admin display
        /// </summary>
        public static IReadOnlyList<ushort> GetRestrictedLandblocksSorted()
        {
            EnsureLoaded();
            return _restrictedLandblocks.OrderBy(x => x).ToList();
        }

        /// <summary>
        /// CONQUEST: Adds a landblock to the restricted list and persists to server props
        /// </summary>
        public static bool AddLandblock(ushort landblock, string label)
        {
            EnsureLoaded();

            if (_restrictedLandblocks.Contains(landblock))
                return false;

            _restrictedLandblocks.Add(landblock);

            if (!string.IsNullOrWhiteSpace(label))
                _restrictedLandblockLabels[landblock] = label.Trim();

            return Persist();
        }

        /// <summary>
        /// CONQUEST: Removes a landblock from the restricted list and persists to server props
        /// </summary>
        public static bool RemoveLandblock(ushort landblock)
        {
            EnsureLoaded();

            if (!_restrictedLandblocks.Remove(landblock))
                return false;

            _restrictedLandblockLabels.Remove(landblock);
            return Persist();
        }

        private static bool Persist()
        {
            var serialized = SerializeToPropertyString();
            _cachedPropertyString = serialized;
            return PropertyManager.ModifyString("summon_restricted_landblocks", serialized);
        }

        /// <summary>
        /// CONQUEST: Serializes the restricted landblock list to the server property CSV format
        /// </summary>
        public static string SerializeToPropertyString()
        {
            EnsureLoaded();

            if (_restrictedLandblocks.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var lb in _restrictedLandblocks.OrderBy(x => x))
            {
                if (sb.Length > 0)
                    sb.Append(',');

                sb.Append($"0x{lb:X4}");

                if (_restrictedLandblockLabels.TryGetValue(lb, out var label) && !string.IsNullOrEmpty(label))
                    sb.Append(':').Append(label);
            }

            return sb.ToString();
        }

        /// <summary>
        /// CONQUEST: Applies or restores combat pet property overrides based on landblock
        /// </summary>
        public static void ApplyCombatPetRestrictions(CombatPet pet, ushort landblock)
        {
            if (pet == null)
                return;

            if (IsRestricted(landblock))
            {
                if (PropertyManager.GetBool("summon_combat_pet_block_in_restricted_landblocks"))
                    return;

                pet.VisualAwarenessRange = PropertyManager.GetDouble("summon_combat_pet_visual_awareness_range", 5);
                pet.Ethereal = PropertyManager.GetBool("summon_combat_pet_ethereal", false);
            }
            else
            {
                pet.Ethereal = true;
                pet.RemoveProperty(PropertyFloat.VisualAwarenessRange);
            }

            pet.ResetAwarenessRangeCache();
        }
    }
}

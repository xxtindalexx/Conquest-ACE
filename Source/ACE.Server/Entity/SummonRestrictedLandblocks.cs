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
        private static HashSet<(ushort Landblock, int? Variation)> _restrictedEntries;
        private static Dictionary<(ushort Landblock, int? Variation), string> _restrictedLabels;
        private static string _cachedPropertyString = string.Empty;

        private static void EnsureLoaded()
        {
            var propString = PropertyManager.GetString("summon_restricted_landblocks") ?? string.Empty;

            if (_restrictedEntries != null && propString.Equals(_cachedPropertyString))
                return;

            LoadFromPropertyString(propString);
        }

        private static void LoadFromPropertyString(string propString)
        {
            _cachedPropertyString = propString ?? string.Empty;
            _restrictedEntries = new HashSet<(ushort, int?)>();
            _restrictedLabels = new Dictionary<(ushort, int?), string>();

            if (string.IsNullOrWhiteSpace(propString))
                return;

            foreach (var entry in propString.Split(','))
            {
                if (!TryParseSerializedEntry(entry, out var key, out var label))
                    continue;

                _restrictedEntries.Add(key);

                if (!string.IsNullOrEmpty(label))
                    _restrictedLabels[key] = label;
            }
        }

        /// <summary>
        /// CONQUEST: Parses a single CSV entry (e.g. 0x0066:Label or 0x0066@2:Label)
        /// </summary>
        public static bool TryParseSerializedEntry(string entry, out (ushort Landblock, int? Variation) key, out string label)
        {
            key = default;
            label = null;

            var trimmed = entry?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return false;

            var colonIndex = trimmed.IndexOf(':');
            var landblockPart = trimmed;

            if (colonIndex >= 0)
            {
                landblockPart = trimmed.Substring(0, colonIndex).Trim();
                label = trimmed.Substring(colonIndex + 1).Trim();
            }

            int? variation = null;
            var atIndex = landblockPart.IndexOf('@');
            if (atIndex >= 0)
            {
                var variationPart = landblockPart.Substring(atIndex + 1).Trim();
                landblockPart = landblockPart.Substring(0, atIndex).Trim();

                if (!int.TryParse(variationPart, out var parsedVariation))
                    return false;

                variation = parsedVariation;
            }

            if (!TryParseLandblock(landblockPart, out ushort landblock))
                return false;

            key = (landblock, variation);
            return true;
        }

        /// <summary>
        /// CONQUEST: Serializes a single entry to the server property CSV format
        /// </summary>
        public static string FormatSerializedEntry(ushort landblock, int? variation, string label)
        {
            var sb = new StringBuilder();
            sb.Append($"0x{landblock:X4}");

            if (variation.HasValue)
                sb.Append('@').Append(variation.Value);

            if (!string.IsNullOrEmpty(label))
                sb.Append(':').Append(label);

            return sb.ToString();
        }

        /// <summary>
        /// CONQUEST: Returns variant suffix for admin display — (all) or vN
        /// </summary>
        public static string FormatVariantSuffix(int? variation)
        {
            return variation.HasValue ? $" v{variation.Value}" : " (all)";
        }

        /// <summary>
        /// CONQUEST: Returns variant suffix for check command output
        /// </summary>
        public static string FormatCheckVariantSuffix(int variation, bool variationSpecified)
        {
            return variationSpecified ? FormatVariantSuffix(variation) : " (checking v0)";
        }

        /// <summary>
        /// CONQUEST: Runtime match — restricted if all-variants entry or exact variant entry exists
        /// </summary>
        public static bool IsRestrictedForVariation(IReadOnlyCollection<(ushort Landblock, int? Variation)> entries, ushort landblock, int variation)
        {
            if (entries == null || entries.Count == 0)
                return false;

            return entries.Contains((landblock, null)) || entries.Contains((landblock, variation));
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
        /// CONQUEST: Returns true if the exact landblock + optional variant entry exists
        /// </summary>
        public static bool ContainsEntry(ushort landblock, int? variation)
        {
            EnsureLoaded();
            return _restrictedEntries.Contains((landblock, variation));
        }

        /// <summary>
        /// CONQUEST: Returns true if combat pets are restricted on this landblock + variant
        /// </summary>
        public static bool IsRestricted(ushort landblock, int variation)
        {
            EnsureLoaded();
            return IsRestrictedForVariation(_restrictedEntries, landblock, variation);
        }

        /// <summary>
        /// CONQUEST: Returns false if combat pet summoning is blocked on this landblock + variant
        /// </summary>
        public static bool CanSummonCombatPet(ushort landblock, int variation)
        {
            if (!IsRestricted(landblock, variation))
                return true;

            return !PropertyManager.GetBool("summon_combat_pet_block_in_restricted_landblocks");
        }

        /// <summary>
        /// CONQUEST: Returns display name for a restricted entry (admin label, DB name, or unnamed)
        /// </summary>
        public static string GetDisplayName(ushort landblock, int? variation)
        {
            EnsureLoaded();

            if (_restrictedLabels.TryGetValue((landblock, variation), out var label) && !string.IsNullOrEmpty(label))
                return label;

            var dbName = Landblock.GetLandblockName(landblock);
            if (!string.IsNullOrEmpty(dbName))
                return dbName;

            return "(unnamed)";
        }

        /// <summary>
        /// CONQUEST: Returns all restricted entries sorted for admin display
        /// </summary>
        public static IReadOnlyList<(ushort Landblock, int? Variation)> GetRestrictedLandblocksSorted()
        {
            EnsureLoaded();
            return _restrictedEntries
                .OrderBy(x => x.Landblock)
                .ThenBy(x => x.Variation.HasValue ? 1 : 0)
                .ThenBy(x => x.Variation ?? -1)
                .ToList();
        }

        /// <summary>
        /// CONQUEST: Adds a landblock entry and persists to server props
        /// </summary>
        public static bool AddLandblock(ushort landblock, int? variation, string label)
        {
            EnsureLoaded();

            var key = (landblock, variation);

            if (_restrictedEntries.Contains(key))
                return false;

            _restrictedEntries.Add(key);

            if (!string.IsNullOrWhiteSpace(label))
                _restrictedLabels[key] = label.Trim();

            return Persist();
        }

        /// <summary>
        /// CONQUEST: Removes a landblock entry and persists to server props
        /// </summary>
        public static bool RemoveLandblock(ushort landblock, int? variation)
        {
            EnsureLoaded();

            var key = (landblock, variation);

            if (!_restrictedEntries.Remove(key))
                return false;

            _restrictedLabels.Remove(key);
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

            if (_restrictedEntries.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var entry in GetRestrictedLandblocksSorted())
            {
                if (sb.Length > 0)
                    sb.Append(',');

                _restrictedLabels.TryGetValue(entry, out var label);
                sb.Append(FormatSerializedEntry(entry.Landblock, entry.Variation, label));
            }

            return sb.ToString();
        }

        /// <summary>
        /// CONQUEST: Applies or restores combat pet property overrides based on landblock + variant
        /// </summary>
        public static void ApplyCombatPetRestrictions(CombatPet pet, ushort landblock, int variation)
        {
            if (pet == null)
                return;

            if (IsRestricted(landblock, variation))
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

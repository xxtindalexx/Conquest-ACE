using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ACE.Server.Managers;

namespace ACE.Server.Entity
{
    /// <summary>
    /// CONQUEST: Landblocks where custom luminance augmentations are temporarily disabled (raid balancing)
    /// </summary>
    public static class AugDisabledLandblocks
    {
        private const string PropertyName = "aug_disabled_landblocks";

        private static HashSet<(ushort Landblock, int? Variation)> _entries;
        private static Dictionary<(ushort Landblock, int? Variation), string> _labels;
        private static string _cachedPropertyString = string.Empty;

        private static void EnsureLoaded()
        {
            var propString = PropertyManager.GetString(PropertyName) ?? string.Empty;

            if (_entries != null && propString.Equals(_cachedPropertyString))
                return;

            LoadFromPropertyString(propString);
        }

        private static void LoadFromPropertyString(string propString)
        {
            _cachedPropertyString = propString ?? string.Empty;
            _entries = new HashSet<(ushort, int?)>();
            _labels = new Dictionary<(ushort, int?), string>();

            if (string.IsNullOrWhiteSpace(propString))
                return;

            foreach (var entry in propString.Split(','))
            {
                if (!SummonRestrictedLandblocks.TryParseSerializedEntry(entry, out var key, out var label))
                    continue;

                _entries.Add(key);

                if (!string.IsNullOrEmpty(label))
                    _labels[key] = label;
            }
        }

        /// <summary>
        /// CONQUEST: Returns true if custom augs are disabled on this landblock + variant
        /// </summary>
        public static bool IsRestricted(ushort landblock, int variation)
        {
            EnsureLoaded();
            return SummonRestrictedLandblocks.IsRestrictedForVariation(_entries, landblock, variation);
        }

        /// <summary>
        /// CONQUEST: Returns true if the exact landblock + optional variant entry exists
        /// </summary>
        public static bool ContainsEntry(ushort landblock, int? variation)
        {
            EnsureLoaded();
            return _entries.Contains((landblock, variation));
        }

        /// <summary>
        /// CONQUEST: Returns display name for an entry (admin label, DB name, or unnamed)
        /// </summary>
        public static string GetDisplayName(ushort landblock, int? variation)
        {
            EnsureLoaded();

            if (_labels.TryGetValue((landblock, variation), out var label) && !string.IsNullOrEmpty(label))
                return label;

            var dbName = Landblock.GetLandblockName(landblock);
            if (!string.IsNullOrEmpty(dbName))
                return dbName;

            return "(unnamed)";
        }

        /// <summary>
        /// CONQUEST: Returns all entries sorted for admin display
        /// </summary>
        public static IReadOnlyList<(ushort Landblock, int? Variation)> GetLandblocksSorted()
        {
            EnsureLoaded();
            return _entries
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

            if (_entries.Contains(key))
                return false;

            _entries.Add(key);

            if (!string.IsNullOrWhiteSpace(label))
                _labels[key] = label.Trim();

            return Persist();
        }

        /// <summary>
        /// CONQUEST: Removes a landblock entry and persists to server props
        /// </summary>
        public static bool RemoveLandblock(ushort landblock, int? variation)
        {
            EnsureLoaded();

            var key = (landblock, variation);

            if (!_entries.Remove(key))
                return false;

            _labels.Remove(key);
            return Persist();
        }

        private static bool Persist()
        {
            var serialized = SerializeToPropertyString();
            _cachedPropertyString = serialized;
            return PropertyManager.ModifyString(PropertyName, serialized);
        }

        private static string SerializeToPropertyString()
        {
            EnsureLoaded();

            if (_entries.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var entry in GetLandblocksSorted())
            {
                if (sb.Length > 0)
                    sb.Append(',');

                _labels.TryGetValue(entry, out var label);
                sb.Append(SummonRestrictedLandblocks.FormatSerializedEntry(entry.Landblock, entry.Variation, label));
            }

            return sb.ToString();
        }
    }
}

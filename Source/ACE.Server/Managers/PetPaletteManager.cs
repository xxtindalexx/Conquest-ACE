using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Managers
{
    /// <summary>
    /// CONQUEST: Holds palette template and its associated palette set (base)
    /// </summary>
    public struct PaletteEntry
    {
        public uint PaletteTemplate;
        public uint PaletteSet;  // The paletteSet/PaletteBase to use with this template

        public PaletteEntry(uint template, uint paletteSet)
        {
            PaletteTemplate = template;
            PaletteSet = paletteSet;
        }
    }

    /// <summary>
    /// CONQUEST: Manages pet palette options for randomizing pet appearances
    /// Combines DAT file palettes, custom JSON palettes, and database overrides
    /// All cached at startup for zero-lag hatching
    /// </summary>
    public static class PetPaletteManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Cache of valid palette entries by creature WCID (from DAT + custom JSON)
        // Key: creature WCID, Value: list of PaletteEntry (template + paletteSet)
        private static Dictionary<uint, List<PaletteEntry>> _creaturePaletteCache = null;

        // Database-driven palette options (manual overrides)
        private static Dictionary<uint, List<PetPaletteOption>> _dbPaletteCache = null;
        private static List<PetPaletteOption> _globalPaletteOptions = null;

        // Cache of custom ClothingTable from JSON files
        private static Dictionary<uint, ClothingTable> _customClothingCache = null;

        private static readonly object _cacheLock = new object();
        private static bool _initialized = false;

        // JSON options for case-insensitive deserialization (JSON uses camelCase, C# uses PascalCase)
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initialize the pet palette cache - call this at server startup
        /// </summary>
        public static void Initialize()
        {
            lock (_cacheLock)
            {
                if (_initialized)
                    return;

                _creaturePaletteCache = new Dictionary<uint, List<PaletteEntry>>();
                _dbPaletteCache = new Dictionary<uint, List<PetPaletteOption>>();
                _globalPaletteOptions = new List<PetPaletteOption>();
                _customClothingCache = new Dictionary<uint, ClothingTable>();

                try
                {
                    // Step 1: Load custom ClothingBase JSON files from mods folder
                    LoadCustomClothingBases();

                    // Step 2: Scan all pet creatures and build palette cache
                    BuildCreaturePaletteCache();

                    // Step 3: Load database-driven palette options (manual overrides)
                    LoadDatabaseOptions();

                    _initialized = true;

                    var creaturesWithPalettes = _creaturePaletteCache.Count(c => c.Value.Count > 0);
                    var totalPalettes = _creaturePaletteCache.Values.Sum(v => v.Count);
                    var dbSpecific = _dbPaletteCache.Values.Sum(v => v.Count);

                    Console.WriteLine($"[PetPaletteManager] Initialized:");
                    Console.WriteLine($"  - {_customClothingCache.Count} custom ClothingBase JSON files loaded");
                    Console.WriteLine($"  - {creaturesWithPalettes} creatures with {totalPalettes} total palette options (from DAT/JSON)");
                    Console.WriteLine($"  - {dbSpecific} database-specific and {_globalPaletteOptions.Count} global database options");
                }
                catch (Exception ex)
                {
                    log.Error($"[PetPaletteManager] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
                    _creaturePaletteCache = new Dictionary<uint, List<PaletteEntry>>();
                    _dbPaletteCache = new Dictionary<uint, List<PetPaletteOption>>();
                    _globalPaletteOptions = new List<PetPaletteOption>();
                    _customClothingCache = new Dictionary<uint, ClothingTable>();
                }
            }
        }

        /// <summary>
        /// Load custom ClothingBase JSON files from the mods folder
        /// </summary>
        private static void LoadCustomClothingBases()
        {
            var modsDir = ConfigManager.Config.Server.ModsDirectory;
            if (string.IsNullOrEmpty(modsDir))
            {
                Console.WriteLine("[PetPaletteManager] ModsDirectory not configured, skipping custom ClothingBase loading");
                return;
            }

            var clothingDir = Path.Combine(modsDir, "CustomClothingBase", "json");
            if (!Directory.Exists(clothingDir))
            {
                Console.WriteLine($"[PetPaletteManager] Custom ClothingBase directory not found: {clothingDir}");
                return;
            }

            LoadCustomClothingBasesInternal();
        }

        /// <summary>
        /// Build the creature palette cache by scanning all pet creature weenies
        /// </summary>
        private static void BuildCreaturePaletteCache()
        {
            // Get all pet creatures from the MysteryEgg cache or query DB
            var allWeenies = DatabaseManager.World.GetAllWeenies();

            foreach (var weenie in allWeenies)
            {
                // Check if this is a creature with ClothingBase
                var clothingBaseProp = weenie.WeeniePropertiesDID?.FirstOrDefault(p => p.Type == (uint)PropertyDataId.ClothingBase);
                if (clothingBaseProp == null)
                    continue;

                var clothingBaseId = clothingBaseProp.Value;
                var creatureWcid = weenie.ClassId;

                // Get valid palettes for this ClothingBase
                var validPalettes = GetValidPalettesForClothingBase(clothingBaseId);

                if (validPalettes.Count > 0)
                {
                    _creaturePaletteCache[creatureWcid] = validPalettes;
                }
            }
        }

        /// <summary>
        /// Get valid palette entries for a ClothingBase ID
        /// Checks custom JSON first, then falls back to DAT
        /// Returns both PaletteTemplate and its associated PaletteSet
        /// </summary>
        private static List<PaletteEntry> GetValidPalettesForClothingBase(uint clothingBaseId)
        {
            var palettes = new List<PaletteEntry>();

            ClothingTable clothingTable = null;

            // Try custom JSON first
            if (_customClothingCache.TryGetValue(clothingBaseId, out var customTable))
            {
                clothingTable = customTable;
            }
            // Fall back to DAT
            else if (DatManager.PortalDat.AllFiles.ContainsKey(clothingBaseId))
            {
                try
                {
                    clothingTable = DatManager.PortalDat.ReadFromDat<ClothingTable>(clothingBaseId);
                }
                catch (Exception ex)
                {
                    log.Debug($"[PetPaletteManager] Failed to read ClothingBase {clothingBaseId:X8} from DAT: {ex.Message}");
                }
            }

            // Extract palette templates and their associated paletteSets from ClothingSubPalEffects
            if (clothingTable?.ClothingSubPalEffects != null)
            {
                foreach (var kvp in clothingTable.ClothingSubPalEffects)
                {
                    var paletteTemplate = kvp.Key;
                    var effect = kvp.Value;

                    // Get the paletteSet from the first CloSubPalette entry
                    uint paletteSet = 0;
                    if (effect.CloSubPalettes != null && effect.CloSubPalettes.Count > 0)
                    {
                        paletteSet = effect.CloSubPalettes[0].PaletteSet;
                    }

                    palettes.Add(new PaletteEntry(paletteTemplate, paletteSet));
                }
            }

            return palettes;
        }

        /// <summary>
        /// Load database-driven palette options
        /// </summary>
        private static void LoadDatabaseOptions()
        {
            var allOptions = DatabaseManager.World.GetAllPetPaletteOptions();

            foreach (var option in allOptions)
            {
                if (option.PetWcid == 0)
                {
                    _globalPaletteOptions.Add(option);
                }
                else
                {
                    if (!_dbPaletteCache.ContainsKey(option.PetWcid))
                        _dbPaletteCache[option.PetWcid] = new List<PetPaletteOption>();

                    _dbPaletteCache[option.PetWcid].Add(option);
                }
            }
        }

        /// <summary>
        /// Reload custom JSON files and database options
        /// DAT-based entries are static and don't need reloading
        /// </summary>
        public static void Reload()
        {
            if (!_initialized)
            {
                Initialize();
                return;
            }

            lock (_cacheLock)
            {
                // Remember old custom ClothingBase IDs
                var oldCustomIds = _customClothingCache?.Keys.ToHashSet() ?? new HashSet<uint>();

                // Reload custom JSON files
                _customClothingCache = new Dictionary<uint, ClothingTable>();
                LoadCustomClothingBasesInternal();

                // Get new custom ClothingBase IDs
                var newCustomIds = _customClothingCache.Keys.ToHashSet();
                var changedIds = oldCustomIds.Union(newCustomIds).ToHashSet();

                // Update palette entries only for creatures that use changed ClothingBases
                int updatedCreatures = 0;
                if (changedIds.Count > 0 && _creaturePaletteCache != null)
                {
                    var allWeenies = DatabaseManager.World.GetAllWeenies();
                    foreach (var weenie in allWeenies)
                    {
                        var clothingBaseProp = weenie.WeeniePropertiesDID?.FirstOrDefault(p => p.Type == (uint)PropertyDataId.ClothingBase);
                        if (clothingBaseProp == null)
                            continue;

                        if (changedIds.Contains(clothingBaseProp.Value))
                        {
                            var validPalettes = GetValidPalettesForClothingBase(clothingBaseProp.Value);
                            if (validPalettes.Count > 0)
                                _creaturePaletteCache[weenie.ClassId] = validPalettes;
                            else
                                _creaturePaletteCache.Remove(weenie.ClassId);
                            updatedCreatures++;
                        }
                    }
                }

                // Reload database options
                _dbPaletteCache = new Dictionary<uint, List<PetPaletteOption>>();
                _globalPaletteOptions = new List<PetPaletteOption>();
                LoadDatabaseOptions();

                Console.WriteLine($"[PetPaletteManager] Reload complete:");
                Console.WriteLine($"  - {_customClothingCache.Count} custom ClothingBase JSON files loaded");
                Console.WriteLine($"  - {updatedCreatures} creatures updated from {changedIds.Count} changed ClothingBases");
                Console.WriteLine($"  - {_dbPaletteCache.Values.Sum(v => v.Count)} database-specific and {_globalPaletteOptions.Count} global database options");
            }
        }

        /// <summary>
        /// Internal method for loading custom clothing bases (no lock)
        /// </summary>
        private static void LoadCustomClothingBasesInternal()
        {
            var modsDir = ConfigManager.Config.Server.ModsDirectory;
            if (string.IsNullOrEmpty(modsDir))
                return;

            var clothingDir = Path.Combine(modsDir, "CustomClothingBase", "json");
            if (!Directory.Exists(clothingDir))
                return;

            var jsonFiles = Directory.GetFiles(clothingDir, "*.json");
            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    uint clothingBaseId = 0;

                    if (fileName.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        uint.TryParse(fileName.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out clothingBaseId);
                    }
                    else
                    {
                        var hexMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"([0-9A-Fa-f]{8})");
                        if (hexMatch.Success)
                        {
                            uint.TryParse(hexMatch.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out clothingBaseId);
                        }
                    }

                    if (clothingBaseId == 0)
                        continue;

                    var json = File.ReadAllText(filePath);
                    var clothingTable = JsonSerializer.Deserialize<ClothingTable>(json, _jsonOptions);

                    if (clothingTable != null)
                    {
                        _customClothingCache[clothingBaseId] = clothingTable;
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"[PetPaletteManager] Failed to load custom ClothingBase from {filePath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Selects a random palette and applies it to the pet device
        /// Priority: DB-specific > DAT/JSON palettes > DB-global
        /// </summary>
        public static bool ApplyRandomPalette(WorldObject petDevice, uint creatureWcid)
        {
            if (!_initialized)
                Initialize();

            lock (_cacheLock)
            {
                // Priority 1: Database-specific options for this creature
                if (_dbPaletteCache.TryGetValue(creatureWcid, out var dbOptions) && dbOptions.Count > 0)
                {
                    var selected = SelectWeightedRandom(dbOptions);
                    if (selected != null)
                    {
                        ApplyPaletteOptionToDevice(petDevice, selected);
                        return true;
                    }
                }

                // Priority 2: DAT/JSON palettes for this creature
                if (_creaturePaletteCache.TryGetValue(creatureWcid, out var datPalettes) && datPalettes.Count > 0)
                {
                    // Select random palette entry from valid options
                    var selectedEntry = datPalettes[ThreadSafeRandom.Next(0, datPalettes.Count - 1)];

                    // Apply with random shade from global options or default range
                    float shadeMin = 0.0f, shadeMax = 1.0f;
                    if (_globalPaletteOptions.Count > 0)
                    {
                        var shadeOption = _globalPaletteOptions[ThreadSafeRandom.Next(0, _globalPaletteOptions.Count - 1)];
                        shadeMin = shadeOption.ShadeMin;
                        shadeMax = shadeOption.ShadeMax;
                    }

                    var shade = shadeMin + (float)(ThreadSafeRandom.Next(0.0f, 1.0f) * (shadeMax - shadeMin));

                    petDevice.SetProperty(PropertyInt.PaletteTemplate, (int)selectedEntry.PaletteTemplate);
                    petDevice.SetProperty(PropertyFloat.Shade, shade);

                    // Look up the actual Palette ID (0x04) from the PaletteSet (0x0F) using shade
                    if (selectedEntry.PaletteSet != 0)
                    {
                        var paletteId = GetPaletteFromPaletteSet(selectedEntry.PaletteSet, shade);
                        if (paletteId != 0)
                            petDevice.SetProperty(PropertyDataId.PaletteBase, paletteId);
                    }

                    return true;
                }

                // Priority 3: Global database options (shade-only)
                if (_globalPaletteOptions.Count > 0)
                {
                    var selected = SelectWeightedRandom(_globalPaletteOptions);
                    if (selected != null)
                    {
                        ApplyPaletteOptionToDevice(petDevice, selected);
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Selects a random palette option using weighted random selection
        /// </summary>
        private static PetPaletteOption SelectWeightedRandom(List<PetPaletteOption> options)
        {
            if (options == null || options.Count == 0)
                return null;

            var totalWeight = options.Sum(o => o.Weight);
            if (totalWeight <= 0)
                return options[ThreadSafeRandom.Next(0, options.Count - 1)];

            var roll = ThreadSafeRandom.Next(0, totalWeight - 1);
            var cumulative = 0;

            foreach (var option in options)
            {
                cumulative += option.Weight;
                if (roll < cumulative)
                    return option;
            }

            return options[options.Count - 1];
        }

        /// <summary>
        /// Applies a database palette option to a pet device
        /// </summary>
        private static void ApplyPaletteOptionToDevice(WorldObject petDevice, PetPaletteOption option)
        {
            if (option.PaletteTemplate.HasValue)
                petDevice.SetProperty(PropertyInt.PaletteTemplate, option.PaletteTemplate.Value);

            var shade = option.ShadeMin + (float)(ThreadSafeRandom.Next(0.0f, 1.0f) * (option.ShadeMax - option.ShadeMin));
            petDevice.SetProperty(PropertyFloat.Shade, shade);
        }

        /// <summary>
        /// Looks up the actual Palette ID (0x04) from a PaletteSet (0x0F) using shade value
        /// </summary>
        private static uint GetPaletteFromPaletteSet(uint paletteSetId, float shade)
        {
            try
            {
                // Read the PaletteSet from DAT
                if (DatManager.PortalDat.AllFiles.ContainsKey(paletteSetId))
                {
                    var paletteSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(paletteSetId);
                    if (paletteSet != null)
                    {
                        // Use the shade to get the actual Palette ID from the set
                        return paletteSet.GetPaletteID(shade);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"[PetPaletteManager] Failed to read PaletteSet {paletteSetId:X8}: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Applies stored palette data from a pet device to a spawned creature
        /// Builds the actual SubPalette entries from ClothingBase + PaletteTemplate + Shade
        /// </summary>
        public static void ApplyPaletteToCreature(Creature creature, PetDevice petDevice)
        {
            if (creature == null || petDevice == null)
                return;

            var paletteTemplate = petDevice.GetProperty(PropertyInt.PaletteTemplate);
            var shade = petDevice.GetProperty(PropertyFloat.Shade);

            // Set the basic properties
            if (paletteTemplate.HasValue)
                creature.PaletteTemplate = paletteTemplate.Value;
            if (shade.HasValue)
                creature.Shade = shade.Value;

            // Build and apply SubPalette entries from ClothingBase
            if (!creature.ClothingBase.HasValue || !paletteTemplate.HasValue)
                return;

            try
            {
                // Get the ClothingTable for this creature
                ClothingTable clothingTable = null;

                // Try custom JSON first
                if (_customClothingCache != null && _customClothingCache.TryGetValue(creature.ClothingBase.Value, out var customTable))
                {
                    clothingTable = customTable;
                }
                // Fall back to DAT
                else if (DatManager.PortalDat.AllFiles.ContainsKey(creature.ClothingBase.Value))
                {
                    clothingTable = DatManager.PortalDat.ReadFromDat<ClothingTable>(creature.ClothingBase.Value);
                }

                if (clothingTable?.ClothingSubPalEffects == null)
                    return;

                // Get the ClothingSubPalEffect for the selected PaletteTemplate
                if (!clothingTable.ClothingSubPalEffects.TryGetValue((uint)paletteTemplate.Value, out var subPalEffect))
                    return;

                float shadeValue = shade.HasValue ? (float)shade.Value : 0.0f;

                // Build SubPalette entries
                var subPalettes = new List<ACE.Entity.Models.PropertiesPalette>();

                foreach (var cloSubPal in subPalEffect.CloSubPalettes)
                {
                    // Look up the actual Palette ID from the PaletteSet
                    uint paletteId = 0;
                    if (DatManager.PortalDat.AllFiles.ContainsKey(cloSubPal.PaletteSet))
                    {
                        var paletteSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(cloSubPal.PaletteSet);
                        paletteId = paletteSet.GetPaletteID(shadeValue);
                    }

                    if (paletteId == 0)
                        continue;

                    // Add SubPalette entries for each range
                    foreach (var range in cloSubPal.Ranges)
                    {
                        subPalettes.Add(new ACE.Entity.Models.PropertiesPalette
                        {
                            SubPaletteId = paletteId,
                            Offset = (ushort)(range.Offset / 8),
                            Length = (ushort)(range.NumColors / 8)
                        });
                    }
                }

                // Apply the SubPalettes to the creature's Biota
                if (subPalettes.Count > 0)
                {
                    lock (creature.BiotaDatabaseLock)
                    {
                        creature.Biota.PropertiesPalette = subPalettes;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"[PetPaletteManager] Failed to apply palette to creature: {ex.Message}");
            }
        }

        /// <summary>
        /// Get valid palettes for a creature (for admin/debug)
        /// </summary>
        public static List<uint> GetValidPalettes(uint creatureWcid)
        {
            if (!_initialized)
                Initialize();

            lock (_cacheLock)
            {
                if (_creaturePaletteCache.TryGetValue(creatureWcid, out var palettes))
                    return palettes.Select(p => p.PaletteTemplate).ToList();
                return new List<uint>();
            }
        }

        /// <summary>
        /// Get valid palette entries for a creature (for admin/debug)
        /// Returns both PaletteTemplate and PaletteSet
        /// </summary>
        public static List<PaletteEntry> GetValidPaletteEntries(uint creatureWcid)
        {
            if (!_initialized)
                Initialize();

            lock (_cacheLock)
            {
                if (_creaturePaletteCache.TryGetValue(creatureWcid, out var palettes))
                    return new List<PaletteEntry>(palettes);
                return new List<PaletteEntry>();
            }
        }

        /// <summary>
        /// Get cache statistics for admin commands
        /// </summary>
        public static string GetCacheStats()
        {
            if (!_initialized)
                return "Pet palette cache not initialized.";

            lock (_cacheLock)
            {
                var stats = new System.Text.StringBuilder();
                stats.AppendLine("Pet Palette Cache Statistics:");
                stats.AppendLine($"  Custom ClothingBase files: {_customClothingCache.Count}");
                stats.AppendLine($"  Creatures with DAT/JSON palettes: {_creaturePaletteCache.Count(c => c.Value.Count > 0)}");
                stats.AppendLine($"  Total DAT/JSON palette options: {_creaturePaletteCache.Values.Sum(v => v.Count)}");
                stats.AppendLine($"  Database-specific entries: {_dbPaletteCache.Count}");
                stats.AppendLine($"  Database-specific options: {_dbPaletteCache.Values.Sum(v => v.Count)}");
                stats.AppendLine($"  Global database options: {_globalPaletteOptions.Count}");
                return stats.ToString();
            }
        }
    }
}

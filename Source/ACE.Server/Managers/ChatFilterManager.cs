using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Manages automatic chat filtering and gagging for inappropriate language
    /// </summary>
    public static class ChatFilterManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// List of filtered word patterns (compiled regex for performance)
        /// </summary>
        private static List<Regex> FilteredPatterns = new List<Regex>();

        /// <summary>
        /// Raw list of filtered words for admin reference
        /// </summary>
        private static List<string> FilteredWords = new List<string>();

        /// <summary>
        /// Path to the filter words file
        /// </summary>
        private static readonly string FilterFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chatfilter.txt");

        /// <summary>
        /// Initialize the chat filter system
        /// </summary>
        public static void Initialize()
        {
            LoadFilteredWords();
            log.Info($"[ChatFilter] Initialized with {FilteredPatterns.Count} filtered patterns.");
        }

        /// <summary>
        /// Reload the filtered words from disk
        /// </summary>
        public static void Reload()
        {
            LoadFilteredWords();
            log.Info($"[ChatFilter] Reloaded with {FilteredPatterns.Count} filtered patterns.");
        }

        /// <summary>
        /// Load filtered words from the chatfilter.txt file
        /// </summary>
        private static void LoadFilteredWords()
        {
            FilteredPatterns.Clear();
            FilteredWords.Clear();

            if (!File.Exists(FilterFilePath))
            {
                // Create a default file with instructions
                CreateDefaultFilterFile();
            }

            try
            {
                var lines = File.ReadAllLines(FilterFilePath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                        continue;

                    FilteredWords.Add(trimmed);

                    // Create a regex pattern that matches the word with word boundaries
                    // and is case-insensitive
                    try
                    {
                        // Escape regex special characters in the word
                        var escaped = Regex.Escape(trimmed);
                        // Create pattern with word boundaries to avoid partial matches
                        var pattern = new Regex($@"\b{escaped}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        FilteredPatterns.Add(pattern);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"[ChatFilter] Invalid pattern '{trimmed}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ChatFilter] Error loading filter file: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a default filter file with instructions
        /// </summary>
        private static void CreateDefaultFilterFile()
        {
            try
            {
                var content = @"# Chat Filter Word List
# Add one word or phrase per line
# Lines starting with # or // are comments
# Words are matched with word boundaries (case-insensitive)
# Example: adding 'test' will match 'test' but not 'testing'
#
# This file is loaded on server startup
# Use /chatfilter reload command to reload without restart
#
# Add your filtered words below:
";
                File.WriteAllText(FilterFilePath, content);
                log.Info($"[ChatFilter] Created default filter file at: {FilterFilePath}");
            }
            catch (Exception ex)
            {
                log.Error($"[ChatFilter] Error creating default filter file: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a message contains any filtered words
        /// </summary>
        /// <param name="message">The message to check</param>
        /// <returns>True if the message contains filtered content</returns>
        public static bool ContainsFilteredContent(string message)
        {
            if (string.IsNullOrEmpty(message) || FilteredPatterns.Count == 0)
                return false;

            foreach (var pattern in FilteredPatterns)
            {
                if (pattern.IsMatch(message))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get the first matched filtered word in a message (for logging)
        /// </summary>
        /// <param name="message">The message to check</param>
        /// <returns>The matched word or null if no match</returns>
        public static string GetFirstMatchedWord(string message)
        {
            if (string.IsNullOrEmpty(message) || FilteredPatterns.Count == 0)
                return null;

            for (int i = 0; i < FilteredPatterns.Count; i++)
            {
                var match = FilteredPatterns[i].Match(message);
                if (match.Success)
                    return match.Value;
            }

            return null;
        }

        /// <summary>
        /// Process a chat message and apply auto-gag if filtered content is detected
        /// </summary>
        /// <param name="player">The player who sent the message</param>
        /// <param name="message">The message content</param>
        /// <param name="chatType">The type of chat channel</param>
        /// <returns>True if the message was blocked, false if it should be allowed</returns>
        public static bool ProcessMessage(Player player, string message, ChatType chatType)
        {
            // Only filter global channels
            if (chatType != ChatType.Trade && chatType != ChatType.General && chatType != ChatType.LFG && chatType != ChatType.Roleplay && chatType != ChatType.Society)
                return false;

            // Check if filtering is enabled
            if (!PropertyManager.GetBool("chat_filter_enabled"))
                return false;

            // Don't filter admins
            if (player.IsAdmin || player.IsArch || player.IsSentinel)
                return false;

            // Check for filtered content
            if (!ContainsFilteredContent(message))
                return false;

            // Get the matched word for logging
            var matchedWord = GetFirstMatchedWord(message);

            // Apply the gag
            var gagDuration = PropertyManager.GetDouble("chat_filter_gag_duration_seconds");
            ApplyAutoGag(player, gagDuration, matchedWord, chatType);

            return true;
        }

        /// <summary>
        /// Apply an automatic gag to a player for using filtered words
        /// </summary>
        private static void ApplyAutoGag(Player player, double durationSeconds, string matchedWord, ChatType chatType)
        {
            // Set the gag
            player.IsGagged = true;
            player.GagDuration = durationSeconds;

            // Notify the player
            var durationMinutes = (int)(durationSeconds / 60);
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                $"You have been automatically gagged for {durationMinutes} minutes for using inappropriate language in {chatType} chat.",
                ChatMessageType.Broadcast));

            // Log the event
            log.Info($"[ChatFilter] Auto-gagged {player.Name} for {durationMinutes} minutes. Matched word: '{matchedWord}' in {chatType} chat.");

            // Notify admins if configured
            if (PropertyManager.GetBool("chat_filter_gag_notify_admins"))
            {
                var adminMessage = $"[ChatFilter] {player.Name} was auto-gagged for using '{matchedWord}' in {chatType} chat.";
                foreach (var adminPlayer in PlayerManager.GetAllOnline())
                {
                    if (adminPlayer.IsAdmin || adminPlayer.IsArch || adminPlayer.IsSentinel)
                    {
                        adminPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(adminMessage, ChatMessageType.Broadcast));
                    }
                }

                // Send to Discord audit channel
                var discordMessage = $"🔇 [Auto-Gag] | **{player.Name}** was gagged for {durationMinutes} minutes for using `{matchedWord}` in {chatType} chat.";
                DiscordChatManager.SendDiscordMessage("ChatFilter", discordMessage, ConfigManager.Config.Chat.TrackingAuditChannelId);
            }
        }

        /// <summary>
        /// Get the count of filtered patterns
        /// </summary>
        public static int GetFilterCount()
        {
            return FilteredPatterns.Count;
        }

        /// <summary>
        /// Get a copy of the filtered words list (for admin commands)
        /// </summary>
        public static List<string> GetFilteredWords()
        {
            return new List<string>(FilteredWords);
        }

        /// <summary>
        /// Add a word to the filter list and save to file
        /// </summary>
        /// <param name="word">The word to add</param>
        /// <returns>True if added, false if already exists</returns>
        public static bool AddWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;

            word = word.Trim();

            // Check if already exists (case-insensitive)
            if (FilteredWords.Any(w => w.Equals(word, StringComparison.OrdinalIgnoreCase)))
                return false;

            try
            {
                // Add to file
                File.AppendAllText(FilterFilePath, word + Environment.NewLine);

                // Reload to update patterns
                LoadFilteredWords();

                log.Info($"[ChatFilter] Added word: '{word}'");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[ChatFilter] Error adding word '{word}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove a word from the filter list and save to file
        /// </summary>
        /// <param name="word">The word to remove</param>
        /// <returns>True if removed, false if not found</returns>
        public static bool RemoveWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;

            word = word.Trim();

            // Check if exists (case-insensitive)
            var existingWord = FilteredWords.FirstOrDefault(w => w.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (existingWord == null)
                return false;

            try
            {
                // Read all lines, filter out the word, and rewrite
                var lines = File.ReadAllLines(FilterFilePath);
                var newLines = lines.Where(line =>
                {
                    var trimmed = line.Trim();
                    // Keep comments and empty lines
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                        return true;
                    // Remove the matching word (case-insensitive)
                    return !trimmed.Equals(word, StringComparison.OrdinalIgnoreCase);
                }).ToList();

                File.WriteAllLines(FilterFilePath, newLines);

                // Reload to update patterns
                LoadFilteredWords();

                log.Info($"[ChatFilter] Removed word: '{word}'");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[ChatFilter] Error removing word '{word}': {ex.Message}");
                return false;
            }
        }
    }
}

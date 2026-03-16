using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;

namespace ACE.Server.Managers
{
    /// <summary>
    /// CONQUEST: Economy Stats Manager
    /// Tracks luminance and conquest coin generation with source breakdown and unique player counts.
    /// Uses in-memory tracking with periodic DB flush and restart recovery.
    /// </summary>
    public static class EconomyStatsManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Currency types
        public const string CURRENCY_LUMINANCE = "luminance";
        public const string CURRENCY_CONQUEST_COIN = "conquest_coin";

        // Source types
        public const string SOURCE_KILL = "kill";
        public const string SOURCE_QUEST = "quest";
        public const string SOURCE_PASSUP = "passup";
        public const string SOURCE_FELLOWSHIP = "fellowship";
        public const string SOURCE_ALLEGIANCE = "allegiance";
        public const string SOURCE_TREASURE_MAP = "treasure_map";
        public const string SOURCE_BANK_WITHDRAWAL = "bank_withdrawal";
        public const string SOURCE_ADMIN = "admin";
        public const string SOURCE_OTHER = "other";

        /// <summary>
        /// Key for tracking: (Date, CurrencyType, Source)
        /// </summary>
        private class StatsKey : IEquatable<StatsKey>
        {
            public DateTime Date { get; }
            public string CurrencyType { get; }
            public string Source { get; }

            public StatsKey(DateTime date, string currencyType, string source)
            {
                Date = date.Date; // Truncate to day
                CurrencyType = currencyType;
                Source = source;
            }

            public bool Equals(StatsKey other)
            {
                if (other == null) return false;
                return Date == other.Date && CurrencyType == other.CurrencyType && Source == other.Source;
            }

            public override bool Equals(object obj) => Equals(obj as StatsKey);

            public override int GetHashCode() => HashCode.Combine(Date, CurrencyType, Source);
        }

        /// <summary>
        /// In-memory stats accumulator
        /// </summary>
        private class StatsAccumulator
        {
            public long TotalAmount;
            public long TransactionCount;
            public HashSet<uint> UniquePlayerGuids = new HashSet<uint>();
            public readonly object Lock = new object();
        }

        // In-memory tracking
        private static readonly ConcurrentDictionary<StatsKey, StatsAccumulator> _stats = new ConcurrentDictionary<StatsKey, StatsAccumulator>();

        // Flush timer
        private static Timer _flushTimer;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(5);

        // Daily report timer
        private static Timer _dailyReportTimer;
        private static readonly TimeSpan DailyReportCheckInterval = TimeSpan.FromMinutes(1);
        private static DateTime _lastDailyReportDate = DateTime.MinValue;

        // EST timezone for daily reset (handles both Windows and Linux timezone IDs)
        private static readonly TimeZoneInfo EstTimeZone = GetEstTimeZone();

        private static TimeZoneInfo GetEstTimeZone()
        {
            try
            {
                // Try Windows timezone ID first
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fall back to IANA timezone ID for Linux
                return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            }
        }

        // Initialization flag
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize the economy stats manager. Call at server startup after database is ready.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            log.Info("[ECONOMY] Initializing Economy Stats Manager...");

            // Load today's contributor data from DB to rebuild HashSets
            LoadTodaysContributors();

            // Start the periodic flush timer
            _flushTimer = new Timer(FlushToDatabase, null, FlushInterval, FlushInterval);

            // Start the daily report timer (checks every minute for midnight EST)
            _dailyReportTimer = new Timer(CheckDailyReport, null, DailyReportCheckInterval, DailyReportCheckInterval);

            _isInitialized = true;
            log.Info("[ECONOMY] Economy Stats Manager initialized.");
        }

        /// <summary>
        /// Shutdown the manager - flush pending data to database
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized)
                return;

            log.Info("[ECONOMY] Shutting down Economy Stats Manager, flushing data...");

            _flushTimer?.Dispose();
            _flushTimer = null;

            _dailyReportTimer?.Dispose();
            _dailyReportTimer = null;

            // Final flush
            FlushToDatabase(null);

            _isInitialized = false;
            log.Info("[ECONOMY] Economy Stats Manager shut down.");
        }

        /// <summary>
        /// Record a luminance transaction
        /// </summary>
        public static void RecordLuminance(uint playerGuid, long amount, string source)
        {
            if (!_isInitialized || amount <= 0)
                return;

            RecordTransaction(CURRENCY_LUMINANCE, playerGuid, amount, source);
        }

        /// <summary>
        /// Record a conquest coin transaction
        /// </summary>
        public static void RecordConquestCoins(uint playerGuid, int amount, string source)
        {
            if (!_isInitialized || amount <= 0)
                return;

            RecordTransaction(CURRENCY_CONQUEST_COIN, playerGuid, amount, source);
        }

        private static void RecordTransaction(string currencyType, uint playerGuid, long amount, string source)
        {
            var key = new StatsKey(DateTime.UtcNow, currencyType, source);
            var accumulator = _stats.GetOrAdd(key, _ => new StatsAccumulator());

            lock (accumulator.Lock)
            {
                accumulator.TotalAmount += amount;
                accumulator.TransactionCount++;
                accumulator.UniquePlayerGuids.Add(playerGuid);
            }
        }

        /// <summary>
        /// Load today's contributors from the database to rebuild HashSets after restart
        /// </summary>
        private static void LoadTodaysContributors()
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                using (var context = new ShardDbContext())
                {
                    // Load today's stats
                    var existingStats = context.EconomyDailyStats
                        .Where(s => s.StatsDate == today)
                        .ToList();

                    // Load today's contributors
                    var contributors = context.EconomyDailyContributors
                        .Where(c => c.StatsDate == today)
                        .ToList();

                    // Rebuild in-memory structures
                    foreach (var stat in existingStats)
                    {
                        var key = new StatsKey(stat.StatsDate, stat.CurrencyType, stat.Source);
                        var accumulator = _stats.GetOrAdd(key, _ => new StatsAccumulator());

                        lock (accumulator.Lock)
                        {
                            accumulator.TotalAmount = stat.TotalAmount;
                            accumulator.TransactionCount = stat.TransactionCount;

                            // Load the unique player guids for this key
                            var playerGuids = contributors
                                .Where(c => c.CurrencyType == stat.CurrencyType && c.Source == stat.Source)
                                .Select(c => c.PlayerGuid)
                                .ToList();

                            foreach (var guid in playerGuids)
                            {
                                accumulator.UniquePlayerGuids.Add(guid);
                            }
                        }
                    }

                    log.Info($"[ECONOMY] Loaded {existingStats.Count} stat records and {contributors.Count} contributors for today.");
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ECONOMY] Error loading today's contributors: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Flush in-memory stats to the database
        /// </summary>
        private static void FlushToDatabase(object state)
        {
            if (_stats.IsEmpty)
                return;

            try
            {
                var today = DateTime.UtcNow.Date;
                var now = DateTime.UtcNow;

                using (var context = new ShardDbContext())
                {
                    foreach (var kvp in _stats)
                    {
                        var key = kvp.Key;
                        var accumulator = kvp.Value;

                        // Only flush today's data (old data is already persisted)
                        if (key.Date != today)
                            continue;

                        int uniqueCount;
                        long totalAmount;
                        long transactionCount;
                        List<uint> newPlayerGuids;

                        lock (accumulator.Lock)
                        {
                            uniqueCount = accumulator.UniquePlayerGuids.Count;
                            totalAmount = accumulator.TotalAmount;
                            transactionCount = accumulator.TransactionCount;
                            newPlayerGuids = accumulator.UniquePlayerGuids.ToList();
                        }

                        // Upsert the stats record
                        var existingStat = context.EconomyDailyStats
                            .FirstOrDefault(s => s.StatsDate == key.Date &&
                                                 s.CurrencyType == key.CurrencyType &&
                                                 s.Source == key.Source);

                        if (existingStat != null)
                        {
                            existingStat.TotalAmount = totalAmount;
                            existingStat.TransactionCount = transactionCount;
                            existingStat.UniquePlayerCount = uniqueCount;
                            existingStat.LastUpdated = now;
                        }
                        else
                        {
                            context.EconomyDailyStats.Add(new EconomyDailyStats
                            {
                                StatsDate = key.Date,
                                CurrencyType = key.CurrencyType,
                                Source = key.Source,
                                TotalAmount = totalAmount,
                                TransactionCount = transactionCount,
                                UniquePlayerCount = uniqueCount,
                                LastUpdated = now
                            });
                        }

                        // Add any new contributors (ignore duplicates via unique constraint)
                        foreach (var playerGuid in newPlayerGuids)
                        {
                            var exists = context.EconomyDailyContributors
                                .Any(c => c.StatsDate == key.Date &&
                                          c.CurrencyType == key.CurrencyType &&
                                          c.Source == key.Source &&
                                          c.PlayerGuid == playerGuid);

                            if (!exists)
                            {
                                context.EconomyDailyContributors.Add(new EconomyDailyContributors
                                {
                                    StatsDate = key.Date,
                                    CurrencyType = key.CurrencyType,
                                    Source = key.Source,
                                    PlayerGuid = playerGuid
                                });
                            }
                        }
                    }

                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ECONOMY] Error flushing stats to database: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get current stats for display (admin command)
        /// </summary>
        public static Dictionary<string, (long total, long transactions, int uniquePlayers)> GetCurrentStats(string currencyType = null)
        {
            var result = new Dictionary<string, (long, long, int)>();
            var today = DateTime.UtcNow.Date;

            foreach (var kvp in _stats)
            {
                if (kvp.Key.Date != today)
                    continue;

                if (currencyType != null && kvp.Key.CurrencyType != currencyType)
                    continue;

                var displayKey = $"{kvp.Key.CurrencyType}:{kvp.Key.Source}";

                lock (kvp.Value.Lock)
                {
                    result[displayKey] = (kvp.Value.TotalAmount, kvp.Value.TransactionCount, kvp.Value.UniquePlayerGuids.Count);
                }
            }

            return result;
        }

        /// <summary>
        /// Get stats summary for a specific currency type
        /// </summary>
        public static (long totalAmount, long totalTransactions, int totalUniquePlayers) GetCurrencyTotals(string currencyType)
        {
            var today = DateTime.UtcNow.Date;
            long totalAmount = 0;
            long totalTransactions = 0;
            var allPlayers = new HashSet<uint>();

            foreach (var kvp in _stats)
            {
                if (kvp.Key.Date != today || kvp.Key.CurrencyType != currencyType)
                    continue;

                lock (kvp.Value.Lock)
                {
                    totalAmount += kvp.Value.TotalAmount;
                    totalTransactions += kvp.Value.TransactionCount;
                    foreach (var guid in kvp.Value.UniquePlayerGuids)
                        allPlayers.Add(guid);
                }
            }

            return (totalAmount, totalTransactions, allPlayers.Count);
        }

        /// <summary>
        /// Clean up old data (entries older than specified days)
        /// </summary>
        public static void CleanupOldData(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.Date.AddDays(-daysToKeep);

                using (var context = new ShardDbContext())
                {
                    var oldStats = context.EconomyDailyStats.Where(s => s.StatsDate < cutoffDate).ToList();
                    var oldContributors = context.EconomyDailyContributors.Where(c => c.StatsDate < cutoffDate).ToList();

                    context.EconomyDailyStats.RemoveRange(oldStats);
                    context.EconomyDailyContributors.RemoveRange(oldContributors);
                    context.SaveChanges();

                    log.Info($"[ECONOMY] Cleaned up {oldStats.Count} old stat records and {oldContributors.Count} old contributor records.");
                }

                // Also clean up in-memory data for old days
                var keysToRemove = _stats.Keys.Where(k => k.Date < cutoffDate).ToList();
                foreach (var key in keysToRemove)
                {
                    _stats.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ECONOMY] Error cleaning up old data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check if it's time to send the daily report (11:59 PM EST)
        /// </summary>
        private static void CheckDailyReport(object state)
        {
            try
            {
                var estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EstTimeZone);
                var estDate = estNow.Date;

                // Check if we already sent a report today
                if (_lastDailyReportDate == estDate)
                    return;

                // Send report between 11:58 PM and 11:59 PM EST (just before midnight reset)
                if (estNow.Hour == 23 && estNow.Minute >= 58 && estNow.Minute <= 59)
                {
                    log.Info("[ECONOMY] Sending daily economy report to Discord...");
                    SendDailyReportToDiscord(estDate);
                    _lastDailyReportDate = estDate;
                }
            }
            catch (Exception ex)
            {
                log.Error($"[ECONOMY] Error checking daily report: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Send the daily economy stats report to Discord
        /// </summary>
        public static void SendDailyReportToDiscord(DateTime? reportDate = null)
        {
            try
            {
                var trackingChannelId = ConfigManager.Config.Chat.TrackingAuditChannelId;
                if (trackingChannelId == 0 || !ConfigManager.Config.Chat.EnableDiscordConnection)
                {
                    log.Warn("[ECONOMY] TrackingAuditChannelId not configured or Discord not enabled");
                    return;
                }

                // Flush current data to ensure we have the latest
                FlushToDatabase(null);

                var today = reportDate ?? DateTime.UtcNow.Date;
                var sb = new StringBuilder();

                sb.AppendLine("**📊 DAILY ECONOMY REPORT**");
                sb.AppendLine($"Date: {today:yyyy-MM-dd}");
                sb.AppendLine("═══════════════════════════════════");

                // Get Luminance stats
                var lumStats = GetCurrentStats(CURRENCY_LUMINANCE);
                var lumTotals = GetCurrencyTotals(CURRENCY_LUMINANCE);

                sb.AppendLine("\n**💡 LUMINANCE**");
                sb.AppendLine($"Total Generated: {lumTotals.totalAmount:N0}");
                sb.AppendLine($"Total Transactions: {lumTotals.totalTransactions:N0}");
                sb.AppendLine($"Unique Players: {lumTotals.totalUniquePlayers:N0}");

                if (lumStats.Count > 0)
                {
                    sb.AppendLine("\n*By Source:*");
                    foreach (var kvp in lumStats.OrderByDescending(x => x.Value.total))
                    {
                        var source = kvp.Key.Split(':').Last();
                        sb.AppendLine($"  • {source}: {kvp.Value.total:N0} ({kvp.Value.uniquePlayers} players)");
                    }
                }

                // Get Conquest Coin stats
                var coinStats = GetCurrentStats(CURRENCY_CONQUEST_COIN);
                var coinTotals = GetCurrencyTotals(CURRENCY_CONQUEST_COIN);

                sb.AppendLine("\n**🪙 CONQUEST COINS**");
                sb.AppendLine($"Total Generated: {coinTotals.totalAmount:N0}");
                sb.AppendLine($"Total Transactions: {coinTotals.totalTransactions:N0}");
                sb.AppendLine($"Unique Players: {coinTotals.totalUniquePlayers:N0}");

                if (coinStats.Count > 0)
                {
                    sb.AppendLine("\n*By Source:*");
                    foreach (var kvp in coinStats.OrderByDescending(x => x.Value.total))
                    {
                        var source = kvp.Key.Split(':').Last();
                        sb.AppendLine($"  • {source}: {kvp.Value.total:N0} ({kvp.Value.uniquePlayers} players)");
                    }
                }

                sb.AppendLine("\n═══════════════════════════════════");
                sb.AppendLine($"*Report generated at {DateTime.UtcNow:HH:mm:ss} UTC*");

                // Send to Discord
                DiscordChatManager.SendDiscordMessage("[ECONOMY]", sb.ToString(), trackingChannelId);
                log.Info("[ECONOMY] Daily report sent to Discord successfully.");
            }
            catch (Exception ex)
            {
                log.Error($"[ECONOMY] Error sending daily report to Discord: {ex.Message}", ex);
            }
        }
    }
}

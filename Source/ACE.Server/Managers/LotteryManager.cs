using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// CONQUEST: Server-wide weekly luminance lottery.
    ///
    /// Players enter with /lum lottery [count] (1–lottery_max_tickets tickets, each costing lottery_ticket_cost_lum).
    /// Every Sunday at lottery_draw_hour_est (EST) the server picks 3 weighted-random winners who share
    /// lottery_pot_share of the total pot: 1st place gets lottery_first_place_share of the prize pool;
    /// 2nd and 3rd split the rest equally.
    ///
    /// Entries are persisted in biota_properties_int64 using PropertyInt64.LotteryTickets (9062) and
    /// PropertyInt64.LotteryWeekNumber (9063), so no new schema is required and entries survive restarts.
    /// </summary>
    public static class LotteryManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly TimeZoneInfo EstTimeZone = GetEstTimeZone();

        private static TimeZoneInfo GetEstTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            }
        }

        // Timer polls once per minute to check if the draw window has arrived.
        private static Timer _drawTimer;
        private static readonly TimeSpan DrawCheckInterval = TimeSpan.FromMinutes(1);

        // Guards against firing more than once per week even if the timer fires multiple times
        // in the same minute.  Tracks the ISO week+year string of the last completed draw.
        private static volatile string _lastDrawWeekKey = string.Empty;
        private static readonly object _drawLock = new object();

        private static bool _isInitialized;

        // In-memory snapshot of this week's entries; kept current as players enter.
        // characterId → (name, tickets)
        private static readonly ConcurrentDictionary<uint, LotteryEntry> _entries
            = new ConcurrentDictionary<uint, LotteryEntry>();

        // IP → characterId: tracks the first character per IP to enter this week.
        // Used to enforce the one-entry-per-IP rule.
        private static readonly ConcurrentDictionary<string, uint> _ipToCharId
            = new ConcurrentDictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        private struct LotteryEntry
        {
            public string Name;
            public int Tickets;
        }

        // ──────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call once at server startup (after database is ready).
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            log.Info("[LOTTERY] Initializing Luminance Lottery Manager...");

            LoadCurrentWeekEntries();

            _drawTimer = new Timer(CheckDrawWindow, null, DrawCheckInterval, DrawCheckInterval);

            _isInitialized = true;
            log.Info($"[LOTTERY] Lottery Manager initialized. {_entries.Count} participant(s) loaded for current week ({GetCurrentWeekKey()}).");
        }

        public static void Shutdown()
        {
            if (!_isInitialized)
                return;

            _drawTimer?.Dispose();
            _drawTimer = null;
            _isInitialized = false;
            log.Info("[LOTTERY] Lottery Manager shut down.");
        }

        // ──────────────────────────────────────────────────────────────────
        // Player entry
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempt to purchase <paramref name="requestedTickets"/> lottery tickets for the player.
        /// Deducts luminance, persists the entry, and reports the result via chat.
        /// </summary>
        public static void EnterLottery(Player player, int requestedTickets)
        {
            if (!PropertyManager.GetBool("lottery_enabled"))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "[LOTTERY] The lottery is currently disabled.", ChatMessageType.Broadcast));
                return;
            }

            var maxTickets = (int)PropertyManager.GetLong("lottery_max_tickets");
            var ticketCost = PropertyManager.GetLong("lottery_ticket_cost_lum");

            if (requestedTickets < 1 || requestedTickets > maxTickets)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"[LOTTERY] You must request between 1 and {maxTickets} ticket(s).", ChatMessageType.Broadcast));
                return;
            }

            // Must be luminance-flagged (has earned luminance through gameplay)
            if (!player.MaximumLuminance.HasValue || player.MaximumLuminance == 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "[LOTTERY] You must be luminance-flagged to enter the lottery.", ChatMessageType.Broadcast));
                return;
            }

            var currentWeekNum = GetCurrentWeekNumber();
            var characterId = player.Guid.Full;

            // One entry per IP address per week (anti-multibox)
            var playerIp = GetPlayerIp(player.Session);
            if (playerIp != null)
            {
                if (_ipToCharId.TryGetValue(playerIp, out var existingCharId) && existingCharId != characterId)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        "[LOTTERY] Another character from your connection has already entered this week's lottery. Only one entry per connection is allowed.",
                        ChatMessageType.Broadcast));
                    return;
                }
            }

            // How many tickets does this player already hold for the current week?
            var storedWeek = (int)(player.GetProperty(PropertyInt64.LotteryWeekNumber) ?? 0);
            var alreadyHeld = storedWeek == currentWeekNum
                ? (int)(player.GetProperty(PropertyInt64.LotteryTickets) ?? 0)
                : 0;

            var canBuy = maxTickets - alreadyHeld;
            if (canBuy <= 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"[LOTTERY] You already hold the maximum of {maxTickets} ticket(s) for this week.", ChatMessageType.Broadcast));
                return;
            }

            var toBuy = Math.Min(requestedTickets, canBuy);
            var totalCost = ticketCost * toBuy;

            var totalLum = (player.BankedLuminance ?? 0) + (player.AvailableLuminance ?? 0);
            if (totalLum < totalCost)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"[LOTTERY] You need {totalCost:N0} luminance for {toBuy} ticket(s) but only have {totalLum:N0}.",
                    ChatMessageType.Broadcast));
                return;
            }

            if (!player.SpendLuminance(totalCost))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "[LOTTERY] Failed to deduct luminance. Please try again.", ChatMessageType.Broadcast));
                return;
            }

            var newTotal = alreadyHeld + toBuy;
            player.SetProperty(PropertyInt64.LotteryTickets, newTotal);
            player.SetProperty(PropertyInt64.LotteryWeekNumber, currentWeekNum);

            _entries[characterId] = new LotteryEntry { Name = player.Name, Tickets = newTotal };

            // Lock in this IP for the week (no-op if already registered)
            if (playerIp != null)
                _ipToCharId.TryAdd(playerIp, characterId);

            var drawTime = NextDrawTime();
            var drawStr = TimeZoneInfo.ConvertTimeFromUtc(drawTime, EstTimeZone).ToString("dddd, MMMM d 'at' h:mm tt 'EST'");

            player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                $"[LOTTERY] You purchased {toBuy} ticket(s) for {totalCost:N0} luminance. " +
                $"You now hold {newTotal}/{maxTickets} ticket(s) this week. " +
                $"Draw: {drawStr}.",
                ChatMessageType.Broadcast));

            log.Info($"[LOTTERY] {player.Name} (0x{characterId:X8}) purchased {toBuy} ticket(s) (total {newTotal}) for {totalCost:N0} lum.");
        }

        // ──────────────────────────────────────────────────────────────────
        // Status query (used by /lum status)
        // ──────────────────────────────────────────────────────────────────

        public static void SendStatusToPlayer(Player player)
        {
            if (!PropertyManager.GetBool("lottery_enabled"))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "[LOTTERY] The lottery is currently disabled.", ChatMessageType.Broadcast));
                return;
            }

            var maxTickets = (int)PropertyManager.GetLong("lottery_max_tickets");
            var ticketCost = PropertyManager.GetLong("lottery_ticket_cost_lum");
            var potShare = PropertyManager.GetDouble("lottery_pot_share");
            var firstShare = PropertyManager.GetDouble("lottery_first_place_share");

            var currentWeekNum = GetCurrentWeekNumber();
            var storedWeek = (int)(player.GetProperty(PropertyInt64.LotteryWeekNumber) ?? 0);
            var myTickets = storedWeek == currentWeekNum
                ? (int)(player.GetProperty(PropertyInt64.LotteryTickets) ?? 0)
                : 0;

            long totalTickets = _entries.Values.Sum(e => e.Tickets);
            long totalCollected = totalTickets * ticketCost;
            long prizePool = (long)(totalCollected * potShare);
            long first = (long)(prizePool * firstShare);
            long runnerUp = (long)(prizePool * (1.0 - firstShare) / 2.0);

            var drawTime = NextDrawTime();
            var estDraw = TimeZoneInfo.ConvertTimeFromUtc(drawTime, EstTimeZone);
            var timeUntil = drawTime - DateTime.UtcNow;
            var timeStr = timeUntil.TotalDays >= 1
                ? $"{(int)timeUntil.TotalDays}d {timeUntil.Hours}h {timeUntil.Minutes}m"
                : $"{timeUntil.Hours}h {timeUntil.Minutes}m";

            var sb = new StringBuilder();
            sb.AppendLine("------ Luminance Lottery ------");
            sb.AppendLine($"Draw: {estDraw:ddd MMM d 'at' h:mm tt 'EST'} (in {timeStr})");
            sb.AppendLine($"Ticket cost: {ticketCost:N0} lum   Max: {maxTickets}/player");
            sb.AppendLine($"Participants: {_entries.Count}   Total tickets: {totalTickets:N0}");
            sb.AppendLine($"Prize pool: {prizePool:N0} lum  (1st: {first:N0}  2nd/3rd: {runnerUp:N0} ea.)");
            sb.AppendLine($"Your tickets this week: {myTickets}/{maxTickets}");

            foreach (var line in sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(line.TrimEnd(), ChatMessageType.Broadcast));
        }

        // ──────────────────────────────────────────────────────────────────
        // Draw logic
        // ──────────────────────────────────────────────────────────────────

        private static void CheckDrawWindow(object state)
        {
            try
            {
                var estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EstTimeZone);

                // Only run on the configured day (0 = Sunday) at the configured hour (within the same minute)
                var drawDayOfWeek = (DayOfWeek)(int)Math.Max(0, Math.Min(6, PropertyManager.GetLong("lottery_draw_day_of_week")));
                var drawHour = (int)Math.Max(0, Math.Min(23, PropertyManager.GetLong("lottery_draw_hour_est")));

                if (estNow.DayOfWeek != drawDayOfWeek || estNow.Hour != drawHour)
                    return;

                var weekKey = GetCurrentWeekKey();

                lock (_drawLock)
                {
                    if (_lastDrawWeekKey == weekKey)
                        return;

                    _lastDrawWeekKey = weekKey;
                }

                TryRunWeeklyDraw();
            }
            catch (Exception ex)
            {
                log.Error($"[LOTTERY] Error in draw window check: {ex.Message}", ex);
            }
        }

        private static void TryRunWeeklyDraw()
        {
            log.Info("[LOTTERY] Running weekly draw...");

            if (!PropertyManager.GetBool("lottery_enabled"))
            {
                log.Info("[LOTTERY] Lottery is disabled; skipping draw.");
                return;
            }

            // Reload from DB in case some entries were made by offline-then-online players
            // that the in-memory dict might have missed after a restart mid-week.
            LoadCurrentWeekEntries();

            if (_entries.IsEmpty)
            {
                BroadcastSystemMessage("[LOTTERY] This week's lottery draw has run — no tickets were sold. Better luck next week!");
                log.Info("[LOTTERY] Draw ran with no participants.");
                return;
            }

            var ticketCost = PropertyManager.GetLong("lottery_ticket_cost_lum");
            var potShare = PropertyManager.GetDouble("lottery_pot_share");
            var firstShare = PropertyManager.GetDouble("lottery_first_place_share");

            long totalTickets = _entries.Values.Sum(e => e.Tickets);
            long totalCollected = totalTickets * ticketCost;
            long prizePool = (long)(totalCollected * potShare);
            long firstPrize = (long)(prizePool * firstShare);
            long runnerUpPrize = (long)(prizePool * (1.0 - firstShare) / 2.0);

            // Build weighted pool: one entry per ticket
            var pool = new List<uint>();
            foreach (var kvp in _entries)
                for (int i = 0; i < kvp.Value.Tickets; i++)
                    pool.Add(kvp.Key);

            // Fisher-Yates partial shuffle to pick 3 distinct winners
            var winners = new List<(uint id, string name, long prize, int place)>();
            var usedIds = new HashSet<uint>();
            int needed = Math.Min(3, _entries.Count);

            for (int i = 0; i < needed; i++)
            {
                // Pick from remaining pool entries that haven't won yet
                var eligible = pool.Where(id => !usedIds.Contains(id)).ToList();
                if (eligible.Count == 0)
                    break;

                var idx = ThreadSafeRandom.Next(0, eligible.Count - 1);
                var winnerId = eligible[idx];
                usedIds.Add(winnerId);

                var winnerName = _entries.TryGetValue(winnerId, out var entry) ? entry.Name : $"0x{winnerId:X8}";
                var prize = i == 0 ? firstPrize : runnerUpPrize;
                var place = i + 1;

                winners.Add((winnerId, winnerName, prize, place));
                AwardLuminance(winnerId, winnerName, prize);
            }

            // Clear all entries for this week from DB and in-memory dict
            ClearCurrentWeekEntries();
            _entries.Clear();

            // Announce results
            var sb = new StringBuilder();
            sb.AppendLine($"[LOTTERY] This week's draw is complete! {totalTickets:N0} tickets were sold, {totalCollected:N0} lum collected.");
            sb.AppendLine($"Prize pool: {prizePool:N0} lum (1st: {firstPrize:N0}  2nd/3rd: {runnerUpPrize:N0} ea.)");
            foreach (var (_, name, prize, place) in winners)
            {
                var placeStr = place == 1 ? "1st" : place == 2 ? "2nd" : "3rd";
                sb.AppendLine($"  {placeStr} place: {name} — {prize:N0} lum!");
            }
            if (winners.Count == 0)
                sb.AppendLine("  No winners were selected.");

            BroadcastSystemMessage(sb.ToString().TrimEnd());

            log.Info($"[LOTTERY] Draw complete. Tickets={totalTickets}, Collected={totalCollected:N0}, Pool={prizePool:N0}.");
            foreach (var (id, name, prize, place) in winners)
                log.Info($"[LOTTERY]   {place}{(place == 1 ? "st" : place == 2 ? "nd" : "rd")} place: {name} (0x{id:X8}) — {prize:N0} lum");
        }

        // ──────────────────────────────────────────────────────────────────
        // Award luminance (online or offline)
        // ──────────────────────────────────────────────────────────────────

        private static void AwardLuminance(uint characterId, string name, long amount)
        {
            if (amount <= 0)
                return;

            var onlinePlayer = PlayerManager.GetOnlinePlayer(characterId);
            if (onlinePlayer != null)
            {
                onlinePlayer.BankedLuminance = (onlinePlayer.BankedLuminance ?? 0) + amount;
                onlinePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"[LOTTERY] Congratulations! You won {amount:N0} luminance in this week's lottery!", ChatMessageType.Broadcast));
                log.Info($"[LOTTERY] Awarded {amount:N0} lum to online player {name} (0x{characterId:X8}).");
                return;
            }

            var offlinePlayer = PlayerManager.GetOfflinePlayer(characterId);
            if (offlinePlayer != null)
            {
                offlinePlayer.BankedLuminance = (offlinePlayer.BankedLuminance ?? 0) + amount;
                offlinePlayer.SaveBiotaToDatabase();
                log.Info($"[LOTTERY] Awarded {amount:N0} lum to offline player {name} (0x{characterId:X8}) — saved to DB.");
                return;
            }

            log.Warn($"[LOTTERY] Could not find player {name} (0x{characterId:X8}) to award {amount:N0} lum. Prize lost.");
        }

        // ──────────────────────────────────────────────────────────────────
        // DB helpers
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads all biota entries with LotteryTickets > 0 and LotteryWeekNumber == current week
        /// into the in-memory dict, and rebuilds the IP → characterId map from account records.
        /// </summary>
        private static void LoadCurrentWeekEntries()
        {
            _entries.Clear();
            _ipToCharId.Clear();

            var currentWeek = GetCurrentWeekNumber();
            var ticketType = (ushort)PropertyInt64.LotteryTickets;
            var weekType = (ushort)PropertyInt64.LotteryWeekNumber;

            try
            {
                using var context = new ShardDbContext();

                var participants = (
                    from t in context.BiotaPropertiesInt64
                    join w in context.BiotaPropertiesInt64 on t.ObjectId equals w.ObjectId
                    join c in context.Character on t.ObjectId equals c.Id
                    where t.Type == ticketType && t.Value > 0
                       && w.Type == weekType && w.Value == currentWeek
                    select new { CharId = t.ObjectId, Name = c.Name, Tickets = (int)t.Value, AccountId = (uint)c.AccountId }
                ).ToList();

                foreach (var p in participants)
                {
                    _entries[p.CharId] = new LotteryEntry { Name = p.Name, Tickets = p.Tickets };

                    // Rebuild IP map from the stored account's last-login IP
                    var ip = GetAccountIp(p.AccountId);
                    if (ip != null)
                        _ipToCharId.TryAdd(ip, p.CharId);
                }
            }
            catch (Exception ex)
            {
                log.Error($"[LOTTERY] Error loading current week entries: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Zeros out LotteryTickets for all current-week participants so they don't carry over.
        /// Also clears the IP map so the next week starts fresh.
        /// </summary>
        private static void ClearCurrentWeekEntries()
        {
            if (_entries.IsEmpty)
                return;

            var ticketType = (ushort)PropertyInt64.LotteryTickets;
            var participantIds = _entries.Keys.ToHashSet();

            try
            {
                using var context = new ShardDbContext();

                var toReset = context.BiotaPropertiesInt64
                    .Where(p => participantIds.Contains(p.ObjectId) && p.Type == ticketType)
                    .ToList();

                foreach (var entry in toReset)
                    entry.Value = 0;

                context.SaveChanges();
                log.Info($"[LOTTERY] Cleared lottery tickets for {toReset.Count} participant(s).");
            }
            catch (Exception ex)
            {
                log.Error($"[LOTTERY] Error clearing entries after draw: {ex.Message}", ex);
            }

            _ipToCharId.Clear();
        }

        // ──────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the ISO week number in the form yyyyWW (e.g. 202625).
        /// Used as the week identifier stored in PropertyInt64.LotteryWeekNumber.
        /// </summary>
        public static int GetCurrentWeekNumber()
        {
            var estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EstTimeZone);
            int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(estNow, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);
            return estNow.Year * 100 + week;
        }

        /// <summary>
        /// Human-readable key, e.g. "2026-W25".  Used for the draw dedup guard.
        /// </summary>
        public static string GetCurrentWeekKey()
        {
            var n = GetCurrentWeekNumber();
            return $"{n / 100}-W{n % 100:D2}";
        }

        /// <summary>
        /// Calculates the UTC DateTime of the next configured draw.
        /// </summary>
        public static DateTime NextDrawTime()
        {
            var drawDayOfWeek = (DayOfWeek)(int)Math.Max(0, Math.Min(6, PropertyManager.GetLong("lottery_draw_day_of_week")));
            var drawHour = (int)Math.Max(0, Math.Min(23, PropertyManager.GetLong("lottery_draw_hour_est")));

            var estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EstTimeZone);
            int daysUntil = ((int)drawDayOfWeek - (int)estNow.DayOfWeek + 7) % 7;
            if (daysUntil == 0 && estNow.Hour >= drawHour)
                daysUntil = 7;

            var estDraw = estNow.Date.AddDays(daysUntil).AddHours(drawHour);
            return TimeZoneInfo.ConvertTimeToUtc(estDraw, EstTimeZone);
        }

        /// <summary>
        /// Returns the current pot size in luminance (total collected × pot_share).
        /// </summary>
        public static long GetCurrentPrizePool()
        {
            var ticketCost = PropertyManager.GetLong("lottery_ticket_cost_lum");
            var potShare = PropertyManager.GetDouble("lottery_pot_share");
            long totalTickets = _entries.Values.Sum(e => e.Tickets);
            return (long)(totalTickets * ticketCost * potShare);
        }

        /// <summary>
        /// Admin: force the draw to run immediately regardless of the scheduled time.
        /// Resets the week-key guard so the normal Sunday timer won't fire a second time.
        /// </summary>
        public static void ForceRunDraw(Session adminSession)
        {
            var weekKey = GetCurrentWeekKey();
            lock (_drawLock)
            {
                _lastDrawWeekKey = weekKey;
            }

            var adminName = adminSession?.Player?.Name ?? "CONSOLE";
            log.Warn($"[LOTTERY] Draw forced early by admin: {adminName}");
            PlayerManager.BroadcastToAuditChannel(adminSession?.Player,
                $"[LOTTERY] Weekly draw forced early by {adminName}.");

            TryRunWeeklyDraw();

            // Disable the lottery after a forced early draw so it doesn't reopen until explicitly re-enabled.
            PropertyManager.ModifyBool("lottery_enabled", false);
            log.Info("[LOTTERY] Lottery auto-disabled after forced early draw.");
            PlayerManager.BroadcastToAuditChannel(adminSession?.Player,
                "[LOTTERY] Lottery has been automatically disabled after the forced draw.");
        }

        // ──────────────────────────────────────────────────────────────────
        // IP helpers
        // ──────────────────────────────────────────────────────────────────

        private static string GetPlayerIp(Session session)
        {
            try
            {
                var ipBytes = session?.Player?.Account?.LastLoginIP;
                if (ipBytes == null || ipBytes.Length == 0)
                    return null;
                return new IPAddress(ipBytes).ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string GetAccountIp(uint accountId)
        {
            try
            {
                var account = DatabaseManager.Authentication.GetAccountById(accountId);
                if (account?.LastLoginIP == null || account.LastLoginIP.Length == 0)
                    return null;
                return new IPAddress(account.LastLoginIP).ToString();
            }
            catch
            {
                return null;
            }
        }

        private static void BroadcastSystemMessage(string msg)
        {
            foreach (var line in msg.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimEnd();
                if (!string.IsNullOrEmpty(trimmed))
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat(trimmed, ChatMessageType.WorldBroadcast));
            }
        }
    }
}

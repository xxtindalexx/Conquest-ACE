using System;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        #region CONQUEST: Luminance Per Hour Tracking

        /// <summary>
        /// Whether LPH tracking is active for this player
        /// </summary>
        public bool LphTrackingActive { get; private set; } = false;

        /// <summary>
        /// When LPH tracking started
        /// </summary>
        private DateTime _lphStartTime;

        /// <summary>
        /// Total luminance earned since tracking started
        /// </summary>
        private long _lphTotalLuminance = 0;

        /// <summary>
        /// Luminance earned in current 5-minute window
        /// </summary>
        private long _lphCurrent5MinLum = 0;

        /// <summary>
        /// Cached 5-minute rate (lum/hour based on last completed 5-min window)
        /// </summary>
        private long _lph5MinRate = 0;

        /// <summary>
        /// When the current 5-minute window started
        /// </summary>
        private DateTime _lph5MinWindowStart;

        /// <summary>
        /// Start tracking luminance per hour
        /// </summary>
        public void LphStart()
        {
            LphTrackingActive = true;
            _lphStartTime = DateTime.UtcNow;
            _lphTotalLuminance = 0;
            _lphCurrent5MinLum = 0;
            _lph5MinRate = 0;
            _lph5MinWindowStart = DateTime.UtcNow;
            Session.Network.EnqueueSend(new GameMessageSystemChat("[LPH] Luminance tracking started.", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Restart tracking luminance per hour
        /// </summary>
        public void LphRestart()
        {
            LphStart();
        }

        /// <summary>
        /// Stop tracking luminance per hour
        /// </summary>
        public void LphStop()
        {
            LphTrackingActive = false;
            Session.Network.EnqueueSend(new GameMessageSystemChat("[LPH] Luminance tracking stopped.", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Track a luminance gain for LPH calculations
        /// </summary>
        private void LphTrackGain(long amount)
        {
            if (!LphTrackingActive || amount <= 0)
                return;

            _lphTotalLuminance += amount;
            _lphCurrent5MinLum += amount;

            // Check if 5-minute window has elapsed
            var now = DateTime.UtcNow;
            if ((now - _lph5MinWindowStart).TotalMinutes >= 5)
            {
                // Snapshot the rate: lum earned in window * 12 = lum/hour
                _lph5MinRate = _lphCurrent5MinLum * 12;

                // Reset for next window
                _lphCurrent5MinLum = 0;
                _lph5MinWindowStart = now;
            }
        }

        /// <summary>
        /// Get the current LPH stats and display to player
        /// </summary>
        public void LphCheck()
        {
            if (!LphTrackingActive)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("[LPH] Tracking not active. Use /lph start to begin tracking.", ChatMessageType.Broadcast));
                return;
            }

            var elapsed = DateTime.UtcNow - _lphStartTime;
            var elapsedStr = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

            // Calculate overall rate
            double hoursElapsed = elapsed.TotalHours;
            long overallLph = hoursElapsed > 0 ? (long)(_lphTotalLuminance / hoursElapsed) : 0;

            // Build the message similar to VTank format
            var msg = $"[LPH] You've earned {_lphTotalLuminance:N0} Luminance in {elapsedStr} for {overallLph:N0} Lum/hour (5min {_lph5MinRate:N0} Lum/hour).";
            Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        }

        #endregion
        /// <summary>
        /// Applies luminance modifiers before adding luminance
        /// </summary>
        public void EarnLuminance(long amount, XpType xpType, ShareType shareType = ShareType.All)
        {
            if (IsOlthoiPlayer || IsMule)
                return;

            // Apply server modifiers
            var questModifier = PropertyManager.GetDouble("quest_lum_modifier");
            var modifier = PropertyManager.GetDouble("luminance_modifier");
            if (xpType == XpType.Quest)
                modifier *= questModifier;

            // CONQUEST: Fellowship Luminance Sharing - share BASE amount only (no personal bonuses)
            // This matches XP behavior - each member applies their own bonuses to their share
            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship))
            {
                // Apply only server modifiers, not personal bonuses
                var baseAmount = (long)Math.Round(amount * modifier);

                if (baseAmount < 0)
                    return;

                // Share the base amount - each member will apply their own bonuses
                GrantLuminance(baseAmount, xpType, shareType);
                return;
            }

            // Solo player or non-shareable luminance - apply personal bonuses
            var enchantment = GetXPAndLuminanceModifier(xpType);

            // CONQUEST: Quest Bonus System - account-wide quest completion bonus
            var questBonus = GetQuestCountXPBonus();

            // CONQUEST: PK Dungeon Bonus - +10% Lum in PK-only dungeons
            var pkDungeonBonus = GetPKDungeonBonus();

            // CONQUEST: Enlightenment bonus (+1% per level)
            var enlightenmentBonus = 1.0 + (Enlightenment * 0.01);

            var m_amount = (long)Math.Round(amount * enchantment * modifier * questBonus * pkDungeonBonus * enlightenmentBonus);

            // CONQUEST: Show Luminance breakdown if player has enabled it via /xpdebugging command
            if (ShowXpBreakdown && (xpType == XpType.Kill || xpType == XpType.Quest))
            {
                var bonusLum = m_amount - amount;
                var questBonusPercent = (questBonus - 1.0) * 100;
                var pkBonusPercent = (pkDungeonBonus - 1.0) * 100;
                var enlightenmentBonusPercent = Enlightenment * 1.0;
                var equipmentBonusPercent = EnchantmentManager.GetXPBonus() * 100;

                var lumSource = xpType == XpType.Quest ? "Quest" : "Kill";
                Session.Network.EnqueueSend(new Network.GameMessages.Messages.GameMessageSystemChat(
                    $"Lum Breakdown ({lumSource}): {amount:N0} base → {m_amount:N0} total (+{bonusLum:N0} bonus)\n" +
                    $"Modifiers: Quest {questBonusPercent:F2}% | PK {pkBonusPercent:F0}% | ENL {enlightenmentBonusPercent:F0}% | Equip {equipmentBonusPercent:F0}%",
                    ChatMessageType.Broadcast));
            }

            // CONQUEST: Call AddLuminance directly since bonuses are already applied
            // Don't call GrantLuminance here as it would apply bonuses again!
            AddLuminance(m_amount, xpType, shareType);
        }

        /// <summary>
        /// Directly grants luminance to the player, without any additional luminance modifiers
        /// </summary>
        public void GrantLuminance(long amount, XpType xpType, ShareType shareType = ShareType.All)
        {
            if (IsOlthoiPlayer)
                return;

            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship))
            {
                // this will divy up the luminance, and re-call this function
                // with ShareType.Fellowship removed
                Fellowship.SplitLuminance((ulong)amount, xpType, shareType, this);
                return;
            }

            // CONQUEST: Apply personal bonuses to fellowship share
            // When receiving a fellowship share, apply the recipient's personal bonuses
            // (all personal bonuses) but NOT server modifiers (already applied)
            var enchantment = GetXPAndLuminanceModifier(xpType);
            var questBonus = GetQuestCountXPBonus();
            var pkDungeonBonus = GetPKDungeonBonus();
            var enlightenmentBonus = 1.0 + (Enlightenment * 0.01);

            var bonusedAmount = (long)Math.Round(amount * enchantment * questBonus * pkDungeonBonus * enlightenmentBonus);

            // CONQUEST: Show Luminance breakdown for fellowship shares if player has enabled xpdebugging
            if (ShowXpBreakdown && (xpType == XpType.Kill || xpType == XpType.Quest || xpType == XpType.Fellowship))
            {
                var bonusLum = bonusedAmount - amount;
                var questBonusPercent = (questBonus - 1.0) * 100;
                var pkBonusPercent = (pkDungeonBonus - 1.0) * 100;
                var enlightenmentBonusPercent = Enlightenment * 1.0;
                var equipmentBonusPercent = EnchantmentManager.GetXPBonus() * 100;

                var lumSource = xpType == XpType.Fellowship ? "Fellowship" : (xpType == XpType.Quest ? "Quest" : "Kill");
                Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"Lum Breakdown ({lumSource}): {amount:N0} share → {bonusedAmount:N0} total (+{bonusLum:N0} bonus)\n" +
                    $"Modifiers: Quest {questBonusPercent:F2}% | PK {pkBonusPercent:F0}% | ENL {enlightenmentBonusPercent:F0}% | Equip {equipmentBonusPercent:F0}%",
                    ChatMessageType.Broadcast));
            }

            AddLuminance(bonusedAmount, xpType, shareType);
        }

        private void AddLuminance(long amount, XpType xpType, ShareType shareType)
        {
            if (!BankedLuminance.HasValue)
            {
                BankedLuminance = 0;
            }
            BankedLuminance += amount;
            if (xpType == XpType.Quest || xpType == XpType.Kill || xpType == XpType.Fellowship)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You've banked {amount:N0} Luminance.", ChatMessageType.Broadcast));

            if (shareType.HasFlag(ShareType.Allegiance))
                UpdateLumAllegiance(amount);

            // CONQUEST: Track luminance for /lph command
            LphTrackGain(amount);

            // 20250203 - Don't spam the client with properties it doesn't use
            //UpdateLuminance();
        }

        /// <summary>
        /// Spends the amount of luminance specified, deducting it from banked luminance first, then available luminance
        /// CONQUEST: Changed to check BankedLuminance first for NPC transactions
        /// </summary>
        public bool SpendLuminance(long amount)
        {

            if (!BankedLuminance.HasValue) { BankedLuminance = 0; }
            if (!AvailableLuminance.HasValue) { AvailableLuminance = 0; }

            // CONQUEST: Try banked luminance first (for NPC transactions)
            if (BankedLuminance > 0 && BankedLuminance >= amount)
            {
                BankedLuminance = BankedLuminance - amount;
                UpdateLuminance();
                return true;
            }

            // Fall back to available luminance (from aetheria)
            if (AvailableLuminance > 0 && AvailableLuminance >= amount)
            {
                AvailableLuminance = AvailableLuminance - amount;
                UpdateLuminance();
                return true;
            }

            return false;
        }

        private void UpdateLumAllegiance(long amount)
        {
            if (!HasAllegiance)
                return;

            if (amount <= 0)
                return;

            AllegianceManager.PassXP(AllegianceNode, (ulong)amount, true, true);
        }

        private void UpdateLuminance()
        {
            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableLuminance, AvailableLuminance ?? 0));
            //Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.BankedLuminance, BankedLuminance ?? 0));
        }
    }
}

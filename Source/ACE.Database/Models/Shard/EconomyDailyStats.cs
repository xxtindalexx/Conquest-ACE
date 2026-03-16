using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    /// <summary>
    /// CONQUEST: Tracks daily economy statistics for luminance and conquest coins
    /// </summary>
    [Table("economy_daily_stats")]
    public class EconomyDailyStats
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The date this record covers (UTC, truncated to day)
        /// </summary>
        [Required]
        public DateTime StatsDate { get; set; }

        /// <summary>
        /// Currency type: "luminance" or "conquest_coin"
        /// </summary>
        [Required]
        [StringLength(50)]
        public string CurrencyType { get; set; }

        /// <summary>
        /// Source of the currency: "kill", "quest", "passup", "treasure_map", "bank_withdrawal", "other"
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Source { get; set; }

        /// <summary>
        /// Total amount generated from this source
        /// </summary>
        public long TotalAmount { get; set; }

        /// <summary>
        /// Number of times this source generated currency
        /// </summary>
        public long TransactionCount { get; set; }

        /// <summary>
        /// Number of unique players who received currency from this source
        /// </summary>
        public int UniquePlayerCount { get; set; }

        /// <summary>
        /// Last time this record was updated
        /// </summary>
        public DateTime LastUpdated { get; set; }
    }
}

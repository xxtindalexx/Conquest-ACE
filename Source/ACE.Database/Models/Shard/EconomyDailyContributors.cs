using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    /// <summary>
    /// CONQUEST: Tracks unique player contributors per day for deduplication after restart
    /// </summary>
    [Table("economy_daily_contributors")]
    public class EconomyDailyContributors
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
        /// Player GUID who contributed
        /// </summary>
        public uint PlayerGuid { get; set; }
    }
}

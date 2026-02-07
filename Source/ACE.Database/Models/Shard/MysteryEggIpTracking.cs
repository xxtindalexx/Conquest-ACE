using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Shard
{
    /// <summary>
    /// CONQUEST: Tracks mystery egg acquisition per IP address per week
    /// Allows 2 eggs per IP per week, with exempt accounts getting additional quota
    /// </summary>
    [Table("mystery_egg_ip_tracking")]
    public class MysteryEggIpTracking
    {
        [Key]
        [Column("id")]
        public uint Id { get; set; }

        [Column("ip_address")]
        [Required]
        [MaxLength(45)]  // IPv6 max length
        public string IpAddress { get; set; }

        [Column("eggs_obtained")]
        public int EggsObtained { get; set; }

        [Column("week_start_time")]
        public long WeekStartTime { get; set; }

        [Column("last_egg_time")]
        public long LastEggTime { get; set; }
    }
}

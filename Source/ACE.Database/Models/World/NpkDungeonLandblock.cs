using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.World
{
    /// <summary>
    /// CONQUEST: Stores NPK-only dungeon landblock+variant combinations
    /// Players in these dungeons must be NPK and cannot flag PK while inside
    /// </summary>
    [Table("npk_dungeon_landblocks")]
    public partial class NpkDungeonLandblock
    {
        /// <summary>
        /// Landblock ID (e.g., 0x002B)
        /// </summary>
        [Key]
        [Column(Order = 0)]
        public ushort Landblock { get; set; }

        /// <summary>
        /// Variation/variant number (0 = base, 1+ = variants)
        /// </summary>
        [Key]
        [Column(Order = 1)]
        public int Variation { get; set; }

        /// <summary>
        /// Optional description for admins (e.g., "Safe Egg Orchard Variant 2")
        /// </summary>
        [StringLength(255)]
        public string Description { get; set; }
    }
}

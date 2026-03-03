using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.World
{
    /// <summary>
    /// CONQUEST: Stores landblocks exempt from IP character restrictions
    /// Players can have multiple characters from the same IP in these locations (e.g., Marketplace, Apartments)
    /// </summary>
    [Table("exempt_landblocks")]
    public partial class ExemptLandblock
    {
        /// <summary>
        /// Landblock ID (e.g., 0x5756)
        /// </summary>
        [Key]
        public ushort Landblock { get; set; }

        /// <summary>
        /// Optional description for admins (e.g., "Marketplace", "Apartments")
        /// </summary>
        [StringLength(255)]
        public string Description { get; set; }
    }
}

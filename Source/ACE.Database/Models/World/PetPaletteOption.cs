using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// CONQUEST: Pet palette options for randomizing pet appearances at hatching
/// </summary>
public partial class PetPaletteOption
{
    /// <summary>
    /// Unique Id of this palette option
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Creature WCID this palette option applies to (0 = applies to all pets)
    /// </summary>
    public uint PetWcid { get; set; }

    /// <summary>
    /// PaletteTemplate to use (null = use creature default)
    /// </summary>
    public int? PaletteTemplate { get; set; }

    /// <summary>
    /// Minimum shade value for random range
    /// </summary>
    public float ShadeMin { get; set; }

    /// <summary>
    /// Maximum shade value for random range
    /// </summary>
    public float ShadeMax { get; set; }

    /// <summary>
    /// Rarity weight for this option (higher = more common)
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Optional name/description for this palette option
    /// </summary>
    public string Name { get; set; }

    public DateTime LastModified { get; set; }

    /// <summary>
    /// Sub-palettes for this option (custom color overrides)
    /// </summary>
    public virtual ICollection<PetPaletteSubPalette> PetPaletteSubPalettes { get; set; } = new HashSet<PetPaletteSubPalette>();
}

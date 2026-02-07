using System;

namespace ACE.Database.Models.World;

/// <summary>
/// CONQUEST: Sub-palette entries for custom pet color overrides
/// </summary>
public partial class PetPaletteSubPalette
{
    /// <summary>
    /// Unique Id of this sub-palette entry
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Foreign key to parent PetPaletteOption
    /// </summary>
    public uint PaletteOptionId { get; set; }

    /// <summary>
    /// The palette ID to use for this sub-palette region
    /// </summary>
    public uint SubPaletteId { get; set; }

    /// <summary>
    /// Start offset in the palette
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Number of palette entries to replace
    /// </summary>
    public uint Length { get; set; }

    public DateTime LastModified { get; set; }

    /// <summary>
    /// Navigation property to parent PetPaletteOption
    /// </summary>
    public virtual PetPaletteOption PetPaletteOption { get; set; }
}

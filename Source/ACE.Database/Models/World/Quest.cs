using System;
using System.Collections.Generic;
using ACE.Database.Models.Shard;

namespace ACE.Database.Models.World;

/// <summary>
/// Quests
/// </summary>
public partial class Quest
{
    /// <summary>
    /// Unique Id of this Quest
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Unique Name of Quest
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Minimum time between Quest completions
    /// </summary>
    public uint MinDelta { get; set; }

    /// <summary>
    /// Maximum number of times Quest can be completed
    /// </summary>
    public int MaxSolves { get; set; }

    /// <summary>
    /// Quest solved text - unused?
    /// </summary>
    public string Message { get; set; }

    public DateTime LastModified { get; set; }
    /// <summary>
    /// Whether this quest has IP-based restrictions
    /// </summary>
    public bool IsIpRestricted { get; set; }

    /// <summary>
    /// Maximum number of characters per IP that can complete this quest
    /// </summary>
    public int? IpLootLimit { get; set; }

    /// <summary>
    /// IP tracking records for this quest (from shard database)
    /// </summary>
    //public virtual ICollection<QuestIpTracking> QuestIpTrackings { get; set; } = new HashSet<QuestIpTracking>();
}

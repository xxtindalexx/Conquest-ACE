using System;
using System.Collections.Generic;

namespace ACE.Database.Models.Auth;

// CONQUEST: Leaderboard system for tracking top players
public partial class Leaderboard
{
    public ulong? Score { get; set; }

    public uint? Account { get; set; }

    public string Character { get; set; }

    public ulong LeaderboardId { get; set; }
}

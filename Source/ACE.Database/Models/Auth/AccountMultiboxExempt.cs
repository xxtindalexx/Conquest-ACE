using System;

namespace ACE.Database.Models.Auth;

// CONQUEST: Multibox exemption tracking for household/family accounts
public partial class AccountMultiboxExempt
{
    public uint AccountId { get; set; }

    public string ExemptedBy { get; set; }

    public DateTime ExemptedTime { get; set; }

    public string Reason { get; set; }
}

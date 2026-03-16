using System;

namespace ACE.Database.Models.Auth;

// CONQUEST: VPN exemption by IP address for false positives
public partial class AccountVpnExempt
{
    public uint Id { get; set; }

    public string IpAddress { get; set; }

    public string ExemptedBy { get; set; }

    public DateTime ExemptedTime { get; set; }

    public string Reason { get; set; }
}


namespace ACE.Common
{
    public class NetworkSettings
    {
        /// <summary>
        /// IP Address for World to listen on. 
        /// </summary>
        /// <remarks>
        /// In just about all cases, you will not need to change this setting from the default. Special networking conditions, or multiple network adapters would possibly be examples of times where you might need to change this.
        /// </remarks>
        public string Host { get; set; } = "0.0.0.0";

        /// <summary>
        /// Port for World to listen on. This also opens the next port up, 9001, for server to client communications. When changed, it will open the port specified and +1 of that port.
        /// </summary>
        /// <remarks>
        /// For firewalls, you would need to include opening both udp ports (9000 and 9001) for communications to work correctly.
        /// </remarks>
        public uint Port { get; set; } = 9000;

        /// <summary>
        /// Increasing this setting will allow more Accounts to connect with this server.
        /// </summary>
        /// <remarks>
        /// WARNING: Must be greater then 0 to allow users to connect.
        /// </remarks>
        public uint MaximumAllowedSessions { get; set; } = 128;

        /// <summary>
        /// The amount of seconds until an active session will be declared dead/inactive. Default value is 60 (for 1 minute).
        /// </summary>
        public uint DefaultSessionTimeout { get; set; } = 60;

        /// <summary>
        /// This setting controls how many connections from the same IP can connect to the server.
        /// </summary>
        /// <remarks>
        /// -1 = unlimited connections allowed
        /// Set this to -1 to allow multiple accounts from same IP to connect
        /// </remarks>
        public int MaximumAllowedSessionsPerIPAddress { get; set; } = -1;

        /// <summary>
        /// CONQUEST: This setting restricts how many characters from the same IP can be active outside marketplace/apartments.
        /// Set to 1 to allow only 1 character outside marketplace/apartments at a time per IP.
        /// </summary>
        /// <remarks>
        /// -1 = unlimited, 1 = one character outside exempt areas (marketplace/apartments)
        /// This only applies when characters leave marketplace - unlimited characters allowed in marketplace
        /// </remarks>
        public int MaximumCharactersOutsideMarketplace { get; set; } = 1;

        /// <summary>
        /// Will allow the given IP addresses to have unlimited sessions - recommend only use this for Admins
        /// </summary>
        /// <remarks>
        /// </remarks>
        public string[] AllowUnlimitedSessionsFromIPAddresses { get; set; } = { };
    }
}

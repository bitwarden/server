using System.Net;

namespace Bit.Core.Utilities;

/// <summary>
/// Extension methods for <see cref="IPAddress"/> to determine if an address is internal/private.
/// Used for SSRF protection to block requests to private network ranges.
/// </summary>
public static class IPAddressExtensions
{
    /// <summary>
    /// Determines whether the given IP address is an internal/private/reserved address.
    /// This includes loopback, private RFC 1918, link-local, CGNAT (RFC 6598),
    /// IPv6 unique local, and other reserved ranges.
    /// </summary>
    /// <param name="ip">The IP address to check.</param>
    /// <returns>True if the IP is internal/private/reserved; otherwise false.</returns>
    public static bool IsInternal(this IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var ipString = ip.ToString();
        if (ipString == "::1" || ipString == "::" || ipString.StartsWith("::ffff:"))
        {
            return true;
        }

        // IPv6
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return ipString.StartsWith("fc") || ipString.StartsWith("fd") ||
                ipString.StartsWith("fe") || ipString.StartsWith("ff");
        }

        // IPv4
        var bytes = ip.GetAddressBytes();
        return bytes[0] switch
        {
            0 => true,                                      // "This" network (RFC 1122)
            10 => true,                                     // Private (RFC 1918)
            100 => bytes[1] >= 64 && bytes[1] < 128,       // CGNAT (RFC 6598) - 100.64.0.0/10
            127 => true,                                    // Loopback (RFC 1122)
            169 => bytes[1] == 254,                         // Link-local / cloud metadata (RFC 3927)
            172 => bytes[1] >= 16 && bytes[1] < 32,        // Private (RFC 1918)
            192 => bytes[1] == 168,                         // Private (RFC 1918)
            _ => false,
        };
    }
}

using System.Net;
using System.Net.Sockets;

namespace Bit.Core.Utilities;

/// <summary>
/// Extension methods for <see cref="IPAddress"/> to determine if an address is internal/private.
/// Used for SSRF protection to block requests to reserved network ranges defined by the
/// IANA IPv4 and IPv6 Special-Purpose Address Registries.
/// </summary>
public static class IPAddressExtensions
{
    private static readonly IPNetwork[] _reservedIPv4Networks =
    [
        new(IPAddress.Parse("0.0.0.0"), 8),           // RFC 1122 "This" network
        new(IPAddress.Parse("10.0.0.0"), 8),          // RFC 1918 Private
        new(IPAddress.Parse("100.64.0.0"), 10),       // RFC 6598 CGNAT
        new(IPAddress.Parse("127.0.0.0"), 8),         // RFC 1122 Loopback
        new(IPAddress.Parse("168.63.129.16"), 32),    // Azure IP address 168.63.129.16 (https://learn.microsoft.com/en-us/azure/virtual-network/what-is-ip-address-168-63-129-16)
        new(IPAddress.Parse("169.254.0.0"), 16),      // RFC 3927 Link-local / cloud metadata
        new(IPAddress.Parse("172.16.0.0"), 12),       // RFC 1918 Private
        new(IPAddress.Parse("192.0.0.0"), 24),        // RFC 6890 IETF Protocol Assignments (includes Oracle Cloud metadata 192.0.0.192)
        new(IPAddress.Parse("192.0.2.0"), 24),        // RFC 5737 TEST-NET-1
        new(IPAddress.Parse("192.88.99.0"), 24),      // RFC 7526 6to4 Relay Anycast (deprecated)
        new(IPAddress.Parse("192.168.0.0"), 16),      // RFC 1918 Private
        new(IPAddress.Parse("198.18.0.0"), 15),       // RFC 2544 Benchmarking
        new(IPAddress.Parse("198.51.100.0"), 24),     // RFC 5737 TEST-NET-2
        new(IPAddress.Parse("203.0.113.0"), 24),      // RFC 5737 TEST-NET-3
        new(IPAddress.Parse("224.0.0.0"), 4),         // RFC 5771 Multicast
        new(IPAddress.Parse("240.0.0.0"), 4),         // RFC 1112 Reserved (includes 255.255.255.255 limited broadcast)
    ];

    private static readonly IPNetwork[] _reservedIPv6Networks =
    [
        new(IPAddress.Parse("::"), 128),              // RFC 4291 Unspecified
        new(IPAddress.Parse("::1"), 128),             // RFC 4291 Loopback
        new(IPAddress.Parse("64:ff9b:1::"), 48),      // RFC 8215 NAT64 local-use
        new(IPAddress.Parse("100::"), 64),            // RFC 6666 Discard-only
        new(IPAddress.Parse("2001::"), 32),           // RFC 4380 Teredo
        new(IPAddress.Parse("2001:2::"), 48),         // RFC 5180 Benchmarking
        new(IPAddress.Parse("2001:3::"), 32),         // RFC 7450 AMT
        new(IPAddress.Parse("2001:10::"), 28),        // RFC 4843 ORCHID (deprecated)
        new(IPAddress.Parse("2001:20::"), 28),        // RFC 7343 ORCHIDv2
        new(IPAddress.Parse("2001:db8::"), 32),       // RFC 3849 Documentation
        new(IPAddress.Parse("3fff::"), 20),           // RFC 9637 Documentation
        new(IPAddress.Parse("5f00::"), 16),           // RFC 9602 Segment Routing
        new(IPAddress.Parse("fc00::"), 7),            // RFC 4193 Unique Local Address
        new(IPAddress.Parse("fe80::"), 10),           // RFC 4291 Link-local
        new(IPAddress.Parse("ff00::"), 8),            // RFC 4291 Multicast
    ];

    // IPv6 prefixes whose addresses embed an IPv4 destination. When the embedded IPv4 is itself
    // reserved (e.g., RFC 1918), the IPv6 address is treated as internal because a NAT64 or 6to4
    // gateway on the path would translate it back to the internal IPv4 host.
    private static readonly (IPNetwork Prefix, int IPv4ByteOffset)[] _ipv4EmbeddedIPv6Networks =
    [
        (new(IPAddress.Parse("64:ff9b::"), 96), 12),    // RFC 6052 NAT64 well-known: IPv4 at bytes 12-15
        (new(IPAddress.Parse("2002::"), 16), 2),        // RFC 3056 6to4: IPv4 at bytes 2-5
    ];

    public static bool IsInternal(this IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return IsReservedIPv4(ip);
        }

        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            return IsReservedIPv4(ip.MapToIPv4());
        }

        foreach (var (prefix, offset) in _ipv4EmbeddedIPv6Networks)
        {
            if (prefix.Contains(ip))
            {
                var bytes = ip.GetAddressBytes();
                var embedded = new IPAddress(bytes.AsSpan(offset, 4));
                if (IsReservedIPv4(embedded))
                {
                    return true;
                }
            }
        }

        return _reservedIPv6Networks.Any(network => network.Contains(ip));
    }

    private static bool IsReservedIPv4(IPAddress ip)
    {
        return _reservedIPv4Networks.Any(network => network.Contains(ip));
    }
}

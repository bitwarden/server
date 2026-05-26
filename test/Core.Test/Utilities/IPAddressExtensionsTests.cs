using System.Net;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class IPAddressExtensionsTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]       // Loopback
    [InlineData("127.0.0.2", true)]       // Loopback range
    [InlineData("10.0.0.1", true)]        // Private Class A
    [InlineData("10.255.255.255", true)]  // Private Class A end
    [InlineData("172.16.0.1", true)]      // Private Class B start
    [InlineData("172.31.255.255", true)]  // Private Class B end
    [InlineData("172.15.0.1", false)]     // Just below private Class B
    [InlineData("172.32.0.1", false)]     // Just above private Class B
    [InlineData("192.168.0.1", true)]     // Private Class C
    [InlineData("192.168.255.255", true)] // Private Class C end
    [InlineData("192.167.0.1", false)]    // Not private Class C
    [InlineData("169.254.0.1", true)]     // Link-local / cloud metadata
    [InlineData("169.253.0.1", false)]    // Not link-local
    [InlineData("0.0.0.0", true)]         // "This" network
    [InlineData("8.8.8.8", false)]        // Google DNS - public
    [InlineData("1.1.1.1", false)]        // Cloudflare DNS - public
    [InlineData("52.20.30.40", false)]    // Public IP
    public void IsInternal_IPv4_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("100.64.0.0", true)]      // CGNAT start
    [InlineData("100.64.0.1", true)]      // CGNAT
    [InlineData("100.100.0.1", true)]     // CGNAT middle (also Alibaba Cloud metadata range)
    [InlineData("100.127.255.254", true)] // CGNAT near end
    [InlineData("100.127.255.255", true)] // CGNAT end
    [InlineData("100.63.255.255", false)] // Just below CGNAT
    [InlineData("100.128.0.0", false)]    // Just above CGNAT
    [InlineData("100.0.0.1", false)]      // 100.x but not CGNAT
    public void IsInternal_CGNAT_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("192.0.0.0", true)]       // RFC 6890 IETF Protocol Assignments start
    [InlineData("192.0.0.192", true)]     // Oracle Cloud metadata
    [InlineData("192.0.0.255", true)]     // IETF Protocol Assignments end
    [InlineData("192.0.1.0", false)]      // Gap between 192.0.0.0/24 and 192.0.2.0/24
    [InlineData("192.0.2.5", true)]       // TEST-NET-1
    [InlineData("192.88.99.1", true)]     // 6to4 Relay Anycast
    [InlineData("198.18.0.0", true)]      // Benchmarking start
    [InlineData("198.19.255.255", true)]  // Benchmarking end
    [InlineData("198.20.0.0", false)]     // Just above benchmarking
    [InlineData("198.51.100.5", true)]    // TEST-NET-2
    [InlineData("203.0.113.5", true)]     // TEST-NET-3
    [InlineData("224.0.0.1", true)]       // Multicast start
    [InlineData("223.255.255.255", false)] // Just below multicast
    [InlineData("239.255.255.255", true)] // Multicast end
    [InlineData("240.0.0.1", true)]       // Reserved future
    [InlineData("255.255.255.254", true)] // Reserved future
    [InlineData("255.255.255.255", true)] // Limited broadcast
    public void IsInternal_IPv4Reserved_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("::1", true)]                       // Loopback
    [InlineData("::", true)]                        // Unspecified
    [InlineData("::2", false)]                      // Adjacent to unspecified, not reserved
    [InlineData("fc00::1", true)]                   // ULA
    [InlineData("fd00::1", true)]                   // ULA
    [InlineData("fe80::1", true)]                   // Link-local
    [InlineData("febf:ffff::1", true)]              // Link-local end (fe80::/10)
    [InlineData("fec0::1", false)]                  // Above link-local (old fe-prefix bug)
    [InlineData("fe00::1", false)]                  // Below link-local (old fe-prefix bug)
    [InlineData("ff02::1", true)]                   // Multicast
    [InlineData("2607:f8b0:4004:800::200e", false)] // Google public IPv6
    public void IsInternal_IPv6_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("100::1", true)]            // RFC 6666 Discard-only
    [InlineData("2001::1", true)]           // Teredo
    [InlineData("2001:2::1", true)]         // Benchmarking
    [InlineData("2001:3::1", true)]         // AMT
    [InlineData("2001:10::1", true)]        // ORCHID
    [InlineData("2001:20::1", true)]        // ORCHIDv2
    [InlineData("2001:db8::1", true)]       // RFC 3849 Documentation
    [InlineData("3fff::1", true)]           // RFC 9637 Documentation start
    [InlineData("3fff:fff:ffff:ffff:ffff:ffff:ffff:ffff", true)] // RFC 9637 Documentation end
    [InlineData("4000::1", false)]          // Just above RFC 9637 Documentation
    [InlineData("5f00::1", true)]           // Segment Routing
    [InlineData("64:ff9b:1::aabb:ccdd", true)] // NAT64 local-use (whole prefix reserved)
    public void IsInternal_IPv6Reserved_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("64:ff9b::a00:1", true)]      // NAT64 well-known alias of 10.0.0.1
    [InlineData("64:ff9b::a7b:2", true)]      // NAT64 well-known alias of 10.123.0.2
    [InlineData("64:ff9b::a63:1e2a", true)]   // NAT64 well-known alias of 10.99.30.42
    [InlineData("64:ff9b::a9fe:a9fe", true)]  // NAT64 well-known alias of 169.254.169.254 (cloud metadata)
    [InlineData("64:ff9b::7f00:1", true)]     // NAT64 well-known alias of 127.0.0.1
    [InlineData("64:ff9b::c0a8:1", true)]     // NAT64 well-known alias of 192.168.0.1
    [InlineData("64:ff9b::6440:1", true)]     // NAT64 well-known alias of 100.64.0.1 (CGNAT)
    [InlineData("64:ff9b::c000:c0", true)]    // NAT64 well-known alias of 192.0.0.192 (Oracle metadata)
    [InlineData("64:ff9b::808:808", false)]   // NAT64 well-known alias of 8.8.8.8 (public)
    [InlineData("64:ff9b::101:101", false)]   // NAT64 well-known alias of 1.1.1.1 (public)
    public void IsInternal_NAT64_DecodesEmbeddedIPv4(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("2002:a00:1::", true)]       // 6to4 of 10.0.0.1
    [InlineData("2002:7f00:1::", true)]      // 6to4 of 127.0.0.1
    [InlineData("2002:c0a8:1::", true)]      // 6to4 of 192.168.0.1
    [InlineData("2002:a9fe:a9fe::", true)]   // 6to4 of 169.254.169.254
    [InlineData("2002:808:808::", false)]    // 6to4 of 8.8.8.8 (public)
    [InlineData("2002:101:101::", false)]    // 6to4 of 1.1.1.1 (public)
    public void IsInternal_6to4_DecodesEmbeddedIPv4(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1", true)]            // IPv4-mapped loopback
    [InlineData("::ffff:10.0.0.1", true)]             // IPv4-mapped RFC 1918
    [InlineData("::ffff:192.168.1.1", true)]          // IPv4-mapped RFC 1918
    [InlineData("::ffff:169.254.169.254", true)]      // IPv4-mapped cloud metadata
    [InlineData("::ffff:100.64.0.1", true)]           // IPv4-mapped CGNAT
    [InlineData("::ffff:192.0.0.192", true)]          // IPv4-mapped Oracle Cloud metadata
    [InlineData("::ffff:198.18.0.1", true)]           // IPv4-mapped benchmarking
    [InlineData("::ffff:224.0.0.1", true)]            // IPv4-mapped multicast
    [InlineData("::ffff:255.255.255.255", true)]      // IPv4-mapped limited broadcast
    [InlineData("::ffff:8.8.8.8", false)]             // IPv4-mapped public
    [InlineData("::ffff:1.1.1.1", false)]             // IPv4-mapped public
    public void IsInternal_IPv4MappedIPv6_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }
}

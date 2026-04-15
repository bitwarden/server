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
    [InlineData("100.100.0.1", true)]     // CGNAT middle
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
    [InlineData("::1", true)]             // IPv6 loopback
    [InlineData("::", true)]              // IPv6 unspecified
    [InlineData("fc00::1", true)]         // IPv6 unique local
    [InlineData("fd00::1", true)]         // IPv6 unique local
    [InlineData("fe80::1", true)]         // IPv6 link-local
    [InlineData("ff02::1", true)]         // IPv6 multicast
    [InlineData("2607:f8b0:4004:800::200e", false)] // Google public IPv6
    public void IsInternal_IPv6_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1", true)]   // IPv4-mapped IPv6 loopback
    [InlineData("::ffff:10.0.0.1", true)]     // IPv4-mapped IPv6 private
    [InlineData("::ffff:192.168.1.1", true)]  // IPv4-mapped IPv6 private
    public void IsInternal_IPv4MappedIPv6_ReturnsExpected(string ipString, bool expected)
    {
        var ip = IPAddress.Parse(ipString);
        Assert.Equal(expected, ip.IsInternal());
    }
}

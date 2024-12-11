#nullable enable

using System.Net;

namespace Bit.Icons.Extensions;

public static class IPAddressExtension
{
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
            return ipString.StartsWith("fc")
                || ipString.StartsWith("fd")
                || ipString.StartsWith("fe")
                || ipString.StartsWith("ff");
        }

        // IPv4
        var bytes = ip.GetAddressBytes();
        return (bytes[0]) switch
        {
            0 => true,
            10 => true,
            127 => true,
            169 => bytes[1] == 254, // Cloud environments, such as AWS
            172 => bytes[1] < 32 && bytes[1] >= 16,
            192 => bytes[1] == 168,
            _ => false,
        };
    }
}

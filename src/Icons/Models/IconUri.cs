#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Bit.Icons.Extensions;

namespace Bit.Icons.Models;

public class IconUri
{
    private readonly IPAddress _ip;
    public string Host { get; }
    public Uri InnerUri { get; }
    public string Scheme => InnerUri.Scheme;

    public bool IsValid
    {
        get
        {
            // Prevent direct access to any ip
            if (IPAddress.TryParse(Host, out _))
            {
                return false;
            }

            // Prevent non-http(s) and non-default ports
            if ((InnerUri.Scheme != "http" && InnerUri.Scheme != "https") || !InnerUri.IsDefaultPort)
            {
                return false;
            }

            // Prevent local hosts (localhost, bobs-pc, etc) and IP addresses
            if (!Host.Contains('.') || _ip.IsInternal())
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Creates a new IconUri from a URI string.
    /// </summary>
    /// <param name="uriString"></param>
    /// <throws>Exception if the uriString format is invalid or if an ip cannot be resolved.</throws>
    public IconUri(Uri uri)
    {
        try
        {
            _ip = DetermineIp(uri);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error parsing URI: {uri}", ex);
        }
        // Create the URI to use in retrieving the icon
        InnerUri = uri.ChangeHost(_ip.ToString());
        Host = uri.Host;
    }

    public static bool TryCreate(Uri uri, [NotNullWhen(true)] out IconUri? iconUri)
    {
        try
        {
            iconUri = new IconUri(uri);
            return true;
        }
        catch (Exception)
        {
            iconUri = null;
            return false;
        }
    }

    private static IPAddress DetermineIp(Uri uri)
    {
        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            return ip;
        }

        var hostEntry = Dns.GetHostEntry(uri.Host);
        ip = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork || ip.IsIPv4MappedToIPv6)?.MapToIPv4();
        if (ip == null)
        {
            throw new Exception($"Unable to determine IP for {uri.Host}");
        }
        return ip;
    }
}

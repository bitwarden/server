#nullable enable

using System.Net;
using System.Net.Sockets;
using Bit.Icons.Extensions;
using Bit.Icons.Models;

namespace Bit.Icons.Services;

public class UriService : IUriService
{
    public IconUri GetUri(string inputUri)
    {
        var uri = new Uri(inputUri);
        return new IconUri(uri, DetermineIp(uri));
    }

    public bool TryGetUri(string stringUri, out IconUri? iconUri)
    {
        if (!Uri.TryCreate(stringUri, UriKind.Absolute, out var uri))
        {
            iconUri = null;
            return false;
        }

        return TryGetUri(uri, out iconUri);
    }

    public IconUri GetUri(Uri uri)
    {
        return new IconUri(uri, DetermineIp(uri));
    }

    public bool TryGetUri(Uri uri, out IconUri? iconUri)
    {
        try
        {
            iconUri = GetUri(uri);
            return true;
        }
        catch (Exception)
        {
            iconUri = null;
            return false;
        }
    }

    public IconUri GetRedirect(HttpResponseMessage response, IconUri originalUri)
    {
        if (response.Headers.Location == null)
        {
            throw new Exception("No redirect location found.");
        }

        var redirectUri = DetermineRedirectUri(response.Headers.Location, originalUri);
        return new IconUri(redirectUri, DetermineIp(redirectUri));
    }

    public bool TryGetRedirect(HttpResponseMessage response, IconUri originalUri, out IconUri? iconUri)
    {
        try
        {
            iconUri = GetRedirect(response, originalUri);
            return true;
        }
        catch (Exception)
        {
            iconUri = null;
            return false;
        }
    }

    private static Uri DetermineRedirectUri(Uri responseUri, IconUri originalIconUri)
    {
        if (responseUri.IsAbsoluteUri)
        {
            if (!responseUri.IsHypertext())
            {
                return responseUri.ChangeScheme("https");
            }
            return responseUri;
        }
        else
        {
            return new UriBuilder
            {
                Scheme = originalIconUri.Scheme,
                Host = originalIconUri.Host,
                Path = responseUri.ToString()
            }.Uri;
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

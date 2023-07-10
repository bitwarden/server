#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Net;
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
    /// Represents an ip-validated Uri for use in grabbing an icon.
    /// </summary>
    /// <param name="uriString"></param>
    /// <param name="ip"></param>
    public IconUri(Uri uri, IPAddress ip)
    {
        _ip = ip;
        InnerUri = uri.ChangeHost(_ip.ToString());
        Host = uri.Host;
    }
}

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Utilities;

/// <summary>
/// A <see cref="DelegatingHandler"/> that protects against Server-Side Request Forgery (SSRF)
/// by resolving hostnames to IP addresses before connecting and blocking requests
/// to internal/private/reserved IP ranges.
///
/// This handler performs DNS resolution on the request URI, validates that none of the
/// resolved addresses are internal, and then rewrites the request to connect directly
/// to the validated IP while preserving the original Host header for TLS/SNI.
/// </summary>
public class SsrfProtectionHandler : DelegatingHandler
{
    private readonly ILogger<SsrfProtectionHandler> _logger;

    public SsrfProtectionHandler(ILogger<SsrfProtectionHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new SsrfProtectionException("Request URI is null.");
        }

        var uri = request.RequestUri;
        var host = uri.Host;

        // Resolve the host to IP addresses
        var resolvedAddresses = await ResolveHostAsync(host).ConfigureAwait(false);

        if (resolvedAddresses.Length == 0)
        {
            throw new SsrfProtectionException($"Unable to resolve DNS for host: {host}");
        }

        // Validate ALL resolved addresses — block if any are internal
        foreach (var address in resolvedAddresses)
        {
            var ipToCheck = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            if (ipToCheck.IsInternal())
            {
                _logger.LogWarning(
                    "SSRF protection blocked request to {Host} resolving to internal IP {Address}",
                    host,
                    ipToCheck);
                throw new SsrfProtectionException(
                    $"Request to '{host}' was blocked because it resolves to an internal IP address.");
            }
        }

        // Pick the first valid IPv4 address (prefer IPv4 for compatibility)
        var selectedIp = resolvedAddresses
            .Select(a => a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? resolvedAddresses.First();

        // Rewrite the request URI to use the IP directly, preserving the Host header
        var builder = new UriBuilder(uri)
        {
            Host = selectedIp.ToString()
        };

        // Preserve the original Host header for TLS SNI and virtual hosting
        if (!request.Headers.Contains("Host"))
        {
            request.Headers.Host = uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
        }

        request.RequestUri = builder.Uri;

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a hostname to its IP addresses. If the host is already an IP address,
    /// returns it directly after validation.
    /// </summary>
    private static async Task<IPAddress[]> ResolveHostAsync(string host)
    {
        // If the host is already an IP address, validate and return it directly
        if (IPAddress.TryParse(host, out var directIp))
        {
            return [directIp];
        }

        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(host).ConfigureAwait(false);
            return hostEntry.AddressList;
        }
        catch (SocketException)
        {
            return [];
        }
    }
}

/// <summary>
/// Exception thrown when an SSRF protection check fails.
/// </summary>
public class SsrfProtectionException : Exception
{
    public SsrfProtectionException(string message) : base(message) { }
    public SsrfProtectionException(string message, Exception innerException) : base(message, innerException) { }
}

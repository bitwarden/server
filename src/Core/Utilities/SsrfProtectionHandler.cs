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
///
/// It also handles HTTP redirects manually so that each redirect hop is validated
/// against SSRF rules. Callers should ensure AllowAutoRedirect is disabled on the
/// primary handler to prevent the inner handler from following redirects without validation.
/// </summary>
public class SsrfProtectionHandler : DelegatingHandler
{
    private const int _maxRedirects = 10;

    private static readonly HashSet<HttpStatusCode> _redirectStatusCodes =
    [
        HttpStatusCode.MovedPermanently,   // 301
        HttpStatusCode.Found,              // 302
        HttpStatusCode.SeeOther,           // 303
        HttpStatusCode.TemporaryRedirect,  // 307
        (HttpStatusCode)308                // 308 Permanent Redirect
    ];

    private readonly ILogger<SsrfProtectionHandler> _logger;

    /// <summary>
    /// When <c>true</c> (default), the handler follows HTTP redirects and validates each hop
    /// against SSRF rules. When <c>false</c>, the handler validates the initial request only
    /// and returns redirect responses as-is to the caller. Set to <c>false</c> for clients
    /// that implement their own redirect-following logic (e.g., Icons).
    /// </summary>
    public bool FollowRedirects { get; set; } = true;

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

        // Track the current URI and method across hops. ValidateAndSendAsync rewrites
        // the request URI to a resolved IP, so we must preserve the original hostname-based
        // URI for correct relative redirect resolution and the current method for correct
        // method transitions (e.g., POST→301→GET→307 should preserve GET, not the original POST).
        var currentUri = request.RequestUri!;
        var currentMethod = request.Method;

        var response = await ValidateAndSendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!FollowRedirects)
        {
            return response;
        }

        // Manually follow redirects with SSRF validation on each hop
        var redirectCount = 0;
        while (_redirectStatusCodes.Contains(response.StatusCode) &&
               response.Headers.Location is not null &&
               redirectCount < _maxRedirects)
        {
            redirectCount++;

            var redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(currentUri, response.Headers.Location);

            // Only allow http/https redirects
            if (redirectUri.Scheme != Uri.UriSchemeHttp && redirectUri.Scheme != Uri.UriSchemeHttps)
            {
                _logger.LogWarning(
                    "SSRF protection blocked redirect to non-HTTP scheme {Scheme}",
                    redirectUri.Scheme);
                break;
            }

            // Determine the method for the redirected request
            var redirectMethod = GetRedirectMethod(currentMethod, response.StatusCode);

            response.Dispose();

            using var redirectRequest = new HttpRequestMessage(redirectMethod, redirectUri);
            response = await ValidateAndSendAsync(redirectRequest, cancellationToken).ConfigureAwait(false);

            // Update tracking for next iteration — use the pre-rewrite URI
            // (redirectUri is not mutated by ValidateAndSendAsync)
            currentUri = redirectUri;
            currentMethod = redirectMethod;
        }

        return response;
    }

    /// <summary>
    /// Validates the request URI against SSRF rules and sends the request via the inner handler.
    /// </summary>
    private async Task<HttpResponseMessage> ValidateAndSendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
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
    /// Determines the HTTP method to use for a redirect based on the original method and status code.
    /// </summary>
    private static HttpMethod GetRedirectMethod(HttpMethod originalMethod, HttpStatusCode statusCode)
    {
        // 307 and 308 preserve the original method
        if (statusCode is HttpStatusCode.TemporaryRedirect or (HttpStatusCode)308)
        {
            return originalMethod;
        }

        // 301, 302, 303 change POST to GET per RFC 7231
        return HttpMethod.Get;
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

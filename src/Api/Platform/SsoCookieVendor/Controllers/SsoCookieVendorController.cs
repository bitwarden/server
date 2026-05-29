using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Platform.SsoCookieVendor;

/// <summary>
/// Provides an endpoint to read an SSO cookie and redirect to a custom URI
/// scheme. The load balancer/reverse proxy must be configured such that
/// requests to this endpoint do not have the auth cookie stripped.
/// </summary>
[Route("sso-cookie-vendor")]
[SelfHosted(SelfHostedOnly = true)]
public class SsoCookieVendorController(IGlobalSettings globalSettings, ILogger<SsoCookieVendorController> logger) : Controller
{
    private readonly IGlobalSettings _globalSettings = globalSettings;
    private readonly ILogger<SsoCookieVendorController> _logger = logger;
    private const int _maxShardCount = 20;
    private const int _maxUriLength = 8192;

    /// <summary>
    /// Reads SSO cookie (shards supported) and redirects to the bitwarden://
    /// URI with cookie value(s).
    /// </summary>
    /// <returns>
    /// 302 redirect on success, 404 if no cookies found, 400 if URI too long,
    /// 500 if misconfigured
    /// </returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var bootstrap = _globalSettings.Communication?.Bootstrap;
        if (string.IsNullOrEmpty(bootstrap) || !bootstrap.Equals("ssoCookieVendor", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("SSO cookie vendor endpoint reached but bootstrap is not configured.");
            return ResponseHTML(StatusCodes.Status404NotFound);
        }

        var cookieName = _globalSettings.Communication?.SsoCookieVendor?.CookieName;
        if (string.IsNullOrWhiteSpace(cookieName))
        {
            _logger.LogError("SSO cookie vendor is not properly configured: CookieName is missing.");
            return ResponseHTML(StatusCodes.Status500InternalServerError);
        }

        var uri = string.Empty;
        if (TryGetCookie(cookieName, out var cookie))
        {
            uri = BuildRedirectUri(cookie);
        }
        else if (TryGetShardedCookie(cookieName, out var shardedCookie))
        {
            uri = BuildRedirectUri(shardedCookie);
        }

        if (uri == string.Empty)
        {
            _logger.LogWarning("No SSO cookies found.");
            return ResponseHTML(StatusCodes.Status404NotFound);
        }

        if (uri.Length > _maxUriLength)
        {
            _logger.LogError("Generated SSO redirect URI exceeds maximum length of {MaxUriLength}.", _maxUriLength);
            return ResponseHTML(StatusCodes.Status400BadRequest);
        }

        return Redirect(uri);
    }

    private static ContentResult ResponseHTML(int statusCode) => new()
    {
        StatusCode = statusCode,
        ContentType = "text/html",
        Content = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Error</title></head>
            <body>
                <p>Error code {statusCode}. Please return to the Bitwarden app and try again.</p>
            </body>
            </html>
            """
    };

    private bool TryGetCookie(string cookieName, out Dictionary<string, string> cookie)
    {
        cookie = [];

        if (Request.Cookies.TryGetValue(cookieName, out var value) && !string.IsNullOrEmpty(value))
        {
            cookie[cookieName] = value;
            return true;
        }

        return false;
    }

    private bool TryGetShardedCookie(string cookieName, out Dictionary<string, string> cookies)
    {
        var shardedCookies = new Dictionary<string, string>();

        for (var i = 0; i < _maxShardCount; i++)
        {
            var shardName = $"{cookieName}-{i}";
            if (Request.Cookies.TryGetValue(shardName, out var value) && !string.IsNullOrEmpty(value))
            {
                shardedCookies[shardName] = value;
            }
            else
            {
                // Stop at first missing shard to maintain order integrity
                break;
            }
        }

        cookies = shardedCookies;
        return shardedCookies.Count > 0;
    }

    private static string BuildRedirectUri(Dictionary<string, string> cookies)
    {
        var queryParams = new List<string>();

        foreach (var kvp in cookies)
        {
            var encodedValue = Uri.EscapeDataString(kvp.Value);
            queryParams.Add($"{kvp.Key}={encodedValue}");
        }

        // Add a sentinel value so clients can detect a truncated URI, in the
        // event a user agent decides the URI is too long.
        queryParams.Add("d=1");

        return $"bitwarden://sso-cookie-vendor?{string.Join("&", queryParams)}";
    }
}

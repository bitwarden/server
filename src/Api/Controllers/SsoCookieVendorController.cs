using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

/// <summary>
/// Provides an endpoint to read an SSO cookie and redirect to a custom URI
/// scheme. The load balancer/reverse proxy must be configured such that
/// requests to this endpoint do not have the auth cookie stripped.
/// </summary>
[Route("sso-cookie-vendor")]
public class SsoCookieVendorController(IGlobalSettings globalSettings) : Controller
{
    private readonly IGlobalSettings _globalSettings = globalSettings;
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
            return NotFound();
        }

        var cookieName = _globalSettings.Communication?.SsoCookieVendor?.CookieName;
        if (string.IsNullOrWhiteSpace(cookieName))
        {
            return StatusCode(500, "SSO cookie vendor is not properly configured");
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
            return NotFound("No SSO cookies found");
        }

        if (uri.Length > _maxUriLength)
        {
            return BadRequest();
        }

        return Redirect(uri);
    }

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

        return $"bitwarden://sso_cookie_vendor?{string.Join("&", queryParams)}";
    }
}

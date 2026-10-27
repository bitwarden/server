using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bit.IntegrationTestCommon;

/// <summary>
/// Drives external authentication cookies through a host's registered cookie handler, so the host's
/// cookie manager, ticket data format, and session store all run exactly as they do for a real request.
/// </summary>
public static class ExternalCookieTestHelpers
{
    /// <summary>
    /// The OpenID Connect scheme that Identity challenges for SSO logins.
    /// </summary>
    private const string IdentitySsoChallengeScheme = "sso";

    /// <summary>
    /// Signs <paramref name="principal"/> in to the cookie <paramref name="scheme"/> of the host that owns
    /// <paramref name="services"/>, and returns the cookie that the host writes to the response.
    /// </summary>
    public static async Task<(string Name, string Value)> SignInCookieAsync(
        IServiceProvider services,
        string scheme,
        ClaimsPrincipal principal,
        AuthenticationProperties properties)
    {
        using var scope = services.CreateScope();
        var httpContext = CreateHttpContext(scope.ServiceProvider);

        await httpContext.SignInAsync(scheme, principal, properties);

        var cookieName = GetCookieName(services, scheme);
        var cookie = SetCookieHeaderValue.ParseList(httpContext.Response.Headers.SetCookie.ToArray()!)
            .Single(c => c.Name == cookieName);
        return (cookieName, cookie.Value.ToString());
    }

    /// <summary>
    /// Authenticates the cookie <paramref name="scheme"/> of the host that owns <paramref name="services"/>
    /// against a request that carries the given cookie.
    /// </summary>
    public static async Task<AuthenticateResult> AuthenticateCookieAsync(
        IServiceProvider services,
        string scheme,
        string cookieName,
        string cookieValue)
    {
        using var scope = services.CreateScope();
        var httpContext = CreateHttpContext(scope.ServiceProvider);
        httpContext.Request.Headers.Cookie = $"{cookieName}={cookieValue}";

        return await httpContext.AuthenticateAsync(scheme);
    }

    public static string GetCookieName(IServiceProvider services, string scheme) =>
        services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme).Cookie.Name
        ?? throw new InvalidOperationException($"Cookie scheme '{scheme}' has no cookie name.");

    /// <summary>
    /// The cookie scheme that Identity's SSO OpenID Connect handler signs external logins in to.
    /// </summary>
    public static string GetIdentityExternalScheme(this IdentityApplicationFactory factory) =>
        factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(IdentitySsoChallengeScheme).SignInScheme
        ?? throw new InvalidOperationException("Identity's SSO handler has no sign-in scheme.");

    /// <summary>
    /// Sends the cookie to Identity's SSO external callback endpoint.
    /// </summary>
    public static Task<HttpContext> GetSsoExternalCallbackAsync(
        this IdentityApplicationFactory factory,
        string cookieName,
        string cookieValue) =>
        factory.Server.GetAsync("/sso/ExternalCallback",
            httpContext => httpContext.Request.Headers.Cookie = $"{cookieName}={cookieValue}");

    /// <summary>
    /// True when <paramref name="response"/> sets Identity's IdentityServer login session cookie.
    /// </summary>
    public static bool SetsIdentityServerSessionCookie(this IdentityApplicationFactory factory, HttpContext response)
    {
        var sessionCookieName = GetCookieName(factory.Services, IdentityServerConstants.DefaultCookieAuthenticationScheme);
        return SetCookieHeaderValue.ParseList(response.Response.Headers.SetCookie.ToArray()!)
            .Any(c => c.Name == sessionCookieName && !string.IsNullOrEmpty(c.Value.ToString()));
    }

    /// <summary>
    /// Runs the web client's authorization code flow with the cookies that <paramref name="response"/> sets, and
    /// returns the subject of the access token that the flow issues. Returns null when the flow issues no token.
    /// </summary>
    public static async Task<string?> GetAccessTokenSubjectAsync(
        this IdentityApplicationFactory factory,
        HttpContext response)
    {
        var cookieHeader = string.Join("; ",
            SetCookieHeaderValue.ParseList(response.Response.Headers.SetCookie.ToArray()!)
                .Where(c => !string.IsNullOrEmpty(c.Value.ToString()))
                .Select(c => $"{c.Name}={c.Value}"));

        var redirectUri = $"{factory.Services.GetRequiredService<GlobalSettings>().BaseServiceUri.Vault}/sso-connector.html";
        var codeVerifier = CryptoRandom.CreateUniqueId(32);
        var codeChallenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        var authorize = await factory.Server.GetAsync("/connect/authorize", httpContext =>
        {
            httpContext.Request.QueryString = QueryString.Create(new Dictionary<string, string?>
            {
                ["client_id"] = "web",
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "api offline_access",
                ["state"] = "test-state",
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
            });
            httpContext.Request.Headers.Cookie = cookieHeader;
        });

        var location = authorize.Response.Headers.Location.ToString();
        if (!location.StartsWith(redirectUri, StringComparison.Ordinal))
        {
            return null;
        }

        var code = QueryHelpers.ParseQuery(new Uri(location).Query)["code"].ToString();
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        var token = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "web",
                ["code"] = code,
                ["code_verifier"] = codeVerifier,
                ["redirect_uri"] = redirectUri,
                ["deviceType"] = ((int)DeviceType.FirefoxBrowser).ToString(CultureInfo.InvariantCulture),
                ["deviceIdentifier"] = IdentityApplicationFactory.DefaultDeviceIdentifier,
                ["deviceName"] = "firefox",
            }));
        if (token.Response.StatusCode != StatusCodes.Status200OK)
        {
            return null;
        }

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(token);
        var accessToken = body.RootElement.GetProperty("access_token").GetString()!;
        using var payload = JsonDocument.Parse(WebEncoders.Base64UrlDecode(accessToken.Split('.')[1]));
        return payload.RootElement.GetProperty(JwtClaimTypes.Subject).GetString();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider requestServices)
    {
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        return httpContext;
    }
}

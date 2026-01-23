// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Globalization;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Http;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity.TokenProviders;

/// <summary>
/// OrganizationDuo and Duo TwoFactorProviderTypes both use the same flows so both of those Token Providers will
/// have this class injected to utilize these methods
/// </summary>
public interface IDuoUniversalTokenService
{
    /// <summary>
    /// Generates the Duo Auth URL for the user to be redirected to Duo for 2FA. This
    /// Auth URL also lets the Duo Service know where to redirect the user back to after
    /// the 2FA process is complete.
    /// </summary>
    /// <param name="duoClient">A not null valid Duo.Client</param>
    /// <param name="tokenDataFactory">This service creates the state token for added security</param>
    /// <param name="user">currently active user</param>
    /// <returns>a URL in string format</returns>
    string GenerateAuthUrl(
        Duo.Client duoClient,
        IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
        User user);

    /// <summary>
    /// Makes the request to Duo to validate the authCode and state token
    /// </summary>
    /// <param name="duoClient">A not null valid Duo.Client</param>
    /// <param name="tokenDataFactory">Factory for decrypting the state</param>
    /// <param name="user">self</param>
    /// <param name="token">token received from the client</param>
    /// <returns>boolean based on result from Duo</returns>
    Task<bool> RequestDuoValidationAsync(
        Duo.Client duoClient,
        IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
        User user,
        string token);

    /// <summary>
    /// Generates a Duo.Client object for use with Duo SDK v4. This method is to validate a Duo configuration
    /// when adding or updating the configuration. This method makes a web request to Duo to verify the configuration.
    /// Throws exception if configuration is invalid.
    /// </summary>
    /// <param name="clientSecret">Duo client Secret</param>
    /// <param name="clientId">Duo client Id</param>
    /// <param name="host">Duo host</param>
    /// <returns>Boolean</returns>
    Task<bool> ValidateDuoConfiguration(string clientSecret, string clientId, string host);

    /// <summary>
    /// Checks provider for the correct Duo metadata: ClientId, ClientSecret, and Host. Does no validation on the data.
    /// it is assumed to be correct. The only way to have the data written to the Database is after verification
    /// occurs.
    /// </summary>
    /// <param name="provider">Host being checked for proper data</param>
    /// <returns>true if all three are present; false if one is missing or the host is incorrect</returns>
    bool HasProperDuoMetadata(TwoFactorProvider provider);

    /// <summary>
    /// Generates a Duo.Client object for use with Duo SDK v4. This combines the health check and the client generation.
    /// This method is made public so that it is easier to test. If the method was private then there would not be an
    /// easy way to mock the response. Since this makes a web request it is difficult to mock.
    /// </summary>
    /// <param name="provider">TwoFactorProvider Duo or OrganizationDuo</param>
    /// <returns>Duo.Client object or null</returns>
    Task<Duo.Client> BuildDuoTwoFactorClientAsync(TwoFactorProvider provider);

    /// <summary>
    /// Builds the redirect URI for Duo authentication based on the client type and request context.
    /// Mobile clients include a deeplinkScheme parameter (https for cloud, bitwarden for self-hosted).
    /// Desktop clients always use the bitwarden scheme.
    /// Other clients (web, browser, cli) do not include the deeplinkScheme parameter.
    /// </summary>
    /// <returns>The redirect URI to be used for Duo authentication</returns>
    string BuildDuoTwoFactorRedirectUri();
}

public class DuoUniversalTokenService(
    ICurrentContext currentContext,
    GlobalSettings globalSettings) : IDuoUniversalTokenService
{
    private readonly ICurrentContext _currentContext = currentContext;
    private readonly GlobalSettings _globalSettings = globalSettings;

    public string GenerateAuthUrl(
        Duo.Client duoClient,
        IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
        User user)
    {
        var state = tokenDataFactory.Protect(new DuoUserStateTokenable(user));
        var authUrl = duoClient.GenerateAuthUri(user.Email, state);

        return authUrl;
    }

    public async Task<bool> RequestDuoValidationAsync(
        Duo.Client duoClient,
        IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
        User user,
        string token)
    {
        var parts = token.Split("|");
        var authCode = parts[0];
        var state = parts[1];
        tokenDataFactory.TryUnprotect(state, out var tokenable);
        if (!tokenable.Valid || !tokenable.TokenIsValid(user))
        {
            return false;
        }

        // duoClient compares the email from the received IdToken with user.Email to verify a bad actor hasn't used
        // their authCode with a victims credentials
        var res = await duoClient.ExchangeAuthorizationCodeFor2faResult(authCode, user.Email);
        // If the result of the exchange doesn't throw an exception and it's not null, then it's valid
        return res.AuthResult.Result == "allow";
    }

    public async Task<bool> ValidateDuoConfiguration(string clientSecret, string clientId, string host)
    {
        // Do some simple checks to ensure data integrity
        if (!ValidDuoHost(host) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }
        // The AuthURI is not important for this health check so we pass in a non-empty string
        var client = new Duo.ClientBuilder(clientId, clientSecret, host, "non-empty").Build();

        // This could throw an exception, the false flag will allow the exception to bubble up
        return await client.DoHealthCheck(false);
    }

    public bool HasProperDuoMetadata(TwoFactorProvider provider)
    {
        return provider?.MetaData != null &&
               provider.MetaData.ContainsKey("ClientId") &&
               provider.MetaData.ContainsKey("ClientSecret") &&
               provider.MetaData.ContainsKey("Host") &&
               ValidDuoHost((string)provider.MetaData["Host"]);
    }


    /// <summary>
    /// Checks the host string to make sure it meets Duo's Guidelines before attempting to create a Duo.Client.
    /// </summary>
    /// <param name="host">string representing the Duo Host</param>
    /// <returns>true if the host is valid false otherwise</returns>
    public static bool ValidDuoHost(string host)
    {
        if (Uri.TryCreate($"https://{host}", UriKind.Absolute, out var uri))
        {
            return (string.IsNullOrWhiteSpace(uri.PathAndQuery) || uri.PathAndQuery == "/") &&
                uri.Host.StartsWith("api-") &&
                (uri.Host.EndsWith(".duosecurity.com") || uri.Host.EndsWith(".duofederal.com"));
        }
        return false;
    }

    private static bool IsBitwardenCloudHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalizedHost = host.ToLowerInvariant();
        return normalizedHost.EndsWith("bitwarden.com") ||
               normalizedHost.EndsWith("bitwarden.eu") ||
               normalizedHost.EndsWith("bitwarden.pw");
    }

    private static bool IsLocalRequestHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalizedHost = host.ToLowerInvariant();
        return normalizedHost == "localhost" ||
               normalizedHost == "127.0.0.1" ||
               normalizedHost == "::1" ||
               normalizedHost.EndsWith(".localhost");
    }

    private static DeeplinkScheme? GetDeeplinkSchemeOverride(HttpContext httpContext)
    {
        if (httpContext == null)
        {
            return null;
        }

        var host = httpContext.Request?.Host.Host;
        // Only allow overrides when developing/testing locally to avoid abuse in production
        if (!IsLocalRequestHost(host))
        {
            return null;
        }

        // Querystring has precedence over header for manual local testing
        var overrideFromQuery = httpContext.Request?.Query["deeplinkScheme"].FirstOrDefault();
        var overrideFromHeader = httpContext.Request?.Headers["Bitwarden-Deeplink-Scheme"].FirstOrDefault();
        var candidate = (overrideFromQuery ?? overrideFromHeader)?.Trim();

        // Allow only the two supported values
        return Enum.TryParse<DeeplinkScheme>(candidate, ignoreCase: true, out var scheme) ? scheme : null;
    }

    public string BuildDuoTwoFactorRedirectUri()
    {
        // Fetch Client name from header value since duo auth can be initiated from multiple clients and we want
        // to redirect back to the initiating client
        _currentContext.HttpContext.Request.Headers.TryGetValue("Bitwarden-Client-Name", out var bitwardenClientName);
        var clientTypeHeader = bitwardenClientName.FirstOrDefault();
        var clientType = Enum.TryParse<ClientType>(clientTypeHeader, ignoreCase: true, out var parsedClientType)
            ? parsedClientType
            : ClientType.Web;
        var clientName = clientType.ToString().ToLowerInvariant();

        // Handle mobile case separately because mobile needs to define the scheme ahead of time
        // for security reasons.
        if (clientType == ClientType.Mobile)
        {
            var requestHost = _currentContext.HttpContext.Request.Host.Host;
            var deeplinkScheme = GetDeeplinkSchemeOverride(_currentContext.HttpContext) ??
                (IsBitwardenCloudHost(requestHost) ? DeeplinkScheme.Https : DeeplinkScheme.Bitwarden);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/duo-redirect-connector.html?client={1}&deeplinkScheme={2}",
                _globalSettings.BaseServiceUri.Vault, clientName, deeplinkScheme.ToString().ToLowerInvariant());
        }

        // Explicitly have the desktop client use the bitwarden scheme. See the complimentary
        // duo web connector in the client project.
        if (clientType == ClientType.Desktop)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}/duo-redirect-connector.html?client={1}&deeplinkScheme={2}",
                _globalSettings.BaseServiceUri.Vault, clientName, DeeplinkScheme.Bitwarden.ToString().ToLowerInvariant());
        }

        // All other clients will not provide an explicit handling. See the complimentary
        // duo web connector in the client project to understand how defaulting is handled.
        return string.Format(CultureInfo.InvariantCulture,
            "{0}/duo-redirect-connector.html?client={1}",
            _globalSettings.BaseServiceUri.Vault, clientName);
    }

    public async Task<Duo.Client> BuildDuoTwoFactorClientAsync(TwoFactorProvider provider)
    {
        var redirectUri = BuildDuoTwoFactorRedirectUri();

        var client = new Duo.ClientBuilder(
            (string)provider.MetaData["ClientId"],
            (string)provider.MetaData["ClientSecret"],
            (string)provider.MetaData["Host"],
            redirectUri).Build();

        if (!await client.DoHealthCheck(false))
        {
            return null;
        }
        return client;
    }
}

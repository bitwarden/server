using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity;

/// <summary>
/// In the TwoFactorController before we write a configuration to the database we check the configuration
/// this interface creates a simple way to inject the process into those endpoints.
/// </summary>
public interface IDuoTokenProvider
{
    Task<bool> ValidateDuoConfiguration(string clientId, string clientSecret, string host);
}

/// <summary>
/// OrganizationDuo and Duo types both use the same flows so both of those Token Providers will 
/// inherit from this class
/// </summary>
public class DuoTokenProvider : IDuoTokenProvider
{
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;

    public DuoTokenProvider(ICurrentContext currentContext, GlobalSettings globalSettings)
    {
        _currentContext = currentContext;
        _globalSettings = globalSettings;
    }

    protected async Task<string> GenerateAuthUrlAsync(
        TwoFactorProvider provider,
        IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
        User user)
    {
        var duoClient = await BuildDuoTwoFactorClientAsync(provider);
        if (duoClient == null)
        {
            return null;
        }

        var state = tokenDataFactory.Protect(new DuoUserStateTokenable(user));
        var authUrl = duoClient.GenerateAuthUri(user.Email, state);

        return authUrl;
    }

    /// <summary>
    /// Makes the request to Duo to validate the authCode and state token
    /// </summary>
    /// <param name="provider">Duo or OrganizationDuo</param>
    /// <param name="tokenDataFactory">Factory for decrypting the state</param>
    /// <param name="user">self</param>
    /// <param name="token">token received from the client</param>
    /// <returns>boolean based on result from Duo</returns>
    protected async Task<bool> RequestDuoValidationAsync(
        TwoFactorProvider provider,
        IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory,
        User user,
        string token)
    {
        var duoClient = await BuildDuoTwoFactorClientAsync(provider);
        if (duoClient == null)
        {
            return false;
        }

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

    /// <summary>
    /// Generates a Duo.Client object for use with Duo SDK v4. This combines the health check and the client generation
    /// </summary>
    /// <param name="provider">TwoFactorProvider Duo or OrganizationDuo</param>
    /// <returns>Duo.Client object or null</returns>
    protected async Task<Duo.Client> BuildDuoTwoFactorClientAsync(TwoFactorProvider provider)
    {
        // Fetch Client name from header value since duo auth can be initiated from multiple clients and we want
        // to redirect back to the initiating client
        _currentContext.HttpContext.Request.Headers.TryGetValue("Bitwarden-Client-Name", out var bitwardenClientName);
        var redirectUri = string.Format("{0}/duo-redirect-connector.html?client={1}",
            _globalSettings.BaseServiceUri.Vault, bitwardenClientName.FirstOrDefault() ?? "web");

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

    /// <summary>
    /// Generates a Duo.Client object for use with Duo SDK v4. This combines the health check and the client generation
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="clientSecret"></param>
    /// <param name="host"></param>
    /// <returns></returns>
    public async Task<bool> ValidateDuoConfiguration(string clientId, string clientSecret, string host)
    {
        // The AuthURI isn't important for this health check so we pass in a non-empty string
        var client = new Duo.ClientBuilder(clientId, clientSecret, host, "non-empty").Build();

        return await client.DoHealthCheck(false);
    }
}

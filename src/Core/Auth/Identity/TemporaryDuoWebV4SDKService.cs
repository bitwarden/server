using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity;

/*
    PM-5156 addresses tech debt
    Interface to allow for DI, will end up being removed as part of the removal of the old Duo SDK v2 flows.
    This service is to support SDK v4 flows for Duo. At some time in the future we will need
    to combine this service with the DuoWebTokenProvider and OrganizationDuoWebTokenProvider to support SDK v4.
*/
public interface ITemporaryDuoWebV4SDKService
{
    Task<string> GenerateAsync(TwoFactorProvider provider, User user);
    Task<bool> ValidateAsync(string token, TwoFactorProvider provider, User user);
}

public class TemporaryDuoWebV4SDKService : ITemporaryDuoWebV4SDKService
{
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IDataProtectorTokenFactory<DuoUserStateTokenable> _tokenDataFactory;

    /// <summary>
    /// Constructor for the DuoUniversalPromptService. Used to supplement v2 implementation of Duo with v4 SDK
    /// </summary>
    /// <param name="currentContext">used to fetch initiating Client</param>
    /// <param name="globalSettings">used to fetch vault URL for Redirect URL</param>
    public TemporaryDuoWebV4SDKService(
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IDataProtectorTokenFactory<DuoUserStateTokenable> tokenDataFactory)
    {
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _tokenDataFactory = tokenDataFactory;
    }

    /// <summary>
    /// Provider agnostic (either Duo or OrganizationDuo) method to generate a Duo Auth URL
    /// </summary>
    /// <param name="provider">Either Duo or OrganizationDuo</param>
    /// <param name="user">self</param>
    /// <returns>AuthUrl for DUO SDK v4</returns>
    public async Task<string> GenerateAsync(TwoFactorProvider provider, User user)
    {
        if (!HasProperMetaData(provider))
        {
            return null;
        }


        var duoClient = await BuildDuoClientAsync(provider);
        if (duoClient == null)
        {
            return null;
        }

        var state = _tokenDataFactory.Protect(new DuoUserStateTokenable(user));
        var authUrl = duoClient.GenerateAuthUri(user.Email, state);

        return authUrl;
    }

    /// <summary>
    /// Validates Duo SDK v4 response
    /// </summary>
    /// <param name="token">response form Duo</param>
    /// <param name="provider">TwoFactorProviderType Duo or OrganizationDuo</param>
    /// <param name="user">self</param>
    /// <returns>true or false depending on result of verification</returns>
    public async Task<bool> ValidateAsync(string token, TwoFactorProvider provider, User user)
    {
        if (!HasProperMetaData(provider))
        {
            return false;
        }

        var duoClient = await BuildDuoClientAsync(provider);
        if (duoClient == null)
        {
            return false;
        }

        var parts = token.Split("|");
        var authCode = parts[0];
        var state = parts[1];

        _tokenDataFactory.TryUnprotect(state, out var tokenable);
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

    private bool HasProperMetaData(TwoFactorProvider provider)
    {
        return provider?.MetaData != null && provider.MetaData.ContainsKey("IKey") &&
            provider.MetaData.ContainsKey("SKey") && provider.MetaData.ContainsKey("Host");
    }

    /// <summary>
    /// Generates a Duo.Client object for use with Duo SDK v4. This combines the health check and the client generation
    /// </summary>
    /// <param name="provider">TwoFactorProvider Duo or OrganizationDuo</param>
    /// <returns>Duo.Client object or null</returns>
    private async Task<Duo.Client> BuildDuoClientAsync(TwoFactorProvider provider)
    {
        // Fetch Client name from header value since duo auth can be initiated from multiple clients and we want
        // to redirect back to the correct client
        _currentContext.HttpContext.Request.Headers.TryGetValue("Bitwarden-Client-Name", out var bitwardenClientName);
        var redirectUri = string.Format("{0}/duo-redirect-connector.html?client={1}",
            _globalSettings.BaseServiceUri.Vault, bitwardenClientName.FirstOrDefault() ?? "web");

        var client = new Duo.ClientBuilder(
            (string)provider.MetaData["IKey"],
            (string)provider.MetaData["SKey"],
            (string)provider.MetaData["Host"],
            redirectUri).Build();

        if (!await client.DoHealthCheck(true))
        {
            return null;
        }
        return client;
    }
}

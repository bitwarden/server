using Bit.Core.Auth.Models;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Duo = DuoUniversal;

namespace Bit.Core.Auth.Identity;

/* 
    PM-5156 addresses tech debt
    Interface to allow for DI, will end up being removed as part of the removal of the old Duo SDK v2 flows.
    This service is to support SDK v4 flows for Duo. At some time in the future we will need
    to combine this service with the DuoWebTokenProvider and OrganizationDuoWebTokenProvider to support SDK v4.
*/
public interface IDuoUniversalPromptService
{
    Task<string> GenerateAsync(TwoFactorProvider provider, User user);
    Task<bool> ValidateAsync(string token, TwoFactorProvider provider, User user);
}

public class DuoUniversalPromptService : IDuoUniversalPromptService
{
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;

    /// <summary>
    /// Constructor for the DuoUniversalPromptService. Used to supplement v2 implementation of Duo with v4 SDK
    /// </summary>
    /// <param name="currentContext">used to fetch initiating Client</param>
    /// <param name="globalSettings">used to fetch vault URL for Redirect URL</param>
    public DuoUniversalPromptService(
        ICurrentContext currentContext,
        GlobalSettings globalSettings)
    {
        _currentContext = currentContext;
        _globalSettings = globalSettings;
    }

    /// <summary>
    /// Provider agnostic (either Duo or OrganizationDuo) method to generate a Duo Auth URL
    /// </summary>
    /// <param name="provider">Either Duo or OrganizationDuo</param>
    /// <param name="user">self</param>
    /// <returns>AuthUrl for DUO SDK v4</returns>
    public async Task<string> GenerateAsync(TwoFactorProvider provider, User user)
    {
        if (!HasProperMetaData(provider)) return null;

        var duoClient = await BuildDuoClientAsync(provider);
        if (duoClient == null) return null;

        var state = Duo.Client.GenerateState(); //? Not sure on this yet. But required for GenerateAuthUrl
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
        if (!HasProperMetaData(provider)) return false;

        var duoClient = await BuildDuoClientAsync(provider);
        if (duoClient == null) return false;

        // If the result of the exchange doesn't throw an exception and it's not null, then it's valid
        return duoClient.ExchangeAuthorizationCodeFor2faResult(token, user.Email) != null;
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
    /// <returns></returns>
    private async Task<Duo.Client> BuildDuoClientAsync(TwoFactorProvider provider)
    {
        // Fetch Client name from header value since duo auth can be initiated from multiple clients and we want 
        // to redirect back to the correct client
        _currentContext.HttpContext.Request.Headers.TryGetValue("Bitwarden-Client-Name", out var bitwardenClientName);
        var redirectUri = string.Format("{0}/duo-redirect-connector?client={1}",
            _globalSettings.BaseServiceUri.Vault, bitwardenClientName.FirstOrDefault() ?? "web");

        var client = new Duo.ClientBuilder(
            (string)provider.MetaData["IKey"],
            (string)provider.MetaData["SKey"],
            (string)provider.MetaData["Host"],
            redirectUri).Build();

        if (!await client.DoHealthCheck())
        {
            return null;
        }
        return client;
    }
}

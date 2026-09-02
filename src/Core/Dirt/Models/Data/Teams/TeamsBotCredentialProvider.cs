using Microsoft.Bot.Connector.Authentication;

namespace Bit.Core.Dirt.Models.Data.Teams;

public class TeamsBotCredentialProvider(string clientId, string clientSecret) : ICredentialProvider
{
    private const string _microsoftBotFrameworkIssuer = AuthenticationConstants.ToBotFromChannelTokenIssuer;

    public Task<bool> IsValidAppIdAsync(string appId)
    {
        return Task.FromResult(appId == clientId);
    }

    public Task<string?> GetAppPasswordAsync(string appId)
    {
        return Task.FromResult(appId == clientId ? clientSecret : null);
    }

    public Task<bool> IsAuthenticationDisabledAsync()
    {
        return Task.FromResult(false);
    }

    public Task<bool> ValidateIssuerAsync(string issuer)
    {
        return Task.FromResult(issuer == _microsoftBotFrameworkIssuer);
    }
}

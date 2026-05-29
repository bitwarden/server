using Bit.Core.Dirt.Models.Data.Teams;

namespace Bit.Core.Dirt.Services.NoopImplementations;

public class NoopTeamsService : ITeamsService
{
    public string GetRedirectUrl(string callbackUrl, string state)
    {
        return string.Empty;
    }

    public Task<string> ObtainTokenViaOAuth(string code, string redirectUrl)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<IReadOnlyList<TeamInfo>> GetJoinedTeamsAsync(string accessToken)
    {
        return Task.FromResult<IReadOnlyList<TeamInfo>>(Array.Empty<TeamInfo>());
    }

    public Task SendMessageToChannelAsync(Uri serviceUri, string channelId, string message)
    {
        return Task.CompletedTask;
    }
}

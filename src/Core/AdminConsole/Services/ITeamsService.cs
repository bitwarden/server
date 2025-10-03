using Bit.Core.Models.Teams;

namespace Bit.Core.Services;

public interface ITeamsService
{
    string GetRedirectUrl(string callbackUrl, string state);
    Task<string> ObtainTokenViaOAuth(string code, string redirectUrl);
    Task<IReadOnlyList<TeamInfo>> GetJoinedTeamsAsync(string accessToken);
    Task SendMessageToChannelAsync(Uri serviceUri, string channelId, string message);
}

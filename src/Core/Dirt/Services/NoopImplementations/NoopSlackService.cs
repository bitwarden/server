using Bit.Core.Dirt.Models.Data.Slack;

namespace Bit.Core.Dirt.Services.NoopImplementations;

public class NoopSlackService : ISlackService
{
    public Task<string> GetChannelIdAsync(string token, string channelName)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<List<string>> GetChannelIdsAsync(string token, List<string> channelNames)
    {
        return Task.FromResult(new List<string>());
    }

    public Task<string> GetDmChannelByEmailAsync(string token, string email)
    {
        return Task.FromResult(string.Empty);
    }

    public string GetRedirectUrl(string callbackUrl, string state)
    {
        return string.Empty;
    }

    public Task<SlackSendMessageResponse?> SendSlackMessageByChannelIdAsync(string token, string message,
        string channelId)
    {
        return Task.FromResult<SlackSendMessageResponse?>(null);
    }

    public Task<string> ObtainTokenViaOAuth(string code, string redirectUrl)
    {
        return Task.FromResult(string.Empty);
    }
}

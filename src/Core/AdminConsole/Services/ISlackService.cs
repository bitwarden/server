namespace Bit.Core.Services;

public interface ISlackService
{
    Task<string> GetChannelIdAsync(string token, string channelName);
    Task<List<string>> GetChannelIdsAsync(string token, List<string> channelNames);
    Task<string> GetDmChannelByEmailAsync(string token, string email);
    string GetRedirectUrl(string callbackUrl, string state);
    Task<string> ObtainTokenViaOAuth(string code, string redirectUrl);
    Task SendSlackMessageByChannelIdAsync(string token, string message, string channelId);
}

namespace Bit.Core.Services;

public interface ISlackService
{
    Task<string> GetChannelIdAsync(string token, string channelName);
    Task<List<string>> GetChannelIdsAsync(string token, List<string> channelNames);
    Task<string> GetDmChannelByEmailAsync(string token, string email);
    Task SendSlackMessageByChannelId(string token, string message, string channelId);
}

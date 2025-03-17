namespace Bit.Core.Models.Data.Integrations;

public class SlackConfiguration
{
    public SlackConfiguration()
    {
    }

    public SlackConfiguration(string channelId, string token)
    {
        ChannelId = channelId;
        Token = token;
    }

    public string Token { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
}

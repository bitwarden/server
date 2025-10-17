using System.Text.Json.Serialization;

namespace Bit.Core.Models.Slack;

public abstract class SlackApiResponse
{
    public bool Ok { get; set; }
    [JsonPropertyName("response_metadata")]
    public SlackResponseMetadata ResponseMetadata { get; set; } = new();
    public string Error { get; set; } = string.Empty;
}

public class SlackResponseMetadata
{
    [JsonPropertyName("next_cursor")]
    public string NextCursor { get; set; } = string.Empty;
}

public class SlackChannelListResponse : SlackApiResponse
{
    public List<SlackChannel> Channels { get; set; } = new();
}

public class SlackUserResponse : SlackApiResponse
{
    public SlackUser User { get; set; } = new();
}

public class SlackOAuthResponse : SlackApiResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    public SlackTeam Team { get; set; } = new();
}

public class SlackTeam
{
    public string Id { get; set; } = string.Empty;
}

public class SlackChannel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SlackUser
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SlackDmResponse : SlackApiResponse
{
    public SlackChannel Channel { get; set; } = new();
}

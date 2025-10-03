using System.Text.Json.Serialization;

namespace Bit.Core.Models.Teams;

public class TeamsOAuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
}

public class JoinedTeamsResponse
{
    [JsonPropertyName("value")]
    public List<TeamInfo> Value { get; set; } = new();
}

public class TeamInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;
}

using System.Text.Json.Serialization;

namespace Bit.Core.Models.Teams;

/// <summary>Represents the response returned by the Microsoft OAuth 2.0 token endpoint.
/// See <see href="https://learn.microsoft.com/graph/auth-v2-user">Microsoft identity platform and OAuth 2.0
/// authorization code flow</see>.</summary>
public class TeamsOAuthResponse
{
    /// <summary>The access token issued by Microsoft, used to call the Microsoft Graph API.</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
}

/// <summary>Represents the response from the <c>/me/joinedTeams</c> Microsoft Graph API call.
/// See <see href="https://learn.microsoft.com/graph/api/user-list-joinedteams">List joined teams -
/// Microsoft Graph v1.0</see>.</summary>
public class JoinedTeamsResponse
{
    /// <summary>The collection of teams that the user has joined.</summary>
    [JsonPropertyName("value")]
    public List<TeamInfo> Value { get; set; } = [];
}

/// <summary>Represents a Microsoft Teams team returned by the Graph API.
/// See <see href="https://learn.microsoft.com/graph/api/resources/team">Team resource type -
/// Microsoft Graph v1.0</see>.</summary>
public class TeamInfo
{
    /// <summary>The unique identifier of the team.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The name of the team.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The ID of the Microsoft Entra tenant for this team.</summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;
}

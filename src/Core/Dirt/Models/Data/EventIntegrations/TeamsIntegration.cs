using System.Text.Json;
using Bit.Core.Dirt.Models.Data.Teams;

namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public record TeamsIntegration(
    string TenantId,
    IReadOnlyList<TeamInfo> Teams,
    string? ChannelId = null,
    Uri? ServiceUrl = null,
    DateTime? DisconnectedDate = null)
{
    /// <summary>True when the integration is connected to a channel and able to deliver events.</summary>
    public bool IsCompleted =>
        !string.IsNullOrEmpty(ChannelId) && ServiceUrl is not null && DisconnectedDate is null;

    /// <summary>
    /// True when the Teams app was removed on Microsoft's side. The tenant and team list are retained so the
    /// owner can reconnect by re-installing the app without repeating the OAuth flow.
    /// </summary>
    public bool NeedsReconnection => DisconnectedDate is not null;

    /// <summary>
    /// Deserializes an <see cref="Bit.Core.Dirt.Entities.OrganizationIntegration.Configuration"/> value, returning
    /// null when it is absent or malformed rather than throwing.
    /// </summary>
    public static TeamsIntegration? FromConfiguration(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TeamsIntegration>(configuration);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

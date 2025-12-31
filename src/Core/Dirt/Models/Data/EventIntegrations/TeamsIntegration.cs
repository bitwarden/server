using Bit.Core.Dirt.Models.Data.Teams;

namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

public record TeamsIntegration(
    string TenantId,
    IReadOnlyList<TeamInfo> Teams,
    string? ChannelId = null,
    Uri? ServiceUrl = null)
{
    public bool IsCompleted => !string.IsNullOrEmpty(ChannelId) && ServiceUrl is not null;
}

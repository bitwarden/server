using Bit.Core.Dirt.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationIntegrationRepository : IRepository<OrganizationIntegration, Guid>
{
    Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId);

    Task<OrganizationIntegration?> GetByTeamsConfigurationTenantIdTeamId(string tenantId, string teamId);

    /// <summary>
    /// Reads the Teams integration for the given tenant and team that is currently connected to a channel — i.e.
    /// it has a ChannelId and ServiceUrl and has not been disconnected. Used to locate the record to tear down
    /// when Microsoft reports that the app was removed.
    /// </summary>
    Task<OrganizationIntegration?> GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(string tenantId, string teamId);
}

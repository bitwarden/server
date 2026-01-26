using Bit.Core.Dirt.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationIntegrationRepository : IRepository<OrganizationIntegration, Guid>
{
    Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId);

    Task<OrganizationIntegration?> GetByTeamsConfigurationTenantIdTeamId(string tenantId, string teamId);
}

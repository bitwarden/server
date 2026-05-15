using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationIntegrationRepository : IRepository<OrganizationIntegration, Guid>
{
    Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId);

    Task<OrganizationIntegration?> GetByTeamsConfigurationTenantIdTeamId(string tenantId, string teamId);

    Task<OrganizationIntegration?> GetByOrganizationIdTypeAsync(Guid organizationId, IntegrationType type);
}

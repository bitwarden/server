using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationRepository : IRepository<OrganizationIntegration, Guid>
{
    Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId);

    Task<OrganizationIntegration?> GetByTenantIdTeamId(string tenantId, string teamId);
}

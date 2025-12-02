using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;

public interface IGetOrganizationIntegrationsQuery
{
    Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId);
}

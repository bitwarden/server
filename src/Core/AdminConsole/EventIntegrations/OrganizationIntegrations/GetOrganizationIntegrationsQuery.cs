using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations;

/// <summary>
/// Query implementation for retrieving organization integrations.
/// </summary>
public class GetOrganizationIntegrationsQuery(IOrganizationIntegrationRepository integrationRepository)
    : IGetOrganizationIntegrationsQuery
{
    public async Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId)
    {
        var integrations = await integrationRepository.GetManyByOrganizationAsync(organizationId);
        return integrations.ToList();
    }
}

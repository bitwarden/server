using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Dirt.Repositories;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations;

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

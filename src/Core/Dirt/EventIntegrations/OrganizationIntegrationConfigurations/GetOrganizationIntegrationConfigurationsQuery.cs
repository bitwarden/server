using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations;

/// <summary>
/// Query implementation for retrieving organization integration configurations.
/// </summary>
public class GetOrganizationIntegrationConfigurationsQuery(
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository)
    : IGetOrganizationIntegrationConfigurationsQuery
{
    public async Task<List<OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(
        Guid organizationId,
        Guid integrationId)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var configurations = await configurationRepository.GetManyByIntegrationAsync(integrationId);
        return configurations.ToList();
    }
}

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;

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

using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

public interface IGetOrganizationIntegrationConfigurationsQuery
{
    Task<List<OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(Guid organizationId, Guid integrationId);
}

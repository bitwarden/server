using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationConfigurationRepository : IRepository<OrganizationIntegrationConfiguration, Guid>
{
    Task<List<OrganizationIntegrationConfiguration>> GetConfigurationsAsync(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType);
}

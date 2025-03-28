using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationConfigurationRepository : IRepository<OrganizationIntegrationConfiguration, Guid>
{
    Task<List<OrganizationIntegrationConfigurationDetails>> GetConfigurationsAsync(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType);
}

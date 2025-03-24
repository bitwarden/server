using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationConfigurationRepository
{
    Task<List<IntegrationConfiguration<T>>> GetConfigurationsAsync<T>(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType);

    Task CreateOrganizationIntegrationAsync<T>(
        Guid organizationId,
        IntegrationType integrationType,
        T configuration);
}

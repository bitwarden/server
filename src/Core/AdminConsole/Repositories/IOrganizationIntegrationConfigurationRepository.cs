using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;

#nullable enable

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationConfigurationRepository
{
    Task<List<IntegrationConfiguration<T>>> GetConfigurationsAsync<T>(IntegrationType integrationType,
        Guid organizationId, EventType eventType);
}

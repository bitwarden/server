using Bit.Core.Enums;
using Bit.Core.Models.Data.Integrations;

#nullable enable

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationConfigurationRepository
{
    Task<IntegrationConfiguration<T>?> GetConfigurationAsync<T>(Guid organizationId, IntegrationType integrationType, EventType eventType);
    Task<IEnumerable<IntegrationConfiguration<T>>> GetAllConfigurationsAsync<T>(Guid organizationId);
    Task AddConfigurationAsync<T>(Guid organizationId, IntegrationType integrationType, EventType eventType, IntegrationConfiguration<T> configuration);
    Task UpdateConfigurationAsync<T>(IntegrationConfiguration<T> configuration);
    Task DeleteConfigurationAsync(Guid id);
}

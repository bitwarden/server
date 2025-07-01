#nullable enable

using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;

namespace Bit.Core.Services;

public interface IIntegrationConfigurationDetailsCache
{
    Task<IReadOnlyList<CachedIntegrationConfigurationDetails<T>>> GetOrAddAsync<T>(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType);

    void RemoveCacheEntry(Guid organizationId, IntegrationType integrationType, EventType eventType);
    void RemoveCacheEntriesForIntegration(Guid organizationId, IntegrationType integrationType);
}

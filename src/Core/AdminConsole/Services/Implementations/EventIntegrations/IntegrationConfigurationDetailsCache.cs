#nullable enable

using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class IntegrationConfigurationDetailsCache(
    [FromKeyedServices("integration-cache")] IMemoryCache cache,
    IOrganizationIntegrationConfigurationRepository repository,
    GlobalSettings globalSettings,
    ILogger<IntegrationConfigurationDetailsCache> logger)
    : IIntegrationConfigurationDetailsCache
{
    private readonly TimeSpan _slidingExpiration = TimeSpan.FromMinutes(globalSettings.EventLogging.CacheSlidingExpiration);

    public async Task<IReadOnlyList<CachedIntegrationConfigurationDetails<T>>> GetOrAddAsync<T>(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType)
    {
        var key = BuildCacheKey(organizationId: organizationId, integrationType: integrationType, eventType: eventType);

        if (cache.TryGetValue(key, out IReadOnlyList<CachedIntegrationConfigurationDetails<T>>? cached) &&
            cached is not null)
        {
            return cached;
        }

        var configurations = await repository.GetConfigurationDetailsAsync(organizationId, integrationType, eventType);
        var result = new List<CachedIntegrationConfigurationDetails<T>>();

        foreach (var configuration in configurations)
        {
            try
            {
                IntegrationFilterGroup? filters = null;
                if (configuration.Filters is string filterJson)
                {
                    filters = JsonSerializer.Deserialize<IntegrationFilterGroup>(filterJson)
                        ?? throw new InvalidOperationException("Failed to deserialize Filters to FilterGroup");
                }

                var config = configuration.MergedConfiguration.Deserialize<T>()
                    ?? throw new InvalidOperationException($"Failed to deserialize to {typeof(T).Name} - bad Configuration");

                result.Add(new CachedIntegrationConfigurationDetails<T>
                {
                    FilterGroup = filters,
                    Configuration = config,
                    Template = configuration.Template ?? string.Empty
                });
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to cache details for {Type}, check Id {RecordId} for error in Configuration or Filters",
                    typeof(T).Name,
                    configuration.Id);
            }
        }

        cache.Set(key, result, new MemoryCacheEntryOptions
        {
            SlidingExpiration = _slidingExpiration,
            Size = result.Count
        });

        return result;
    }

    public void RemoveCacheEntry(Guid organizationId, IntegrationType integrationType, EventType eventType)
    {
        cache.Remove(BuildCacheKey(organizationId, integrationType, eventType));
    }

    public void RemoveCacheEntriesForIntegration(Guid organizationId, IntegrationType integrationType)
    {
        foreach (var eventType in Enum.GetValues<EventType>())
        {
            RemoveCacheEntry(organizationId: organizationId, integrationType: integrationType, eventType: eventType);
        }
    }

    private static string BuildCacheKey(Guid organizationId, IntegrationType integrationType, EventType eventType)
    {
        return $"integration-config:{organizationId}:{integrationType}:{eventType}";
    }
}

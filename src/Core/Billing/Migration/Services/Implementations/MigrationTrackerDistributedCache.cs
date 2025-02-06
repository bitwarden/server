using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Migration.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Migration.Services.Implementations;

public class MigrationTrackerDistributedCache(
    [FromKeyedServices("persistent")]
    IDistributedCache distributedCache) : IMigrationTrackerCache
{
    public async Task StartTracker(Provider provider) =>
        await SetAsync(new ProviderMigrationTracker
        {
            ProviderId = provider.Id,
            ProviderName = provider.Name
        });

    public async Task SetOrganizationIds(Guid providerId, IEnumerable<Guid> organizationIds)
    {
        var tracker = await GetAsync(providerId);

        tracker.OrganizationIds = organizationIds.ToList();

        await SetAsync(tracker);
    }

    public Task<ProviderMigrationTracker> GetTracker(Guid providerId) => GetAsync(providerId);

    public async Task UpdateTrackingStatus(Guid providerId, ProviderMigrationProgress status)
    {
        var tracker = await GetAsync(providerId);

        tracker.Progress = status;

        await SetAsync(tracker);
    }

    public async Task StartTracker(Guid providerId, Organization organization) =>
        await SetAsync(new ClientMigrationTracker
        {
            ProviderId = providerId,
            OrganizationId = organization.Id,
            OrganizationName = organization.Name
        });

    public Task<ClientMigrationTracker> GetTracker(Guid providerId, Guid organizationId) =>
        GetAsync(providerId, organizationId);

    public async Task UpdateTrackingStatus(Guid providerId, Guid organizationId, ClientMigrationProgress status)
    {
        var tracker = await GetAsync(providerId, organizationId);

        tracker.Progress = status;

        await SetAsync(tracker);
    }

    private static string GetProviderCacheKey(Guid providerId) => $"provider_{providerId}_migration";

    private static string GetClientCacheKey(Guid providerId, Guid clientId) =>
        $"provider_{providerId}_client_{clientId}_migration";

    private async Task<ProviderMigrationTracker> GetAsync(Guid providerId)
    {
        var cacheKey = GetProviderCacheKey(providerId);

        var json = await distributedCache.GetStringAsync(cacheKey);

        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ProviderMigrationTracker>(json);
    }

    private async Task<ClientMigrationTracker> GetAsync(Guid providerId, Guid organizationId)
    {
        var cacheKey = GetClientCacheKey(providerId, organizationId);

        var json = await distributedCache.GetStringAsync(cacheKey);

        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ClientMigrationTracker>(json);
    }

    private async Task SetAsync(ProviderMigrationTracker tracker)
    {
        var cacheKey = GetProviderCacheKey(tracker.ProviderId);

        var json = JsonSerializer.Serialize(tracker);

        await distributedCache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });
    }

    private async Task SetAsync(ClientMigrationTracker tracker)
    {
        var cacheKey = GetClientCacheKey(tracker.ProviderId, tracker.OrganizationId);

        var json = JsonSerializer.Serialize(tracker);

        await distributedCache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });
    }
}

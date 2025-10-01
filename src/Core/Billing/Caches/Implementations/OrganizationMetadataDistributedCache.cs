using System.Text.Json;
using Bit.Core.Billing.Organizations.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Caches.Implementations;

public class OrganizationMetadataDistributedCache(
    [FromKeyedServices("persistent")]
    IDistributedCache distributedCache) : IOrganizationMetadataCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<OrganizationMetadata?> Get(Guid organizationId)
    {
        var cacheKey = GetCacheKey(organizationId);
        var cachedValue = await distributedCache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(cachedValue))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OrganizationMetadata>(cachedValue);
    }

    public async Task Set(Guid organizationId, OrganizationMetadata metadata)
    {
        var cacheKey = GetCacheKey(organizationId);
        var serializedValue = JsonSerializer.Serialize(metadata);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        };

        await distributedCache.SetStringAsync(cacheKey, serializedValue, options);
    }

    public async Task Remove(Guid organizationId)
    {
        var cacheKey = GetCacheKey(organizationId);
        await distributedCache.RemoveAsync(cacheKey);
    }

    private static string GetCacheKey(Guid organizationId) =>
        $"organization_metadata_{organizationId}";
}

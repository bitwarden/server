using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Caches.Implementations;

public class SetupIntentDistributedCache(
    [FromKeyedServices("persistent")]
    IDistributedCache distributedCache) : ISetupIntentCache
{
    public async Task<string> Get(Guid subscriberId)
    {
        var cacheKey = GetCacheKey(subscriberId);

        return await distributedCache.GetStringAsync(cacheKey);
    }

    public async Task Remove(Guid subscriberId)
    {
        var cacheKey = GetCacheKey(subscriberId);

        await distributedCache.RemoveAsync(cacheKey);
    }

    public async Task Set(Guid subscriberId, string setupIntentId)
    {
        var cacheKey = GetCacheKey(subscriberId);

        await distributedCache.SetStringAsync(cacheKey, setupIntentId);
    }

    private static string GetCacheKey(Guid subscriberId) => $"pending_bank_account_{subscriberId}";
}

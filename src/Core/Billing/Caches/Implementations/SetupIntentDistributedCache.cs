using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Caches.Implementations;

public class SetupIntentDistributedCache(
    [FromKeyedServices("persistent")]
    IDistributedCache distributedCache) : ISetupIntentCache
{
    public async Task<string?> GetSetupIntentIdForSubscriber(Guid subscriberId)
    {
        var cacheKey = GetCacheKeyBySubscriberId(subscriberId);
        return await distributedCache.GetStringAsync(cacheKey);
    }

    public async Task<Guid?> GetSubscriberIdForSetupIntent(string setupIntentId)
    {
        var cacheKey = GetCacheKeyBySetupIntentId(setupIntentId);
        var value = await distributedCache.GetStringAsync(cacheKey);
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var subscriberId))
        {
            return null;
        }
        return subscriberId;
    }

    public async Task RemoveSetupIntentForSubscriber(Guid subscriberId)
    {
        var cacheKey = GetCacheKeyBySubscriberId(subscriberId);
        await distributedCache.RemoveAsync(cacheKey);
    }

    public async Task Set(Guid subscriberId, string setupIntentId)
    {
        var bySubscriberIdCacheKey = GetCacheKeyBySubscriberId(subscriberId);
        var bySetupIntentIdCacheKey = GetCacheKeyBySetupIntentId(setupIntentId);
        await Task.WhenAll(
            distributedCache.SetStringAsync(bySubscriberIdCacheKey, setupIntentId),
            distributedCache.SetStringAsync(bySetupIntentIdCacheKey, subscriberId.ToString()));
    }

    private static string GetCacheKeyBySetupIntentId(string setupIntentId) =>
        $"subscriber_id_for_setup_intent_id_{setupIntentId}";

    private static string GetCacheKeyBySubscriberId(Guid subscriberId) =>
        $"setup_intent_id_for_subscriber_id_{subscriberId}";
}

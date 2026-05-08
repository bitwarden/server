using System.Globalization;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Billing.Cache;

public class TrialInitiationCache(IDistributedCache cache) : ITrialInitiationCache
{
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    public async Task WriteAsync(string trialInitiationId, int trialLength)
    {
        var value = Encoding.UTF8.GetBytes(trialLength.ToString(CultureInfo.InvariantCulture));
        await cache.SetAsync(CacheKey(trialInitiationId), value, _cacheOptions);
    }

    public async Task<int?> GetAndRemoveAsync(string trialInitiationId)
    {
        var cached = await cache.GetAsync(CacheKey(trialInitiationId));
        if (cached is null)
        {
            return null;
        }

        await cache.RemoveAsync(CacheKey(trialInitiationId));
        return int.Parse(Encoding.UTF8.GetString(cached), CultureInfo.InvariantCulture);
    }

    private static string CacheKey(string trialInitiationId) => $"trial-initiation:{trialInitiationId}";
}
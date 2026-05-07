using System.Globalization;
using System.Text;
using Bit.Core.Exceptions;
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

    public async Task ValidateTrialLengthAsync(string trialInitiationId, int requestedTrialLength)
    {
        var cached = await cache.GetAsync(CacheKey(trialInitiationId));
        if (cached is null)
        {
            return;
        }

        var cachedTrialLength = int.Parse(Encoding.UTF8.GetString(cached), CultureInfo.InvariantCulture);
        if (cachedTrialLength != requestedTrialLength)
        {
            throw new BadRequestException("Trial length does not match the original trial invitation.");
        }

        await cache.RemoveAsync(CacheKey(trialInitiationId));
    }

    private static string CacheKey(string trialInitiationId) => $"trial-initiation:{trialInitiationId}";
}

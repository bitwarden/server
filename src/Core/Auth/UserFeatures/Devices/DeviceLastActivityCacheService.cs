using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.UserFeatures.Devices;

public class DeviceLastActivityCacheService : IDeviceLastActivityCacheService
{
    // Sentinel value — the cache acts as a presence check; the bytes themselves are never read.
    // IDistributedCache.SetAsync requires a non-null byte[], so we use a one-byte non-empty value
    // to stay safely non-empty across every backend (Cosmos, Redis, SQL/EF, in-memory).
    private static readonly byte[] _sentinel = [1];

    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _cacheOptions;

    public DeviceLastActivityCacheService(
        // "persistent" is a well-known keyed service registered by AddDistributedCache(globalSettings).
        // Backed by Cosmos DB in cloud; falls back to SQL Server/EF cache in self-hosted.
        [FromKeyedServices("persistent")] IDistributedCache cache,
        IGlobalSettings globalSettings)
    {
        _cache = cache;
        _cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(globalSettings.DeviceLastActivityCacheTtlHours)
        };
    }

    public async Task<bool> IsUpToDateAsync(Guid userId, string identifier)
    {
        var bytes = await _cache.GetAsync(CacheKey(userId, identifier));
        return bytes != null;
    }

    public async Task RecordUpdateAsync(Guid userId, string identifier)
    {
        await _cache.SetAsync(CacheKey(userId, identifier), _sentinel, _cacheOptions);
    }

    private static string CacheKey(Guid userId, string identifier) => $"device:last-activity:{userId}:{identifier}";
}

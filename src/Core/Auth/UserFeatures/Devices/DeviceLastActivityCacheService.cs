using System.Text;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.UserFeatures.Devices;

public class DeviceLastActivityCacheService : IDeviceLastActivityCacheService
{
    // TTL is 48h rather than 24h to ensure the entry outlives the full following calendar day
    // regardless of bump time. A bump at 11:59 PM with a 24h TTL would expire mid-day tomorrow,
    // creating a race window where a cache miss could trigger a redundant DB write on the same day.
    // The date comparison in HasBeenBumpedTodayAsync is the real guard; TTL is housekeeping only.
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48)
    };

    private readonly IDistributedCache _cache;
    private readonly TimeProvider _timeProvider;

    public DeviceLastActivityCacheService(
        // "persistent" is a well-known keyed service registered by AddDistributedCache(globalSettings).
        // Backed by Cosmos DB in cloud; falls back to SQL Server/EF cache in self-hosted.
        [FromKeyedServices("persistent")] IDistributedCache cache,
        TimeProvider timeProvider)
    {
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<bool> HasBeenBumpedTodayAsync(string identifier)
    {
        var bytes = await _cache.GetAsync(CacheKey(identifier));
        if (bytes == null) return false;
        var cached = Encoding.UTF8.GetString(bytes);
        return cached == _timeProvider.GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd");
    }

    public async Task RecordBumpAsync(string identifier)
    {
        var value = Encoding.UTF8.GetBytes(_timeProvider.GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd"));
        await _cache.SetAsync(CacheKey(identifier), value, _cacheOptions);
    }

    private static string CacheKey(string identifier) => $"device:last-activity:{identifier}";
}

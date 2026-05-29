using System.Globalization;
using System.Text;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.UserFeatures.Devices;

public class DeviceLastActivityCacheService : IDeviceLastActivityCacheService
{
    // 24h TTL is housekeeping. The composite value comparison in IsUpToDateAsync is the real
    // correctness guard — a stale entry (different date or version) misses on value mismatch
    // regardless of TTL, so a longer TTL wouldn't prevent any day-boundary DB write.
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
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

    public async Task<bool> IsUpToDateAsync(Guid userId, string identifier, string? clientVersion)
    {
        var bytes = await _cache.GetAsync(CacheKey(userId, identifier));
        if (bytes == null)
        {
            return false;
        }
        var cached = Encoding.UTF8.GetString(bytes);
        return cached == ComposeCacheValue(clientVersion);
    }

    public async Task RecordUpdateAsync(Guid userId, string identifier, string? clientVersion)
    {
        var value = Encoding.UTF8.GetBytes(ComposeCacheValue(clientVersion));
        await _cache.SetAsync(CacheKey(userId, identifier), value, _cacheOptions);
    }

    // Cache value composes today's date and the supplied client version so a hit means every
    // tracked input matches what we last wrote — i.e. the activity event is already represented.
    // Format: "yyyy-MM-dd|<version-or-empty>". Empty version segment when the header was absent —
    // preserves a stable representation so the read-side comparison is plain string equality. If
    // additional last-observed properties are added to IUpdateDeviceLastActivityCommand, fold them
    // into this composed value so cache semantics continue to track every observed input.
    private string ComposeCacheValue(string? clientVersion)
    {
        var date = _timeProvider.GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{date}|{clientVersion ?? string.Empty}";
    }

    private static string CacheKey(Guid userId, string identifier) => $"device:last-activity:{userId}:{identifier}";
}

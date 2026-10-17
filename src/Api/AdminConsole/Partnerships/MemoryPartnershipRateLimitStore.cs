using Microsoft.Extensions.Caching.Memory;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Keeps partnership request counters in this instance's memory, for when no distributed store is configured.
/// </summary>
public sealed class MemoryPartnershipRateLimitStore(IMemoryCache memoryCache) : IPartnershipRateLimitStore
{
    private readonly Lock _lock = new();

    public Task<long> IncrementAsync(string key, TimeSpan expiry)
    {
        lock (_lock)
        {
            if (!memoryCache.TryGetValue<Counter>(key, out var counter) || counter is null)
            {
                counter = memoryCache.Set(key, new Counter(), expiry);
            }

            return Task.FromResult(++counter.Value);
        }
    }

    private sealed class Counter
    {
        public long Value;
    }
}

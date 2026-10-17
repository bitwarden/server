using System.Collections.Concurrent;
using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bit.Api.Test.AdminConsole.Partnerships.Idempotency;

/// <summary>
/// A <see cref="PartnershipIdempotencyStore"/> over a real FusionCache configured like the Api registration, with a
/// controllable memory cache clock and a distributed cache that records every value written to it.
/// </summary>
public sealed class TestIdempotencyStore : IDisposable
{
    private readonly MemoryCache _memoryCache;
    private readonly FusionCache _cache;

    public TestIdempotencyStore()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions { Clock = Clock });
        _cache = new FusionCache(new FusionCacheOptions
        {
            DefaultEntryOptions = new FusionCacheEntryOptions
            {
                Duration = PartnershipIdempotencyStore.ResponseLifetime,
                IsFailSafeEnabled = false,
                EagerRefreshThreshold = null,
                AllowBackgroundDistributedCacheOperations = false,
                JitterMaxDuration = TimeSpan.Zero,
            },
        }, _memoryCache);
        _cache.SetupDistributedCache(DistributedCache, new FusionCacheSystemTextJsonSerializer());
        Store = new PartnershipIdempotencyStore(_cache, new EphemeralDataProtectionProvider());
    }

    public void Dispose()
    {
        _cache.Dispose();
        _memoryCache.Dispose();
    }

    public TestClock Clock { get; } = new();
    public RecordingDistributedCache DistributedCache { get; } = new();
    public PartnershipIdempotencyStore Store { get; }

    public sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;
        public void Advance(TimeSpan by) => UtcNow += by;
    }

    /// <summary>
    /// Records written values but never returns them, so reads always come from the memory cache and its clock.
    /// </summary>
    public sealed class RecordingDistributedCache : IDistributedCache
    {
        public ConcurrentQueue<(string Key, byte[] Value)> Written { get; } = new();

        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Written.Enqueue((key, value));

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Written.Enqueue((key, value));
            return Task.CompletedTask;
        }
    }
}

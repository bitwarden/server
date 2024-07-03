using Microsoft.Extensions.Caching.Distributed;
using Xunit;
using Microsoft.Extensions.Time.Testing;
using Bit.Infrastructure.EntityFramework;

namespace Bit.Infrastructure.IntegrationTest;

public class DistributedCacheTests
{
    [DatabaseTheory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task Simple_NotExpiredItem_StartsScan(IDistributedCache cache, TimeProvider timeProvider)
    {
        var fakeTimeProvider = (FakeTimeProvider)timeProvider;

        cache.Set("test-key", "some-value"u8.ToArray(), new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(20),
        });

        // Should have expired and not be returned
        var firstValue = cache.Get("test-key");

        // Scan for expired items is supposed to run every 30 minutes
        fakeTimeProvider.Advance(TimeSpan.FromMinutes(31));

        var secondValue = cache.Get("test-key");

        // This should have forced the EF cache to start a scan task
        if (cache is EntityFrameworkCache efCache)
        {
            Assert.NotNull(efCache.scanTask);
            // We don't want the scan task to throw an exception, unwrap it.
            await efCache.scanTask;
        }

        Assert.NotNull(firstValue);
        Assert.Null(secondValue);
    }

    [DatabaseTheory, DatabaseData(UseFakeTimeProvider = true)]
    public async Task ParallelReadsAndWrites_Work(IDistributedCache cache, TimeProvider timeProvider)
    {
        var fakeTimeProvider = (FakeTimeProvider)timeProvider;

        await Parallel.ForEachAsync(Enumerable.Range(1, 100), async (index, _) =>
        {
            await cache.SetAsync($"test-{index}", "some-value"u8.ToArray(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(index),
            });
        });

        await Parallel.ForEachAsync(Enumerable.Range(1, 100), async (index, _) =>
        {
            var value = await cache.GetAsync($"test-{index}");
            Assert.NotNull(value);
        });
    }

    [DatabaseTheory, DatabaseData]
    public async Task MultipleWritesOnSameKey_ShouldNotThrow(IDistributedCache cache)
    {
        await cache.SetAsync("test-duplicate", "some-value"u8.ToArray());
        await cache.SetAsync("test-duplicate", "some-value"u8.ToArray());
    }
}

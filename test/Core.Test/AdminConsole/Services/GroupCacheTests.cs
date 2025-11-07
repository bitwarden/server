using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Services;

public class GroupCacheTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_ConcurrentRequests_OnlyLoadsOnce(Group group)
    {
        var groupId = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var repo = Substitute.For<IGroupRepository>();
        var loadCount = 0;

        var dbLoadStarted = new TaskCompletionSource();
        var dbLoadContinue = new TaskCompletionSource();

        repo.GetByIdAsync(groupId)
            .Returns(async _ =>
            {
                Interlocked.Increment(ref loadCount);
                dbLoadStarted.TrySetResult();
                await dbLoadContinue.Task;
                return group;
            });

        var cache = new GroupCache(memoryCache, TimeSpan.FromMinutes(10), repo);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cache.GetAsync(groupId))
            .ToArray();

        await dbLoadStarted.Task;
        Assert.Equal(1, loadCount);
        dbLoadContinue.SetResult();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Same(group, r));

        Assert.Equal(1, loadCount);
        await repo.Received(1).GetByIdAsync(groupId);

        var second = await cache.GetAsync(groupId);
        Assert.Same(group, second);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CacheExpires_TriggersReload(
        Group group1,
        Group group2)
    {
        var groupId = Guid.NewGuid();
        var repo = Substitute.For<IGroupRepository>();
        var memoryCache = Substitute.For<IMemoryCache>();
        var callCount = 0;

        memoryCache.TryGetValue(groupId, out Arg.Any<object?>()).Returns(false);
        repo.GetByIdAsync(groupId)
            .Returns(_ =>
            {
                Interlocked.Increment(ref callCount);
                return callCount == 1 ? group1 : group2;
            });

        var cache = new GroupCache(memoryCache, TimeSpan.FromMilliseconds(5), repo);

        var first = await cache.GetAsync(groupId);
        Assert.Equal(1, callCount);
        Assert.Same(group1, first);

        var second = await cache.GetAsync(groupId);
        Assert.Equal(2, callCount);
        Assert.Same(group2, second);

        await repo.Received(2).GetByIdAsync(groupId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CacheHit_RetrunsCached(Group group)
    {
        var groupId = Guid.NewGuid();
        var repo = Substitute.For<IGroupRepository>();
        var memoryCache = Substitute.For<IMemoryCache>();

        memoryCache.TryGetValue(groupId, out group).Returns(true);

        var cache = new GroupCache(memoryCache, TimeSpan.FromMilliseconds(5), repo);

        var first = await cache.GetAsync(groupId);
        Assert.Same(group, first);

        var second = await cache.GetAsync(groupId);
        Assert.Same(group, second);

        await repo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public void Dispose_DisposesMemoryCache()
    {
        var memoryCache = Substitute.For<IMemoryCache>();
        var cache = new GroupCache(
            memoryCache: memoryCache,
            cacheEntryTtl: TimeSpan.FromMilliseconds(5),
            groupRepository: Substitute.For<IGroupRepository>()
        );

        cache.Dispose();
        memoryCache.Received(1).Dispose();
    }
}

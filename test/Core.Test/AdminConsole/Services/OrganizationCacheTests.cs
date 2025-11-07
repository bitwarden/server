using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Services;

public class OrganizationCacheTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_ConcurrentRequests_OnlyLoadsOnce(Organization organization)
    {
        var organizationId = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var repo = Substitute.For<IOrganizationRepository>();
        var loadCount = 0;

        var dbLoadStarted = new TaskCompletionSource();
        var dbLoadContinue = new TaskCompletionSource();

        repo.GetByIdAsync(organizationId)
            .Returns(async _ =>
            {
                Interlocked.Increment(ref loadCount);
                dbLoadStarted.TrySetResult();
                await dbLoadContinue.Task;
                return organization;
            });

        var cache = new OrganizationCache(memoryCache, TimeSpan.FromMinutes(10), repo);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cache.GetAsync(organizationId))
            .ToArray();

        await dbLoadStarted.Task;
        Assert.Equal(1, loadCount);
        dbLoadContinue.SetResult();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Same(organization, r));

        Assert.Equal(1, loadCount);
        await repo.Received(1).GetByIdAsync(organizationId);

        var second = await cache.GetAsync(organizationId);
        Assert.Same(organization, second);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CacheExpires_TriggersReload(
        Organization organization1,
        Organization organization2)
    {
        var organizationId = Guid.NewGuid();
        var repo = Substitute.For<IOrganizationRepository>();
        var memoryCache = Substitute.For<IMemoryCache>();
        var callCount = 0;

        memoryCache.TryGetValue(organizationId, out Arg.Any<object?>()).Returns(false);
        repo.GetByIdAsync(organizationId)
            .Returns(_ =>
            {
                Interlocked.Increment(ref callCount);
                return callCount == 1 ? organization1 : organization2;
            });

        var cache = new OrganizationCache(memoryCache, TimeSpan.FromMilliseconds(5), repo);

        var first = await cache.GetAsync(organizationId);
        Assert.Equal(1, callCount);
        Assert.Same(organization1, first);

        var second = await cache.GetAsync(organizationId);
        Assert.Equal(2, callCount);
        Assert.Same(organization2, second);

        await repo.Received(2).GetByIdAsync(organizationId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CacheHit_RetrunsCached(Organization organization)
    {
        var organizationId = Guid.NewGuid();
        var repo = Substitute.For<IOrganizationRepository>();
        var memoryCache = Substitute.For<IMemoryCache>();

        memoryCache.TryGetValue(organizationId, out organization).Returns(true);

        var cache = new OrganizationCache(memoryCache, TimeSpan.FromMilliseconds(5), repo);

        var first = await cache.GetAsync(organizationId);
        Assert.Same(organization, first);

        var second = await cache.GetAsync(organizationId);
        Assert.Same(organization, second);

        await repo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public void Dispose_DisposesMemoryCache()
    {
        var memoryCache = Substitute.For<IMemoryCache>();
        var cache = new OrganizationCache(
            memoryCache: memoryCache,
            cacheEntryTtl: TimeSpan.FromMilliseconds(5),
            organizationRepository: Substitute.For<IOrganizationRepository>()
        );

        cache.Dispose();
        memoryCache.Received(1).Dispose();
    }
}

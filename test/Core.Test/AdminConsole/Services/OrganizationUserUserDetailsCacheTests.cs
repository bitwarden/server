using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Services;

public class OrganizationUserDetailsCacheTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_ConcurrentRequests_OnlyLoadsOnce(OrganizationUserUserDetails userDetails)
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var repo = Substitute.For<IOrganizationUserRepository>();
        var loadCount = 0;

        var dbLoadStarted = new TaskCompletionSource();
        var dbLoadContinue = new TaskCompletionSource();

        repo.GetDetailsByOrganizationIdUserIdAsync(organizationId, userId)
            .Returns(async _ =>
            {
                Interlocked.Increment(ref loadCount);
                dbLoadStarted.TrySetResult();
                await dbLoadContinue.Task;
                return userDetails;
            });

        var cache = new OrganizationUserUserDetailsCache(memoryCache, TimeSpan.FromMinutes(10), repo);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cache.GetAsync(organizationId, userId))
            .ToArray();

        await dbLoadStarted.Task;
        Assert.Equal(1, loadCount);
        dbLoadContinue.SetResult();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Same(userDetails, r));

        Assert.Equal(1, loadCount);
        await repo.Received(1).GetDetailsByOrganizationIdUserIdAsync(organizationId, userId);

        var second = await cache.GetAsync(organizationId, userId);
        Assert.Same(userDetails, second);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CacheExpires_TriggersReload(
        OrganizationUserUserDetails userDetails1,
        OrganizationUserUserDetails userDetails2)
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = new OrganizationUserKey(organizationId, userId);
        var repo = Substitute.For<IOrganizationUserRepository>();
        var memoryCache = Substitute.For<IMemoryCache>();
        var callCount = 0;

        memoryCache.TryGetValue(key, out Arg.Any<object?>()).Returns(false);
        repo.GetDetailsByOrganizationIdUserIdAsync(organizationId, userId)
            .Returns(_ =>
            {
                Interlocked.Increment(ref callCount);
                return callCount == 1 ? userDetails1 : userDetails2;
            });

        var cache = new OrganizationUserUserDetailsCache(memoryCache, TimeSpan.FromMilliseconds(5), repo);

        var first = await cache.GetAsync(organizationId, userId);
        Assert.Equal(1, callCount);
        Assert.Same(userDetails1, first);

        var second = await cache.GetAsync(organizationId, userId);
        Assert.Equal(2, callCount);
        Assert.Same(userDetails2, second);

        await repo.Received(2).GetDetailsByOrganizationIdUserIdAsync(organizationId, userId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_CacheHit_RetrunsCached(OrganizationUserUserDetails userDetails)
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = new OrganizationUserKey(organizationId, userId);
        var repo = Substitute.For<IOrganizationUserRepository>();
        var memoryCache = Substitute.For<IMemoryCache>();

        memoryCache.TryGetValue(key, out userDetails).Returns(true);

        var cache = new OrganizationUserUserDetailsCache(memoryCache, TimeSpan.FromMilliseconds(5), repo);

        var first = await cache.GetAsync(organizationId, userId);
        Assert.Same(userDetails, first);

        var second = await cache.GetAsync(organizationId, userId);
        Assert.Same(userDetails, second);

        await repo.DidNotReceive().GetDetailsByOrganizationIdUserIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public void Dispose_DisposesMemoryCache()
    {
        var memoryCache = Substitute.For<IMemoryCache>();
        var cache = new OrganizationUserUserDetailsCache(
            memoryCache: memoryCache,
            cacheEntryTtl: TimeSpan.FromMilliseconds(5),
            userRepository: Substitute.For<IOrganizationUserRepository>()
        );

        cache.Dispose();
        memoryCache.Received(1).Dispose();
    }
}

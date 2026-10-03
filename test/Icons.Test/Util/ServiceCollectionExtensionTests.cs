using Bit.Icons.Extensions;
using Bit.Icons.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Icons.Test.Util;

public class ServiceCollectionExtensionTests
{
    private const long _iconsCacheSizeLimit = 100;
    private const long _changePasswordUriCacheSizeLimit = 10_000;

    /// <summary>
    /// Builds a provider with deliberately different size limits for the two caches, so that a
    /// single shared cache cannot satisfy both.
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddCaches(
            new IconsSettings { CacheSizeLimit = _iconsCacheSizeLimit },
            new ChangePasswordUriSettings { CacheSizeLimit = _changePasswordUriCacheSizeLimit });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddCaches_RegistersTwoDistinctCaches()
    {
        using var provider = BuildProvider();

        var iconsCache = provider.GetRequiredKeyedService<IMemoryCache>(IconsCacheConstants.IconsCacheName);
        var changePasswordUriCache =
            provider.GetRequiredKeyedService<IMemoryCache>(IconsCacheConstants.ChangePasswordUriCacheName);

        Assert.NotSame(iconsCache, changePasswordUriCache);
    }

    [Fact]
    public void AddCaches_CachesDoNotShareEntries()
    {
        using var provider = BuildProvider();
        var iconsCache = provider.GetRequiredKeyedService<IMemoryCache>(IconsCacheConstants.IconsCacheName);
        var changePasswordUriCache =
            provider.GetRequiredKeyedService<IMemoryCache>(IconsCacheConstants.ChangePasswordUriCacheName);

        iconsCache.Set("example.com", "icons-value", new MemoryCacheEntryOptions { Size = 1 });

        Assert.False(changePasswordUriCache.TryGetValue("example.com", out _));
    }

    [Fact]
    public void AddCaches_EachCacheUsesItsOwnSizeLimit()
    {
        // An entry larger than a cache's SizeLimit is rejected outright, so an entry sized between
        // the two limits is accepted by one cache and refused by the other only if each cache is
        // using its own configured limit.
        const int entrySize = 500;

        using var provider = BuildProvider();
        var iconsCache = provider.GetRequiredKeyedService<IMemoryCache>(IconsCacheConstants.IconsCacheName);
        var changePasswordUriCache =
            provider.GetRequiredKeyedService<IMemoryCache>(IconsCacheConstants.ChangePasswordUriCacheName);

        iconsCache.Set("example.com", "icons-value", new MemoryCacheEntryOptions { Size = entrySize });
        changePasswordUriCache.Set("example.com", "change-password-value",
            new MemoryCacheEntryOptions { Size = entrySize });

        Assert.False(iconsCache.TryGetValue("example.com", out _));
        Assert.True(changePasswordUriCache.TryGetValue("example.com", out _));
    }
}

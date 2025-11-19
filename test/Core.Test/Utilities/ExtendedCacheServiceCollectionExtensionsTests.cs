using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Utilities;

public class ExtendedCacheServiceCollectionExtensionsTests
{
    private readonly IServiceCollection _services;
    private readonly GlobalSettings _globalSettings;
    private const string _cacheName = "TestCache";

    public ExtendedCacheServiceCollectionExtensionsTests()
    {
        _services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        _globalSettings = new GlobalSettings();
        config.GetSection("GlobalSettings").Bind(_globalSettings);

        _services.TryAddSingleton(config);
        _services.TryAddSingleton(_globalSettings);
        _services.TryAddSingleton<IGlobalSettings>(_globalSettings);
        _services.AddLogging();
    }

    [Fact]
    public void AddExtendedCache_CustomSettings_OverridesDefaults()
    {
        var settings = new GlobalSettings.ExtendedCacheSettings
        {
            Duration = TimeSpan.FromMinutes(12),
            FailSafeMaxDuration = TimeSpan.FromHours(1.5),
            FailSafeThrottleDuration = TimeSpan.FromMinutes(1),
            EagerRefreshThreshold = 0.75f,
            FactorySoftTimeout = TimeSpan.FromMilliseconds(20),
            FactoryHardTimeout = TimeSpan.FromSeconds(3),
            DistributedCacheSoftTimeout = TimeSpan.FromSeconds(0.5),
            DistributedCacheHardTimeout = TimeSpan.FromSeconds(1.5),
            JitterMaxDuration = TimeSpan.FromSeconds(5),
            IsFailSafeEnabled = false,
            AllowBackgroundDistributedCacheOperations = false,
        };

        _services.AddExtendedCache(_cacheName, _globalSettings, settings);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);
        var opt = cache.DefaultEntryOptions;

        Assert.Equal(TimeSpan.FromMinutes(12), opt.Duration);
        Assert.False(opt.IsFailSafeEnabled);
        Assert.Equal(TimeSpan.FromHours(1.5), opt.FailSafeMaxDuration);
        Assert.Equal(TimeSpan.FromMinutes(1), opt.FailSafeThrottleDuration);
        Assert.Equal(0.75f, opt.EagerRefreshThreshold);
        Assert.Equal(TimeSpan.FromMilliseconds(20), opt.FactorySoftTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(3000), opt.FactoryHardTimeout);
        Assert.Equal(TimeSpan.FromSeconds(0.5), opt.DistributedCacheSoftTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1.5), opt.DistributedCacheHardTimeout);
        Assert.False(opt.AllowBackgroundDistributedCacheOperations);
        Assert.Equal(TimeSpan.FromSeconds(5), opt.JitterMaxDuration);
    }

    [Fact]
    public void AddExtendedCache_DefaultSettings_ConfiguresExpectedValues()
    {
        _services.AddExtendedCache(_cacheName, _globalSettings);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);
        var opt = cache.DefaultEntryOptions;

        Assert.Equal(TimeSpan.FromMinutes(30), opt.Duration);
        Assert.True(opt.IsFailSafeEnabled);
        Assert.Equal(TimeSpan.FromHours(2), opt.FailSafeMaxDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), opt.FailSafeThrottleDuration);
        Assert.Equal(0.9f, opt.EagerRefreshThreshold);
        Assert.Equal(TimeSpan.FromMilliseconds(100), opt.FactorySoftTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), opt.FactoryHardTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), opt.DistributedCacheSoftTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), opt.DistributedCacheHardTimeout);
        Assert.True(opt.AllowBackgroundDistributedCacheOperations);
        Assert.Equal(TimeSpan.FromSeconds(2), opt.JitterMaxDuration);
    }

    [Fact]
    public void AddExtendedCache_DisabledDistributedCache_DoesNotRegisterBackplaneOrRedis()
    {
        var settings = new GlobalSettings.ExtendedCacheSettings
        {
            EnableDistributedCache = false,
        };

        _services.AddExtendedCache(_cacheName, _globalSettings, settings);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);

        Assert.False(cache.HasDistributedCache);
        Assert.False(cache.HasBackplane);
    }

    [Fact]
    public void AddExtendedCache_EmptyCacheName_DoesNothing()
    {
        _services.AddExtendedCache(string.Empty, _globalSettings);

        var regs = _services.Where(s => s.ServiceType == typeof(IFusionCache)).ToList();
        Assert.Empty(regs);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetKeyedService<IFusionCache>(_cacheName);
        Assert.Null(cache);
    }

    [Fact]
    public void AddExtendedCache_MultipleCalls_OnlyAddsOneCacheService()
    {
        var settings = CreateGlobalSettings(new()
        {
            { "GlobalSettings:DistributedCache:Redis:ConnectionString", "localhost:6379" }
        });

        // Provide a multiplexer (shared)
        _services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

        _services.AddExtendedCache(_cacheName, settings);
        _services.AddExtendedCache(_cacheName, settings);
        _services.AddExtendedCache(_cacheName, settings);

        var regs = _services.Where(s => s.ServiceType == typeof(IFusionCache)).ToList();
        Assert.Single(regs);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);
        Assert.NotNull(cache);
    }

    [Fact]
    public void AddExtendedCache_MultipleDifferentCaches_AddsAll()
    {
        _services.AddExtendedCache("Cache1", _globalSettings);
        _services.AddExtendedCache("Cache2", _globalSettings);

        using var provider = _services.BuildServiceProvider();

        var cache1 = provider.GetRequiredKeyedService<IFusionCache>("Cache1");
        var cache2 = provider.GetRequiredKeyedService<IFusionCache>("Cache2");

        Assert.NotNull(cache1);
        Assert.NotNull(cache2);
        Assert.NotSame(cache1, cache2);
    }

    [Fact]
    public void AddExtendedCache_WithRedis_EnablesDistributedCacheAndBackplane()
    {
        var settings = CreateGlobalSettings(new()
        {
            { "GlobalSettings:DistributedCache:Redis:ConnectionString", "localhost:6379" },
            { "GlobalSettings:DistributedCache:DefaultExtendedCache:UseSharedRedisCache", "true" }
        });

        // Provide a multiplexer (shared)
        _services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

        _services.AddExtendedCache(_cacheName, settings);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);

        Assert.True(cache.HasDistributedCache);
        Assert.True(cache.HasBackplane);
    }

    [Fact]
    public void AddExtendedCache_InvalidRedisConnection_LogsAndThrows()
    {
        var settings = new GlobalSettings.ExtendedCacheSettings
        {
            UseSharedRedisCache = false,
            Redis = new GlobalSettings.ConnectionStringSettings { ConnectionString = "invalid:9999" }
        };

        _services.AddExtendedCache(_cacheName, _globalSettings, settings);

        using var provider = _services.BuildServiceProvider();
        Assert.Throws<RedisConnectionException>(() =>
        {
            var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);
            // Trigger lazy initialization
            cache.GetOrDefault<string>("test");
        });
    }

    [Fact]
    public void AddExtendedCache_WithExistingRedis_UsesExistingDistributedCacheAndBackplane()
    {
        var settings = CreateGlobalSettings(new()
        {
            { "GlobalSettings:DistributedCache:Redis:ConnectionString", "localhost:6379" },
        });

        _services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
        _services.AddSingleton(Substitute.For<IDistributedCache>());

        _services.AddExtendedCache(_cacheName, settings);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);

        Assert.True(cache.HasDistributedCache);
        Assert.True(cache.HasBackplane);

        var existingCache = provider.GetRequiredService<IDistributedCache>();
        Assert.NotNull(existingCache);
    }

    [Fact]
    public void AddExtendedCache_NoRedis_DisablesDistributedCacheAndBackplane()
    {
        _services.AddExtendedCache(_cacheName, _globalSettings);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);

        Assert.False(cache.HasDistributedCache);
        Assert.False(cache.HasBackplane);
    }

    [Fact]
    public void AddExtendedCache_NoSharedRedisButNoConnectionString_DisablesDistributedCacheAndBackplane()
    {
        var settings = new GlobalSettings.ExtendedCacheSettings
        {
            UseSharedRedisCache = false,
            // No Redis connection string
        };

        _services.AddExtendedCache(_cacheName, _globalSettings, settings);

        using var provider = _services.BuildServiceProvider();
        var cache = provider.GetRequiredKeyedService<IFusionCache>(_cacheName);

        Assert.False(cache.HasDistributedCache);
        Assert.False(cache.HasBackplane);
    }

    [Fact]
    public void AddExtendedCache_KeyedRedis_UsesSeparateMultiplexers()
    {
        var settingsA = new GlobalSettings.ExtendedCacheSettings
        {
            EnableDistributedCache = true,
            UseSharedRedisCache = false,
            Redis = new GlobalSettings.ConnectionStringSettings { ConnectionString = "localhost:6379" }
        };
        var settingsB = new GlobalSettings.ExtendedCacheSettings
        {
            EnableDistributedCache = true,
            UseSharedRedisCache = false,
            Redis = new GlobalSettings.ConnectionStringSettings { ConnectionString = "localhost:6380" }
        };

        _services.AddKeyedSingleton("CacheA", Substitute.For<IConnectionMultiplexer>());
        _services.AddKeyedSingleton("CacheB", Substitute.For<IConnectionMultiplexer>());

        _services.AddExtendedCache("CacheA", _globalSettings, settingsA);
        _services.AddExtendedCache("CacheB", _globalSettings, settingsB);

        using var provider = _services.BuildServiceProvider();
        var muxA = provider.GetRequiredKeyedService<IConnectionMultiplexer>("CacheA");
        var muxB = provider.GetRequiredKeyedService<IConnectionMultiplexer>("CacheB");

        Assert.NotNull(muxA);
        Assert.NotNull(muxB);
        Assert.NotSame(muxA, muxB);
    }

    [Fact]
    public void AddExtendedCache_WithExistingKeyedDistributedCache_ReusesIt()
    {
        var existingCache = Substitute.For<IDistributedCache>();
        _services.AddKeyedSingleton<IDistributedCache>(_cacheName, existingCache);

        var settings = new GlobalSettings.ExtendedCacheSettings
        {
            UseSharedRedisCache = false,
            Redis = new GlobalSettings.ConnectionStringSettings { ConnectionString = "localhost:6379" }
        };

        _services.AddExtendedCache(_cacheName, _globalSettings, settings);

        using var provider = _services.BuildServiceProvider();
        var resolved = provider.GetRequiredKeyedService<IDistributedCache>(_cacheName);

        Assert.Same(existingCache, resolved);
    }

    private static GlobalSettings CreateGlobalSettings(Dictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        var settings = new GlobalSettings();
        config.GetSection("GlobalSettings").Bind(settings);
        return settings;
    }
}

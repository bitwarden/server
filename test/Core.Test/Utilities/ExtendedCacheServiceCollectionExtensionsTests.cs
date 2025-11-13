using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Utilities;

public class ExtendedCacheServiceCollectionExtensionsTests
{
    private readonly IServiceCollection _services;
    private readonly GlobalSettings _globalSettings;

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
    public void TryAddFusionCoreServices_CustomSettings_OverridesDefaults()
    {
        var settings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            { "GlobalSettings:DistributedCache:Duration", "00:12:00" },
            { "GlobalSettings:DistributedCache:FailSafeMaxDuration", "01:30:00" },
            { "GlobalSettings:DistributedCache:FailSafeThrottleDuration", "00:01:00" },
            { "GlobalSettings:DistributedCache:EagerRefreshThreshold", "0.75" },
            { "GlobalSettings:DistributedCache:FactorySoftTimeout", "00:00:00.020" },
            { "GlobalSettings:DistributedCache:FactoryHardTimeout", "00:00:03" },
            { "GlobalSettings:DistributedCache:DistributedCacheSoftTimeout", "00:00:00.500" },
            { "GlobalSettings:DistributedCache:DistributedCacheHardTimeout", "00:00:01.500" },
            { "GlobalSettings:DistributedCache:JitterMaxDuration", "00:00:05" },
            { "GlobalSettings:DistributedCache:IsFailSafeEnabled", "false" },
            { "GlobalSettings:DistributedCache:AllowBackgroundDistributedCacheOperations", "false" },
        });

        _services.TryAddExtendedCacheServices(settings);
        using var provider = _services.BuildServiceProvider();
        var fusionCache = provider.GetRequiredService<IFusionCache>();
        var options = fusionCache.DefaultEntryOptions;

        Assert.Equal(TimeSpan.FromMinutes(12), options.Duration);
        Assert.False(options.IsFailSafeEnabled);
        Assert.Equal(TimeSpan.FromHours(1.5), options.FailSafeMaxDuration);
        Assert.Equal(TimeSpan.FromMinutes(1), options.FailSafeThrottleDuration);
        Assert.Equal(0.75f, options.EagerRefreshThreshold);
        Assert.Equal(TimeSpan.FromMilliseconds(20), options.FactorySoftTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(3000), options.FactoryHardTimeout);
        Assert.Equal(TimeSpan.FromSeconds(0.5), options.DistributedCacheSoftTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1.5), options.DistributedCacheHardTimeout);
        Assert.False(options.AllowBackgroundDistributedCacheOperations);
        Assert.Equal(TimeSpan.FromSeconds(5), options.JitterMaxDuration);
    }

    [Fact]
    public void TryAddFusionCoreServices_DefaultSettings_ConfiguresExpectedValues()
    {
        _services.TryAddExtendedCacheServices(_globalSettings);
        using var provider = _services.BuildServiceProvider();

        var fusionCache = provider.GetRequiredService<IFusionCache>();
        var options = fusionCache.DefaultEntryOptions;

        Assert.Equal(TimeSpan.FromMinutes(30), options.Duration);
        Assert.True(options.IsFailSafeEnabled);
        Assert.Equal(TimeSpan.FromHours(2), options.FailSafeMaxDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), options.FailSafeThrottleDuration);
        Assert.Equal(0.9f, options.EagerRefreshThreshold);
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.FactorySoftTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), options.FactoryHardTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), options.DistributedCacheSoftTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), options.DistributedCacheHardTimeout);
        Assert.True(options.AllowBackgroundDistributedCacheOperations);
        Assert.Equal(TimeSpan.FromSeconds(2), options.JitterMaxDuration);
    }

    [Fact]
    public void TryAddFusionCoreServices_MultipleCalls_OnlyConfiguresOnce()
    {
        var settings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            { "GlobalSettings:DistributedCache:Redis:ConnectionString", "localhost:6379" },
        });
        _services.TryAddExtendedCacheServices(settings);
        _services.TryAddExtendedCacheServices(settings);
        _services.TryAddExtendedCacheServices(settings);

        var registrations = _services.Where(s => s.ServiceType == typeof(IFusionCache)).ToList();
        Assert.Single(registrations);
        var distributedRegistrations = _services.Where(s => s.ServiceType == typeof(IDistributedCache)).ToList();
        Assert.Single(distributedRegistrations);

        using var provider = _services.BuildServiceProvider();
        var fusionCache = provider.GetRequiredService<IFusionCache>();
        Assert.NotNull(fusionCache);
    }

    [Fact]
    public void TryAddFusionCoreServices_WithRedis_EnablesDistributedCacheAndBackplane()
    {
        var settings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            { "GlobalSettings:DistributedCache:Redis:ConnectionString", "localhost:6379" },
        });

        _services.TryAddExtendedCacheServices(settings);
        using var provider = _services.BuildServiceProvider();

        var fusionCache = provider.GetRequiredService<IFusionCache>();
        Assert.True(fusionCache.HasDistributedCache);
        Assert.True(fusionCache.HasBackplane);
    }

    [Fact]
    public void TryAddFusionCoreServices_WithExistingRedis_EnablesDistributedCacheAndBackplane()
    {
        var settings = CreateGlobalSettings(new Dictionary<string, string?>
        {
            { "GlobalSettings:DistributedCache:Redis:ConnectionString", "localhost:6379" },
        });

        _services.AddSingleton<IDistributedCache, RedisCache>();
        _services.TryAddExtendedCacheServices(settings);
        using var provider = _services.BuildServiceProvider();

        var fusionCache = provider.GetRequiredService<IFusionCache>();
        Assert.True(fusionCache.HasDistributedCache);
        Assert.True(fusionCache.HasBackplane);
    }

    [Fact]
    public void TryAddFusionCoreServices_WithoutRedis_DisablesDistributedCacheAndBackplane()
    {
        _services.TryAddExtendedCacheServices(_globalSettings);
        using var provider = _services.BuildServiceProvider();

        var fusionCache = provider.GetRequiredService<IFusionCache>();
        Assert.False(fusionCache.HasDistributedCache);
        Assert.False(fusionCache.HasBackplane);
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

using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Microsoft.Extensions.DependencyInjection;

public static class ExtendedCacheServiceCollectionExtensions
{
    /// <summary>
    /// Add Fusion Cache <see href="https://github.com/ZiggyCreatures/FusionCache"/> to the service
    /// collection.<br/>
    /// <br/>
    /// If Redis is configured, it uses Redis for an L2 cache and backplane. If not, it simply uses in-memory caching.
    /// </summary>
    public static IServiceCollection TryAddExtendedCacheServices(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (services.Any(s => s.ServiceType == typeof(IFusionCache)))
        {
            return services;
        }

        var fusionCacheBuilder = services.AddFusionCache()
            .WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = globalSettings.DistributedCache.DistributedCacheCircuitBreakerDuration;
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = globalSettings.DistributedCache.Duration,
                IsFailSafeEnabled = globalSettings.DistributedCache.IsFailSafeEnabled,
                FailSafeMaxDuration = globalSettings.DistributedCache.FailSafeMaxDuration,
                FailSafeThrottleDuration = globalSettings.DistributedCache.FailSafeThrottleDuration,
                EagerRefreshThreshold = globalSettings.DistributedCache.EagerRefreshThreshold,
                FactorySoftTimeout = globalSettings.DistributedCache.FactorySoftTimeout,
                FactoryHardTimeout = globalSettings.DistributedCache.FactoryHardTimeout,
                DistributedCacheSoftTimeout = globalSettings.DistributedCache.DistributedCacheSoftTimeout,
                DistributedCacheHardTimeout = globalSettings.DistributedCache.DistributedCacheHardTimeout,
                AllowBackgroundDistributedCacheOperations = globalSettings.DistributedCache.AllowBackgroundDistributedCacheOperations,
                JitterMaxDuration = globalSettings.DistributedCache.JitterMaxDuration
            })
            .WithSerializer(
                new FusionCacheSystemTextJsonSerializer()
            );

        if (!CoreHelpers.SettingHasValue(globalSettings.DistributedCache.Redis.ConnectionString))
        {
            return services;
        }

        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(globalSettings.DistributedCache.Redis.ConnectionString));

        fusionCacheBuilder
            .WithDistributedCache(sp =>
            {
                var cache = sp.GetService<IDistributedCache>();
                if (cache is not null)
                {
                    return cache;
                }
                var mux = sp.GetRequiredService<IConnectionMultiplexer>();
                return new RedisCache(new RedisCacheOptions
                {
                    ConnectionMultiplexerFactory = () => Task.FromResult(mux)
                });
            })
            .WithBackplane(sp =>
            {
                var backplane = sp.GetService<IFusionCacheBackplane>();
                if (backplane is not null)
                {
                    return backplane;
                }
                var mux = sp.GetRequiredService<IConnectionMultiplexer>();
                return new RedisBackplane(new RedisBackplaneOptions
                {
                    ConnectionMultiplexerFactory = () => Task.FromResult(mux)
                });
            });

        return services;
    }
}

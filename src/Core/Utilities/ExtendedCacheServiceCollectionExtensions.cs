using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Microsoft.Extensions.DependencyInjection;

public static class ExtendedCacheServiceCollectionExtensions
{
    /// <summary>
    /// Adds a new, named Fusion Cache <see href="https://github.com/ZiggyCreatures/FusionCache"/> to the service
    /// collection. If an existing cache of the same name is found, it will do nothing.<br/>
    /// <br/>
    /// <b>Note</b>: When re-using an existing distributed cache, it is expected to call this method <b>after</b> calling
    /// <code>services.AddDistributedCache(globalSettings)</code><br />This ensures that DI correctly finds
    /// and re-uses the shared distributed cache infrastructure.<br />
    /// <br />
    /// <b>Backplane</b>: Cross-instance cache invalidation is only available when using Redis.
    /// Non-Redis distributed caches operate with eventual consistency across multiple instances.
    /// </summary>
    public static IServiceCollection AddExtendedCache(
        this IServiceCollection services,
        string cacheName,
        GlobalSettings globalSettings,
        GlobalSettings.ExtendedCacheSettings? settings = null)
    {
        settings ??= globalSettings.DistributedCache.DefaultExtendedCache;
        if (settings is null || string.IsNullOrEmpty(cacheName))
        {
            return services;
        }

        // If a cache already exists with this key, do nothing
        if (services.Any(s => s.ServiceType == typeof(IFusionCache) &&
                         s.ServiceKey?.Equals(cacheName) == true))
        {
            return services;
        }

        if (services.All(s => s.ServiceType != typeof(FusionCacheSystemTextJsonSerializer)))
        {
            services.AddFusionCacheSystemTextJsonSerializer();
        }
        var fusionCacheBuilder = services
            .AddFusionCache(cacheName)
            .WithCacheKeyPrefix($"{cacheName}:")
            .AsKeyedServiceByCacheName()
            .WithOptions(opt =>
            {
                opt.DistributedCacheCircuitBreakerDuration = settings.DistributedCacheCircuitBreakerDuration;
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = settings.Duration,
                IsFailSafeEnabled = settings.IsFailSafeEnabled,
                FailSafeMaxDuration = settings.FailSafeMaxDuration,
                FailSafeThrottleDuration = settings.FailSafeThrottleDuration,
                EagerRefreshThreshold = settings.EagerRefreshThreshold,
                FactorySoftTimeout = settings.FactorySoftTimeout,
                FactoryHardTimeout = settings.FactoryHardTimeout,
                DistributedCacheSoftTimeout = settings.DistributedCacheSoftTimeout,
                DistributedCacheHardTimeout = settings.DistributedCacheHardTimeout,
                AllowBackgroundDistributedCacheOperations = settings.AllowBackgroundDistributedCacheOperations,
                JitterMaxDuration = settings.JitterMaxDuration
            })
            .WithRegisteredSerializer();

        if (!settings.EnableDistributedCache)
            return services;

        if (settings.UseSharedDistributedCache)
        {
            if (!CoreHelpers.SettingHasValue(globalSettings.DistributedCache.Redis.ConnectionString))
            {
                // Using Shared Non-Redis Distributed Cache:
                //   1. Assume IDistributedCache is already registered (e.g., Cosmos, SQL Server)
                //   2. Backplane not supported (Redis-only feature, requires pub/sub)

                fusionCacheBuilder
                    .TryWithRegisteredDistributedCache();

                return services;
            }

            // Using Shared Redis, TryAdd and reuse all pieces (multiplexer, distributed cache and backplane)

            services.TryAddSingleton<IConnectionMultiplexer>(sp =>
                CreateConnectionMultiplexer(sp, cacheName, globalSettings.DistributedCache.Redis.ConnectionString));

            services.TryAddSingleton<IDistributedCache>(sp =>
            {
                var mux = sp.GetRequiredService<IConnectionMultiplexer>();
                return new RedisCache(new RedisCacheOptions
                {
                    ConnectionMultiplexerFactory = () => Task.FromResult(mux)
                });
            });

            services.TryAddSingleton<IFusionCacheBackplane>(sp =>
            {
                var mux = sp.GetRequiredService<IConnectionMultiplexer>();
                return new RedisBackplane(new RedisBackplaneOptions
                {
                    ConnectionMultiplexerFactory = () => Task.FromResult(mux)
                });
            });

            fusionCacheBuilder
                .WithRegisteredDistributedCache()
                .WithRegisteredBackplane();

            return services;
        }

        // Using keyed Distributed Cache. Create/Reuse all pieces as keyed services.

        if (!CoreHelpers.SettingHasValue(settings.Redis.ConnectionString))
        {
            // Using Keyed Non-Redis Distributed Cache:
            //   1. Assume IDistributedCache (e.g., Cosmos, SQL Server) is already registered with cacheName as key
            //   2. Backplane not supported (Redis-only feature, requires pub/sub)

            fusionCacheBuilder
                .TryWithRegisteredKeyedDistributedCache(serviceKey: cacheName);

            return services;
        }

        // Using Keyed Redis: TryAdd and reuse all pieces (multiplexer, distributed cache and backplane)

        services.TryAddKeyedSingleton<IConnectionMultiplexer>(
            cacheName,
            (sp, _) => CreateConnectionMultiplexer(sp, cacheName, settings.Redis.ConnectionString)
        );
        services.TryAddKeyedSingleton<IDistributedCache>(
            cacheName,
            (sp, _) =>
            {
                var mux = sp.GetRequiredKeyedService<IConnectionMultiplexer>(cacheName);
                return new RedisCache(new RedisCacheOptions
                {
                    ConnectionMultiplexerFactory = () => Task.FromResult(mux)
                });
            }
        );
        services.TryAddKeyedSingleton<IFusionCacheBackplane>(
            cacheName,
            (sp, _) =>
            {
                var mux = sp.GetRequiredKeyedService<IConnectionMultiplexer>(cacheName);
                return new RedisBackplane(new RedisBackplaneOptions
                {
                    ConnectionMultiplexerFactory = () => Task.FromResult(mux)
                });
            }
        );

        fusionCacheBuilder
            .WithRegisteredKeyedDistributedCacheByCacheName()
            .WithRegisteredKeyedBackplaneByCacheName();

        return services;
    }

    private static ConnectionMultiplexer CreateConnectionMultiplexer(IServiceProvider sp, string cacheName,
        string connectionString)
    {
        try
        {
            return ConnectionMultiplexer.Connect(connectionString);
        }
        catch (Exception ex)
        {
            var logger = sp.GetService<ILogger>();
            logger?.LogError(ex, "Failed to connect to Redis for cache {CacheName}", cacheName);
            throw;
        }
    }
}

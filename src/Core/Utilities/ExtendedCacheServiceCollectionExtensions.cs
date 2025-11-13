using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using ZiggyCreatures.Caching.Fusion;
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

        if (CoreHelpers.SettingHasValue(globalSettings.DistributedCache.Redis.ConnectionString))
        {
            // Add Redis Cache if one doesn't already exist
            if (services.All(s => s.ServiceType != typeof(IDistributedCache)))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = globalSettings.DistributedCache.Redis.ConnectionString;
                });
            }
            // Add Redis Backplane
            services.AddFusionCacheStackExchangeRedisBackplane(opt =>
            {
                opt.Configuration = globalSettings.DistributedCache.Redis.ConnectionString;
            });

            fusionCacheBuilder
                .WithRegisteredDistributedCache()
                .WithRegisteredBackplane();
        }

        return services;
    }
}

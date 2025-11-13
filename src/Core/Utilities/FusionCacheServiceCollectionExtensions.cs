using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Microsoft.Extensions.DependencyInjection;

public static class FusionCacheServiceCollectionExtensions
{
    /// <summary>
    /// Add Fusion Cache <see href="https://github.com/ZiggyCreatures/FusionCache"/> to the service
    /// collection.<br/>
    /// <br/>
    /// If Redis is configured, it uses Redis for an L2 cache and backplane. If not, it simply uses in-memory caching.
    /// </summary>
    public static IServiceCollection TryAddFusionCacheServices(this IServiceCollection services, GlobalSettings globalSettings)
    {
        if (services.Any(s => s.ServiceType == typeof(IFusionCache)))
        {
            return services;
        }

        var fusionCacheBuilder = services.AddFusionCache()
            .WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = globalSettings.FusionCache.DistributedCacheCircuitBreakerDuration;
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = globalSettings.FusionCache.Duration,
                IsFailSafeEnabled = globalSettings.FusionCache.IsFailSafeEnabled,
                FailSafeMaxDuration = globalSettings.FusionCache.FailSafeMaxDuration,
                FailSafeThrottleDuration = globalSettings.FusionCache.FailSafeThrottleDuration,
                EagerRefreshThreshold = globalSettings.FusionCache.EagerRefreshThreshold,
                FactorySoftTimeout = globalSettings.FusionCache.FactorySoftTimeout,
                FactoryHardTimeout = globalSettings.FusionCache.FactoryHardTimeout,
                DistributedCacheSoftTimeout = globalSettings.FusionCache.DistributedCacheSoftTimeout,
                DistributedCacheHardTimeout = globalSettings.FusionCache.DistributedCacheHardTimeout,
                AllowBackgroundDistributedCacheOperations = globalSettings.FusionCache.AllowBackgroundDistributedCacheOperations,
                JitterMaxDuration = globalSettings.FusionCache.JitterMaxDuration
            })
            .WithSerializer(
                new FusionCacheSystemTextJsonSerializer()
            );

        if (CoreHelpers.SettingHasValue(globalSettings.DistributedCache.Redis.ConnectionString))
        {
            fusionCacheBuilder
                .WithDistributedCache(new RedisCache(new RedisCacheOptions()
                {
                    Configuration = globalSettings.DistributedCache.Redis.ConnectionString
                }))
                .WithBackplane(new RedisBackplane(new RedisBackplaneOptions()
                {
                    Configuration = globalSettings.DistributedCache.Redis.ConnectionString
                }));
        }

        return services;
    }
}

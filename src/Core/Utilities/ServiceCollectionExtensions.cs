using AspNetCoreRateLimit;
using AspNetCoreRateLimit.Redis;
using Bit.Core.HostedServices;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Bit.Core.Utilities
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds either an in-memory or distributed IP rate limiter depending if a Redis connection string is available.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="globalSettings"></param>
        public static IServiceCollection AddIpRateLimiting(this IServiceCollection services,
            GlobalSettings globalSettings)
        {
            services.AddHostedService<IpRateLimitSeedStartupService>();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

            if (string.IsNullOrEmpty(globalSettings.Redis.ConnectionString))
            {
                services.AddInMemoryRateLimiting();
            }
            else
            {
                services.AddRedisRateLimiting(); // Requires a registered IConnectionMultiplexer 
            }

            return services;
        }

        /// <summary>
        ///     Adds an implementation of <see cref="IDistributedCache"/> to the service collection. Uses a memory
        /// cache if self hosted or no Redis connection string is available in GlobalSettings.
        /// </summary>
        public static IServiceCollection AddDistributedCache(
            this IServiceCollection services,
            GlobalSettings globalSettings)
        {
            if (globalSettings.SelfHosted || string.IsNullOrEmpty(globalSettings.Redis.ConnectionString))
            {
                services.AddDistributedMemoryCache();
                return services;
            }

            // Register the IConnectionMultiplexer explicitly so it can be accessed via DI
            // (e.g. for the IP rate limiting store)
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(globalSettings.Redis.ConnectionString));

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = globalSettings.Redis.ConnectionString;
            });

            // TODO: Explicitly register IDistributedCache to re-use existing IConnectionMultiplexer after net6 upgrade
            // The multiplexer factory is available in Microsoft.Extensions.Caching.StackExchangeRedis v6
            // And will reduce the number of redundant connections to the Redis instance
            // services.AddSingleton<IDistributedCache>(s =>
            // {
            //     return new RedisCache(new RedisCacheOptions
            //     {
            //         ConnectionMultiplexerFactory = () =>
            //             Task.FromResult(s.GetRequiredService<IConnectionMultiplexer>())
            //     });
            // });

            return services;
        }
    }
}

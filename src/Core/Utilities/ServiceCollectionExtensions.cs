using AspNetCoreRateLimit;
using AspNetCoreRateLimit.Redis;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

            if (string.IsNullOrEmpty(globalSettings.Redis.ConnectionString))
                services.AddInMemoryRateLimiting();
            else
                services.AddRedisRateLimiting();

            return services;
        }
    }
}

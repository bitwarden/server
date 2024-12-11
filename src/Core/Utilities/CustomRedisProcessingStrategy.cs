using AspNetCoreRateLimit;
using AspNetCoreRateLimit.Redis;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Bit.Core.Utilities;

/// <summary>
/// A modified version of <see cref="AspNetCoreRateLimit.Redis.RedisProcessingStrategy"/> that gracefully
/// handles a disrupted Redis connection. If the connection is down or the number of failed requests within
/// a given time period exceed the configured threshold, then rate limiting is temporarily disabled.
/// </summary>
/// <remarks>
/// This is necessary to ensure the service does not become unresponsive due to Redis being out of service. As
/// the default implementation would throw an exception and exit the request pipeline for all requests.
/// </remarks>
public class CustomRedisProcessingStrategy : RedisProcessingStrategy
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<CustomRedisProcessingStrategy> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly GlobalSettings.DistributedIpRateLimitingSettings _distributedSettings;

    private const string _redisTimeoutCacheKey = "IpRateLimitRedisTimeout";

    public CustomRedisProcessingStrategy(
        [FromKeyedServices("rate-limiter")] IConnectionMultiplexer connectionMultiplexer,
        IRateLimitConfiguration config,
        ILogger<CustomRedisProcessingStrategy> logger,
        IMemoryCache memoryCache,
        GlobalSettings globalSettings
    )
        : base(connectionMultiplexer, config, logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
        _memoryCache = memoryCache;
        _distributedSettings = globalSettings.DistributedIpRateLimiting;
    }

    public override async Task<RateLimitCounter> ProcessRequestAsync(
        ClientRequestIdentity requestIdentity,
        RateLimitRule rule,
        ICounterKeyBuilder counterKeyBuilder,
        RateLimitOptions rateLimitOptions,
        CancellationToken cancellationToken = default
    )
    {
        // If Redis is down entirely, skip rate limiting
        if (!_connectionMultiplexer.IsConnected)
        {
            _logger.LogDebug("Redis connection is down, skipping IP rate limiting");
            return SkipRateLimitResult();
        }

        // Check if any Redis timeouts have occurred recently
        if (_memoryCache.TryGetValue<TimeoutCounter>(_redisTimeoutCacheKey, out var timeoutCounter))
        {
            // We've exceeded threshold, backoff Redis and skip rate limiting for now
            if (timeoutCounter.Count >= _distributedSettings.MaxRedisTimeoutsThreshold)
            {
                _logger.LogDebug(
                    "Redis timeout threshold has been exceeded, backing off and skipping IP rate limiting"
                );
                return SkipRateLimitResult();
            }
        }

        try
        {
            return await base.ProcessRequestAsync(
                requestIdentity,
                rule,
                counterKeyBuilder,
                rateLimitOptions,
                cancellationToken
            );
        }
        catch (Exception ex) when (ex is RedisTimeoutException || ex is RedisConnectionException)
        {
            _logger.LogWarning(ex, "Redis appears down, skipping rate limiting");
            // If this is the first timeout/connection error we've had, start a new counter and sliding window
            timeoutCounter ??= new TimeoutCounter()
            {
                Count = 0,
                ExpiresAt = DateTime.UtcNow.AddSeconds(_distributedSettings.SlidingWindowSeconds),
            };
            timeoutCounter.Count++;

            _memoryCache.Set(
                _redisTimeoutCacheKey,
                timeoutCounter,
                new MemoryCacheEntryOptions { AbsoluteExpiration = timeoutCounter.ExpiresAt }
            );

            // Just because Redis timed out does not mean we should kill the request
            return SkipRateLimitResult();
        }
    }

    /// <summary>
    /// A RateLimitCounter result used when the rate limiting middleware should
    /// fail open and allow the request to proceed without checking request limits.
    /// </summary>
    private static RateLimitCounter SkipRateLimitResult()
    {
        return new RateLimitCounter { Count = 0, Timestamp = DateTime.UtcNow };
    }

    internal class TimeoutCounter
    {
        public DateTime ExpiresAt { get; init; }

        public int Count { get; set; }
    }
}

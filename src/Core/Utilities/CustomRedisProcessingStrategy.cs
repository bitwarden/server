using AspNetCoreRateLimit;
using Bit.Core.Settings;
using Microsoft.Extensions.Caching.Memory;
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
public class CustomRedisProcessingStrategy : ProcessingStrategy
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IRateLimitConfiguration _config;
    private readonly ILogger<CustomRedisProcessingStrategy> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly GlobalSettings.DistributedIpRateLimitingSettings _distributedSettings;

    private const string _redisTimeoutCacheKey = "IpRateLimitRedisTimeout";

    public CustomRedisProcessingStrategy(
        IConnectionMultiplexer connectionMultiplexer,
        IRateLimitConfiguration config,
        ILogger<CustomRedisProcessingStrategy> logger,
        IMemoryCache memoryCache,
        GlobalSettings globalSettings)
        : base(config)
    {
        _connectionMultiplexer = connectionMultiplexer ??
                                 throw new ArgumentException(
                                     "IConnectionMultiplexer was null. Ensure StackExchange.Redis was successfully registered");
        _config = config;
        _logger = logger;
        _memoryCache = memoryCache;
        _distributedSettings = globalSettings.DistributedIpRateLimiting;
    }

    private static readonly LuaScript _atomicIncrement = LuaScript.Prepare(
        "local count = redis.call(\"INCRBYFLOAT\", @key, tonumber(@delta)) local ttl = redis.call(\"TTL\", @key) if ttl == -1 then redis.call(\"EXPIRE\", @key, @timeout) end return count");

    public override async Task<RateLimitCounter> ProcessRequestAsync(ClientRequestIdentity requestIdentity,
        RateLimitRule rule, ICounterKeyBuilder counterKeyBuilder, RateLimitOptions rateLimitOptions,
        CancellationToken cancellationToken = default)
    {
        // If Redis is down entirely, skip rate limiting
        if (!_connectionMultiplexer.IsConnected)
        {
            _logger.LogDebug("Redis connection is down, skipping IP rate limiting");
            return SkipRateLimitResult();
        }

        // Check if any Redis timeouts have occured recently
        if (_memoryCache.TryGetValue<TimeoutCounter>(_redisTimeoutCacheKey, out var timeoutCounter))
        {
            // We've exceeded threshold, backoff Redis and skip rate limiting for now
            if (timeoutCounter.Count >= _distributedSettings.MaxRedisTimeoutsThreshold)
            {
                _logger.LogDebug(
                    "Redis timeout threshold has been exceeded, backing off and skipping IP rate limiting");
                return SkipRateLimitResult();
            }
        }

        var counterId = BuildCounterKey(requestIdentity, rule, counterKeyBuilder, rateLimitOptions);

        try
        {
            return await IncrementAsync(counterId, rule.PeriodTimespan ?? rule.Period.ToTimeSpan(), _config.RateIncrementer);
        }
        catch (RedisTimeoutException)
        {
            // If this is the first timeout we've had, start a new counter and sliding window 
            timeoutCounter ??= new TimeoutCounter()
            {
                Count = 0,
                ExpiresAt = DateTime.UtcNow.AddSeconds(_distributedSettings.SlidingWindowSeconds)
            };
            timeoutCounter.Count++;

            _memoryCache.Set(_redisTimeoutCacheKey, timeoutCounter,
                new MemoryCacheEntryOptions { AbsoluteExpiration = timeoutCounter.ExpiresAt });

            // Just because Redis timed out does not mean we should kill the request
            return SkipRateLimitResult();
        }
    }

    private async Task<RateLimitCounter> IncrementAsync(string counterId, TimeSpan interval,
        Func<double> rateIncrementer = null)
    {
        var now = DateTime.UtcNow;
        var numberOfIntervals = now.Ticks / interval.Ticks;
        var intervalStart = new DateTime(numberOfIntervals * interval.Ticks, DateTimeKind.Utc);

        _logger.LogDebug("Calling Lua script. {CounterId}, {Timeout}, {Delta}", counterId, interval.TotalSeconds, 1D);
        var count = await _connectionMultiplexer.GetDatabase().ScriptEvaluateAsync(_atomicIncrement,
            new
            {
                key = new RedisKey(counterId),
                timeout = interval.TotalSeconds,
                delta = rateIncrementer?.Invoke() ?? 1D
            });
        return new RateLimitCounter { Count = (double)count, Timestamp = intervalStart };
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

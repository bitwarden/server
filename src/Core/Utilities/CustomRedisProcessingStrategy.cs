using AspNetCoreRateLimit;
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

    /// <summary>
    /// Maximum number of Redis timeouts that can be experienced within the sliding timeout
    /// window before IP rate limiting is temporarily disabled.
    /// </summary>
    private const int _maxRedisTimeoutCount = 1;

    /// <summary>
    /// Length of the sliding window to track Redis timeout exceptions.
    /// </summary>
    private static readonly TimeSpan _redisTimeoutWindow = TimeSpan.FromSeconds(30);

    public CustomRedisProcessingStrategy(
        IConnectionMultiplexer connectionMultiplexer,
        IRateLimitConfiguration config,
        ILogger<CustomRedisProcessingStrategy> logger,
        IMemoryCache memoryCache)
        : base(config)
    {
        _connectionMultiplexer = connectionMultiplexer ??
                                 throw new ArgumentException(
                                     "IConnectionMultiplexer was null. Ensure StackExchange.Redis was successfully registered");
        _config = config;
        _logger = logger;
        _memoryCache = memoryCache;
    }

    private static readonly LuaScript _atomicIncrement = LuaScript.Prepare(
        "local count = redis.call(\"INCRBYFLOAT\", @key, tonumber(@delta)) local ttl = redis.call(\"TTL\", @key) if ttl == -1 then redis.call(\"EXPIRE\", @key, @timeout) end return count");

    public override async Task<RateLimitCounter> ProcessRequestAsync(ClientRequestIdentity requestIdentity,
        RateLimitRule rule, ICounterKeyBuilder counterKeyBuilder, RateLimitOptions rateLimitOptions,
        CancellationToken cancellationToken = default)
    {
        // If Redis is down entirely, don't attempt any rate limiting
        if (!_connectionMultiplexer.IsConnected)
        {
            _logger.LogDebug("Redis connection is down, skipping IP rate limiting");
            return DisabledRateLimitResult();
        }

        // Check if any Redis timeouts have occured recently
        if (_memoryCache.TryGetValue<TimeoutCounter>("redisTimeout", out var timeoutCounter))
        {
            // We've exceeded threshold, backoff Redis and don't attempt rate limiting for now
            if (timeoutCounter.Count >= _maxRedisTimeoutCount)
            {
                _logger.LogDebug(
                    "Redis timeout threshold has been exceeded, backing off and skipping IP rate limiting");
                return DisabledRateLimitResult();
            }
        }

        var counterId = BuildCounterKey(requestIdentity, rule, counterKeyBuilder, rateLimitOptions);

        try
        {
            return await IncrementAsync(counterId, rule.PeriodTimespan.Value, _config.RateIncrementer);
        }
        catch (RedisTimeoutException)
        {
            // If this is the first timeout we've had, start a new counter and sliding window 
            timeoutCounter ??= new TimeoutCounter()
            {
                Count = 0,
                ExpiresAt = DateTime.UtcNow.Add(_redisTimeoutWindow)
            };
            timeoutCounter.Count++;

            _memoryCache.Set("redisTimeout", timeoutCounter,
                new MemoryCacheEntryOptions { AbsoluteExpiration = timeoutCounter.ExpiresAt });

            // Just because Redis timed out does not mean we should kill the request
            return DisabledRateLimitResult();
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
    private static RateLimitCounter DisabledRateLimitResult()
    {
        return new RateLimitCounter { Count = 0, Timestamp = DateTime.UtcNow };
    }

    private class TimeoutCounter
    {
        public DateTime ExpiresAt { get; init; }

        public int Count { get; set; }
    }
}

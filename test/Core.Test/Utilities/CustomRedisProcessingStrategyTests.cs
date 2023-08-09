using AspNetCoreRateLimit;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class CustomRedisProcessingStrategyTests
{
    #region Sample RateLimit Options for Testing

    private readonly GlobalSettings _sampleSettings = new()
    {
        DistributedIpRateLimiting = new GlobalSettings.DistributedIpRateLimitingSettings
        {
            Enabled = true,
            MaxRedisTimeoutsThreshold = 2,
            SlidingWindowSeconds = 5
        }
    };

    private readonly ClientRequestIdentity _sampleClientId = new()
    {
        ClientId = "test",
        ClientIp = "127.0.0.1",
        HttpVerb = "GET",
        Path = "/"
    };

    private readonly RateLimitRule _sampleRule = new() { Endpoint = "/", Limit = 5, Period = "1m", PeriodTimespan = TimeSpan.FromMinutes(1) };

    private readonly RateLimitOptions _sampleOptions = new() { };

    #endregion

    private readonly ICounterKeyBuilder _mockCounterKeyBuilder = Substitute.For<ICounterKeyBuilder>();
    private IDatabase _mockDb;

    public CustomRedisProcessingStrategyTests()
    {
        _mockCounterKeyBuilder.Build(Arg.Any<ClientRequestIdentity>(), Arg.Any<RateLimitRule>())
            .Returns(_sampleClientId.ClientId);
    }

    [Fact]
    public async Task IncrementRateLimitCount_When_RedisIsHealthy()
    {
        // Arrange
        var strategy = BuildProcessingStrategy();

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder, _sampleOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(1, result.Count);
        VerifyRedisCalls(1);
    }

    [Fact]
    public async Task SkipRateLimit_When_RedisIsDown()
    {
        // Arrange
        var strategy = BuildProcessingStrategy(false);

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder, _sampleOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Count);
        NotCalled();
    }

    [Fact]
    public async Task SkipRateLimit_When_TimeoutThresholdExceeded()
    {
        // Arrange
        var mockCache = Substitute.For<IMemoryCache>();
        object existingCount = new CustomRedisProcessingStrategy.TimeoutCounter
        {
            Count = _sampleSettings.DistributedIpRateLimiting.MaxRedisTimeoutsThreshold + 1
        };
        mockCache.TryGetValue(Arg.Any<object>(), out existingCount).ReturnsForAnyArgs(x =>
        {
            x[1] = existingCount;
            return true;
        });

        var strategy = BuildProcessingStrategy(mockCache: mockCache);

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder, _sampleOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Count);
        NotCalled();
    }

    [Fact]
    public async Task SkipRateLimit_When_RedisTimeoutException()
    {
        // Arrange
        var mockCache = Substitute.For<IMemoryCache>();
        var mockCacheEntry = Substitute.For<ICacheEntry>();
        mockCache.CreateEntry(Arg.Any<object>()).Returns(mockCacheEntry);

        var strategy = BuildProcessingStrategy(mockCache: mockCache, throwRedisTimeout: true);

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder, _sampleOptions,
            CancellationToken.None);

        var timeoutCounter = ((CustomRedisProcessingStrategy.TimeoutCounter)mockCacheEntry.Value);

        // Assert
        Assert.Equal(0, result.Count); // Skip rate limiting
        VerifyRedisCalls(1);

        Assert.Equal(1, timeoutCounter.Count); // Timeout count increased/cached
        Assert.NotNull(mockCacheEntry.AbsoluteExpiration);
        mockCache.Received().CreateEntry(Arg.Any<object>());
    }

    [Fact]
    public async Task BackoffRedis_After_ThresholdExceeded()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var strategy = BuildProcessingStrategy(mockCache: memoryCache, throwRedisTimeout: true);

        // Act

        // Redis Timeout 1
        await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder, _sampleOptions,
            CancellationToken.None);

        // Redis Timeout 2
        await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder, _sampleOptions,
            CancellationToken.None);

        // Skip Redis
        await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder, _sampleOptions,
            CancellationToken.None);

        // Assert
        VerifyRedisCalls(_sampleSettings.DistributedIpRateLimiting.MaxRedisTimeoutsThreshold);
    }

    private void VerifyRedisCalls(int times)
    {
        _mockDb
            .Received(times)
            .ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object>(), Arg.Any<CommandFlags>());
    }

    private void NotCalled()
    {
        _mockDb
            .DidNotReceive()
            .ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object>(), Arg.Any<CommandFlags>());
    }

    private CustomRedisProcessingStrategy BuildProcessingStrategy(
        bool isRedisConnected = true,
        bool throwRedisTimeout = false,
        IMemoryCache mockCache = null)
    {
        var mockRedisConnection = Substitute.For<IConnectionMultiplexer>();

        mockRedisConnection.IsConnected.Returns(isRedisConnected);

        _mockDb = Substitute.For<IDatabase>();

        var mockScriptEvaluate = _mockDb
            .ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object>(), Arg.Any<CommandFlags>());

        if (throwRedisTimeout)
        {
            mockScriptEvaluate.Returns<RedisResult>(x => throw new RedisTimeoutException("Timeout", CommandStatus.WaitingToBeSent));
        }
        else
        {
            mockScriptEvaluate.Returns(RedisResult.Create(1));
        }

        mockRedisConnection.GetDatabase(Arg.Any<int>(), Arg.Any<object>())
            .Returns(_mockDb);

        var mockLogger = Substitute.For<ILogger<CustomRedisProcessingStrategy>>();
        var mockConfig = Substitute.For<IRateLimitConfiguration>();

        mockCache ??= Substitute.For<IMemoryCache>();

        return new CustomRedisProcessingStrategy(mockRedisConnection, mockConfig,
            mockLogger, mockCache, _sampleSettings);
    }
}

using AspNetCoreRateLimit;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
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

    private readonly Mock<ICounterKeyBuilder> _mockCounterKeyBuilder = new();
    private Mock<IDatabase> _mockDb;

    public CustomRedisProcessingStrategyTests()
    {
        _mockCounterKeyBuilder
            .Setup(x =>
                x.Build(It.IsAny<ClientRequestIdentity>(), It.IsAny<RateLimitRule>()))
            .Returns(_sampleClientId.ClientId);
    }

    [Fact]
    public async Task IncrementRateLimitCount_When_RedisIsHealthy()
    {
        // Arrange
        var strategy = BuildProcessingStrategy();

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder.Object, _sampleOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(1, result.Count);
        VerifyRedisCalls(Times.Once());
    }

    [Fact]
    public async Task SkipRateLimit_When_RedisIsDown()
    {
        // Arrange
        var strategy = BuildProcessingStrategy(false);

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder.Object, _sampleOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Count);
        VerifyRedisCalls(Times.Never());
    }

    [Fact]
    public async Task SkipRateLimit_When_TimeoutThresholdExceeded()
    {
        // Arrange
        var mockCache = new Mock<IMemoryCache>();
        object existingCount = new CustomRedisProcessingStrategy.TimeoutCounter
        {
            Count = _sampleSettings.DistributedIpRateLimiting.MaxRedisTimeoutsThreshold + 1
        };
        mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out existingCount)).Returns(true);

        var strategy = BuildProcessingStrategy(mockCache: mockCache.Object);

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder.Object, _sampleOptions,
            CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Count);
        VerifyRedisCalls(Times.Never());
    }

    [Fact]
    public async Task SkipRateLimit_When_RedisTimeoutException()
    {
        // Arrange
        var mockCache = new Mock<IMemoryCache>();
        var mockCacheEntry = new Mock<ICacheEntry>();
        mockCacheEntry.SetupAllProperties();
        mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

        var strategy = BuildProcessingStrategy(mockCache: mockCache.Object, throwRedisTimeout: true);

        // Act
        var result = await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder.Object, _sampleOptions,
            CancellationToken.None);

        var timeoutCounter = ((CustomRedisProcessingStrategy.TimeoutCounter)mockCacheEntry.Object.Value);

        // Assert
        Assert.Equal(0, result.Count); // Skip rate limiting
        VerifyRedisCalls(Times.Once());

        Assert.Equal(1, timeoutCounter.Count); // Timeout count increased/cached
        Assert.NotNull(mockCacheEntry.Object.AbsoluteExpiration);
        mockCache.Verify(x => x.CreateEntry(It.IsAny<object>()));
    }

    [Fact]
    public async Task BackoffRedis_After_ThresholdExceeded()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var strategy = BuildProcessingStrategy(mockCache: memoryCache, throwRedisTimeout: true);

        // Act

        // Redis Timeout 1
        await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder.Object, _sampleOptions,
            CancellationToken.None);

        // Redis Timeout 2
        await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder.Object, _sampleOptions,
            CancellationToken.None);

        // Skip Redis
        await strategy.ProcessRequestAsync(_sampleClientId, _sampleRule, _mockCounterKeyBuilder.Object, _sampleOptions,
            CancellationToken.None);

        // Assert
        VerifyRedisCalls(Times.Exactly(_sampleSettings.DistributedIpRateLimiting.MaxRedisTimeoutsThreshold));
    }

    private void VerifyRedisCalls(Times times)
    {
        _mockDb.Verify(x =>
            x.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), It.IsAny<CommandFlags>()),
            times);
    }

    private CustomRedisProcessingStrategy BuildProcessingStrategy(
        bool isRedisConnected = true,
        bool throwRedisTimeout = false,
        IMemoryCache mockCache = null)
    {
        var mockRedisConnection = new Mock<IConnectionMultiplexer>();

        mockRedisConnection.Setup(x => x.IsConnected).Returns(isRedisConnected);

        _mockDb = new Mock<IDatabase>();

        var mockScriptEvaluate = _mockDb
            .Setup(x =>
                x.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), It.IsAny<CommandFlags>()));

        if (throwRedisTimeout)
        {
            mockScriptEvaluate.ThrowsAsync(new RedisTimeoutException("Timeout", CommandStatus.WaitingToBeSent));
        }
        else
        {
            mockScriptEvaluate.ReturnsAsync(RedisResult.Create(1));
        }

        mockRedisConnection
            .Setup(x =>
                x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);

        var mockLogger = new Mock<ILogger<CustomRedisProcessingStrategy>>();
        var mockConfig = new Mock<IRateLimitConfiguration>();

        mockCache ??= new Mock<IMemoryCache>().Object;

        return new CustomRedisProcessingStrategy(mockRedisConnection.Object, mockConfig.Object,
            mockLogger.Object, mockCache, _sampleSettings);
    }
}

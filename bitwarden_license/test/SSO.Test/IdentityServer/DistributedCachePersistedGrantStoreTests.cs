using Bit.Sso.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.SSO.Test.IdentityServer;

public class DistributedCachePersistedGrantStoreTests
{
    private readonly IFusionCache _cache;
    private readonly DistributedCachePersistedGrantStore _sut;

    public DistributedCachePersistedGrantStoreTests()
    {
        _cache = Substitute.For<IFusionCache>();
        _sut = new DistributedCachePersistedGrantStore(_cache);
    }

    [Fact]
    public async Task StoreAsync_StoresGrantWithCalculatedTTL()
    {
        // Arrange
        var grant = CreateTestGrant("test-key", expiration: DateTime.UtcNow.AddMinutes(5));

        // Act
        await _sut.StoreAsync(grant);

        // Assert
        await _cache.Received(1).SetAsync(
            "test-key",
            grant,
            Arg.Is<FusionCacheEntryOptions>(opts =>
                opts.Duration >= TimeSpan.FromMinutes(4.9) &&
                opts.Duration <= TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task StoreAsync_WithNoExpiration_UsesDefaultFiveMinuteTTL()
    {
        // Arrange
        var grant = CreateTestGrant("no-expiry-key", expiration: null);

        // Act
        await _sut.StoreAsync(grant);

        // Assert
        await _cache.Received(1).SetAsync(
            "no-expiry-key",
            grant,
            Arg.Is<FusionCacheEntryOptions>(opts => opts.Duration == TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task StoreAsync_WithAlreadyExpiredGrant_DoesNotStore()
    {
        // Arrange
        var expiredGrant = CreateTestGrant("expired-key", expiration: DateTime.UtcNow.AddMinutes(-1));

        // Act
        await _sut.StoreAsync(expiredGrant);

        // Assert
        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<PersistedGrant>(),
            Arg.Any<FusionCacheEntryOptions?>());
    }

    [Fact]
    public async Task StoreAsync_EnablesDistributedCache()
    {
        // Arrange
        var grant = CreateTestGrant("distributed-key", expiration: DateTime.UtcNow.AddMinutes(5));

        // Act
        await _sut.StoreAsync(grant);

        // Assert
        await _cache.Received(1).SetAsync(
            "distributed-key",
            grant,
            Arg.Is<FusionCacheEntryOptions>(opts =>
                opts.SkipDistributedCache == false &&
                opts.SkipDistributedCacheReadWhenStale == false));
    }

    [Fact]
    public async Task RemoveAsync_RemovesGrantFromCache()
    {
        // Act
        await _sut.RemoveAsync("remove-key");

        // Assert
        await _cache.Received(1).RemoveAsync("remove-key");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyCollection()
    {
        // Arrange
        var filter = new PersistedGrantFilter
        {
            SubjectId = "test-subject",
            SessionId = "test-session",
            ClientId = "test-client",
            Type = "authorization_code"
        };

        // Act
        var result = await _sut.GetAllAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveAllAsync_CompletesWithoutError()
    {
        // Arrange
        var filter = new PersistedGrantFilter
        {
            SubjectId = "test-subject",
            ClientId = "test-client"
        };

        // Act & Assert - should not throw
        await _sut.RemoveAllAsync(filter);

        // Verify no cache operations were performed
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task StoreAsync_PreservesAllGrantProperties()
    {
        // Arrange
        var grant = new PersistedGrant
        {
            Key = "full-grant-key",
            Type = "authorization_code",
            SubjectId = "user-123",
            SessionId = "session-456",
            ClientId = "client-789",
            Description = "Test grant",
            CreationTime = DateTime.UtcNow.AddMinutes(-1),
            Expiration = DateTime.UtcNow.AddMinutes(5),
            ConsumedTime = null,
            Data = "{\"test\":\"data\"}"
        };

        PersistedGrant? capturedGrant = null;
        await _cache.SetAsync(
            Arg.Any<string>(),
            Arg.Do<PersistedGrant>(g => capturedGrant = g),
            Arg.Any<FusionCacheEntryOptions?>());

        // Act
        await _sut.StoreAsync(grant);

        // Assert
        Assert.NotNull(capturedGrant);
        Assert.Equal(grant.Key, capturedGrant.Key);
        Assert.Equal(grant.Type, capturedGrant.Type);
        Assert.Equal(grant.SubjectId, capturedGrant.SubjectId);
        Assert.Equal(grant.SessionId, capturedGrant.SessionId);
        Assert.Equal(grant.ClientId, capturedGrant.ClientId);
        Assert.Equal(grant.Description, capturedGrant.Description);
        Assert.Equal(grant.CreationTime, capturedGrant.CreationTime);
        Assert.Equal(grant.Expiration, capturedGrant.Expiration);
        Assert.Equal(grant.ConsumedTime, capturedGrant.ConsumedTime);
        Assert.Equal(grant.Data, capturedGrant.Data);
    }

    private static PersistedGrant CreateTestGrant(string key, DateTime? expiration)
    {
        return new PersistedGrant
        {
            Key = key,
            Type = "authorization_code",
            SubjectId = "test-subject",
            ClientId = "test-client",
            CreationTime = DateTime.UtcNow,
            Expiration = expiration,
            Data = "{\"test\":\"data\"}"
        };
    }
}

using Bit.Core.Auth.Services.Implementations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

public class OAuthConsentNonceStoreTests
{
    // Exercises the no-Redis fallback path against a real in-memory distributed cache.
    private static OAuthConsentNonceStore CreateStore() =>
        new(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            Array.Empty<IConnectionMultiplexer>());

    [Fact]
    public async Task TryConsumeAsync_IssuedNonce_SucceedsExactlyOnce()
    {
        var store = CreateStore();
        var nonce = await store.IssueAsync();

        Assert.True(await store.TryConsumeAsync(nonce));
        // Single-use: a second consume of the same nonce fails.
        Assert.False(await store.TryConsumeAsync(nonce));
    }

    [Fact]
    public async Task TryConsumeAsync_UnknownNonce_ReturnsFalse()
    {
        var store = CreateStore();
        Assert.False(await store.TryConsumeAsync("not-a-real-nonce"));
    }

    [Fact]
    public async Task TryConsumeAsync_EmptyNonce_ReturnsFalse()
    {
        var store = CreateStore();
        Assert.False(await store.TryConsumeAsync(string.Empty));
    }
}

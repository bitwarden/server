#nullable enable

using System.Text.Json;
using Bit.Core.Auth.Identity.TokenProviders;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

/// <summary>
/// Covers <see cref="OtpTokenProvider{TOptions}.ValidateTokenWithFailureLimitAsync"/> against a real in-memory
/// cache, so the failure count round-trips through the stored entry the same way it does in production.
/// </summary>
public class OtpTokenProviderFailureLimitTests
{
    private const string TokenProviderName = "TestProvider";
    private const string Purpose = "TestPurpose";
    private const string UniqueIdentifier = "user-1";
    private const string CacheKey = $"{TokenProviderName}_{Purpose}_{UniqueIdentifier}";
    private const int MaxFailedAttempts = 5;

    private readonly RecordingDistributedCache _cache = new();
    // Starts at the real time, because the in-memory cache rejects an absolute expiry that is already past.
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
    private readonly DefaultOtpTokenProviderOptions _options = new();
    private readonly OtpTokenProvider<DefaultOtpTokenProviderOptions> _sut;

    public OtpTokenProviderFailureLimitTests()
    {
        _sut = new OtpTokenProvider<DefaultOtpTokenProviderOptions>(_cache, Options.Create(_options), _timeProvider);
    }

    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_ValidToken_ReturnsTrueAndRemovesEntry()
    {
        var token = await GenerateAsync();

        var result = await ValidateWithLimitAsync(token);

        Assert.True(result);
        Assert.Null(await _cache.GetAsync(CacheKey));
    }

    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_OneFewerWrongTokensThanLimit_ValidTokenStillValidates()
    {
        var token = await GenerateAsync();
        for (var i = 0; i < MaxFailedAttempts - 1; i++)
        {
            Assert.False(await ValidateWithLimitAsync(WrongTokenFor(token)));
        }

        var result = await ValidateWithLimitAsync(token);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_LimitReached_RemovesEntry()
    {
        var token = await GenerateAsync();
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            Assert.False(await ValidateWithLimitAsync(WrongTokenFor(token)));
        }

        Assert.Null(await _cache.GetAsync(CacheKey));
        Assert.False(await ValidateWithLimitAsync(token));
    }

    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_MismatchedBoundValue_CountsAsFailure()
    {
        var token = await GenerateAsync(boundValue: "expected");
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            Assert.False(await ValidateWithLimitAsync(token, boundValue: "other"));
        }

        var result = await ValidateWithLimitAsync(token, boundValue: "expected");

        Assert.False(result);
    }

    [Fact]
    public async Task GenerateTokenAsync_AfterWrongTokens_ResetsFailureCount()
    {
        var firstToken = await GenerateAsync();
        for (var i = 0; i < MaxFailedAttempts - 1; i++)
        {
            Assert.False(await ValidateWithLimitAsync(WrongTokenFor(firstToken)));
        }
        var secondToken = await GenerateAsync();
        for (var i = 0; i < MaxFailedAttempts - 1; i++)
        {
            Assert.False(await ValidateWithLimitAsync(WrongTokenFor(secondToken)));
        }

        var result = await ValidateWithLimitAsync(secondToken);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_MatchingBoundValue_ReturnsTrueAndRemovesEntry()
    {
        var token = await GenerateAsync(boundValue: "expected");

        var result = await ValidateWithLimitAsync(token, boundValue: "expected");

        Assert.True(result);
        Assert.Null(await _cache.GetAsync(CacheKey));
    }

    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_BoundTokenValidatedWithoutBoundValue_ReturnsFalse()
    {
        var token = await GenerateAsync(boundValue: "expected");

        var result = await ValidateWithLimitAsync(token);

        Assert.False(result);
    }

    /// <summary>
    /// A wrong token rewrites the entry to store the new count. The rewrite keeps the expiry set at generation,
    /// so wrong tokens never extend the token's lifetime.
    /// </summary>
    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_WrongToken_KeepsOriginalExpiry()
    {
        var generatedAt = _timeProvider.GetUtcNow();
        var token = await GenerateAsync();
        _timeProvider.Advance(TimeSpan.FromMinutes(4));

        Assert.False(await ValidateWithLimitAsync(WrongTokenFor(token)));

        var rewriteOptions = _cache.LastSetOptions[CacheKey];
        Assert.Equal(
            generatedAt + _options.DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow,
            rewriteOptions.AbsoluteExpiration);
        Assert.Null(rewriteOptions.AbsoluteExpirationRelativeToNow);
        Assert.Null(rewriteOptions.SlidingExpiration);
    }

    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_WrongTokenAfterStoredExpiry_RemovesEntry()
    {
        var token = await GenerateAsync();
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        Assert.False(await ValidateWithLimitAsync(WrongTokenFor(token)));

        Assert.Null(await _cache.GetAsync(CacheKey));
    }

    /// <summary>
    /// The stored expiry is the first moment the token is no longer valid, so a wrong token at exactly that
    /// moment removes the entry.
    /// </summary>
    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_WrongTokenAtStoredExpiry_RemovesEntry()
    {
        var token = await GenerateAsync();
        _timeProvider.Advance(_options.DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow!.Value);

        Assert.False(await ValidateWithLimitAsync(WrongTokenFor(token)));

        Assert.Null(await _cache.GetAsync(CacheKey));
    }

    /// <summary>
    /// An entry that is not the JSON shape the provider writes is treated as absent, not as an error.
    /// </summary>
    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_RawTokenCacheEntry_ReturnsFalse()
    {
        await _cache.SetAsync(CacheKey, System.Text.Encoding.UTF8.GetBytes("123456"));

        var result = await ValidateWithLimitAsync("123456");

        Assert.False(result);
    }

    /// <summary>
    /// An entry without a stored expiry cannot be rewritten without extending its lifetime, so a wrong token
    /// removes it.
    /// </summary>
    [Fact]
    public async Task ValidateTokenWithFailureLimitAsync_EntryWithoutExpiry_WrongTokenRemovesEntry()
    {
        await _cache.SetAsync(CacheKey, JsonSerializer.SerializeToUtf8Bytes(new { Token = "123456" }));

        Assert.False(await ValidateWithLimitAsync("654321"));

        Assert.Null(await _cache.GetAsync(CacheKey));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ValidateTokenWithFailureLimitAsync_NonPositiveLimit_Throws(int maxFailedAttempts)
    {
        var token = await GenerateAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _sut.ValidateTokenWithFailureLimitAsync(
                token, TokenProviderName, Purpose, UniqueIdentifier, maxFailedAttempts));
    }

    /// <summary>
    /// Callers of <see cref="OtpTokenProvider{TOptions}.ValidateTokenAsync"/> keep unlimited guesses, and their
    /// wrong tokens do not count toward the limit of a failure-limited caller of the same token.
    /// </summary>
    [Fact]
    public async Task ValidateTokenAsync_WrongTokens_DoNotCountTowardFailureLimit()
    {
        var token = await GenerateAsync();
        for (var i = 0; i < MaxFailedAttempts * 2; i++)
        {
            Assert.False(await _sut.ValidateTokenAsync(
                WrongTokenFor(token), TokenProviderName, Purpose, UniqueIdentifier));
        }

        var result = await ValidateWithLimitAsync(token);

        Assert.True(result);
    }

    private async Task<string> GenerateAsync(string? boundValue = null)
    {
        var token = await _sut.GenerateTokenAsync(TokenProviderName, Purpose, UniqueIdentifier, boundValue);
        Assert.NotNull(token);
        return token;
    }

    private Task<bool> ValidateWithLimitAsync(string token, string? boundValue = null) =>
        _sut.ValidateTokenWithFailureLimitAsync(
            token, TokenProviderName, Purpose, UniqueIdentifier, MaxFailedAttempts, boundValue);

    /// <summary>
    /// Returns a token of the same length that differs from the given token at every position.
    /// </summary>
    private static string WrongTokenFor(string token) =>
        new(token.Select(c => c == '0' ? '1' : '0').ToArray());

    /// <summary>
    /// An in-memory distributed cache that also records the entry options of the last write to each key.
    /// </summary>
    private sealed class RecordingDistributedCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner =
            new(Options.Create(new MemoryDistributedCacheOptions()));

        public Dictionary<string, DistributedCacheEntryOptions> LastSetOptions { get; } = new();

        public byte[]? Get(string key) => _inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            _inner.GetAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            LastSetOptions[key] = options;
            _inner.Set(key, value, options);
        }

        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            LastSetOptions[key] = options;
            return _inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

        public void Remove(string key) => _inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
    }
}

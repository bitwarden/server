using System.Globalization;
using System.Text.Json;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class OtpTokenProvider<TOptions>(
    [FromKeyedServices("persistent")]
    IDistributedCache distributedCache,
    IOptions<TOptions> options,
    TimeProvider timeProvider) : IOtpTokenProvider<TOptions>
        where TOptions : DefaultOtpTokenProviderOptions
{
    private readonly TOptions _otpTokenProviderOptions = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// This is where the OTP tokens are stored.
    /// </summary>
    private readonly IDistributedCache _distributedCache = distributedCache;

    /// <summary>
    /// Used to store and fetch the OTP tokens from the distributed cache.
    /// The format is "{tokenProviderName}_{purpose}_{uniqueIdentifier}".
    /// </summary>
    private readonly string _cacheKeyFormat = "{0}_{1}_{2}";

    public async Task<string?> GenerateTokenAsync(string tokenProviderName, string purpose, string uniqueIdentifier, string? boundValue = null)
    {
        if (string.IsNullOrEmpty(tokenProviderName)
            || string.IsNullOrEmpty(purpose)
            || string.IsNullOrEmpty(uniqueIdentifier))
        {
            return null;
        }

        var cacheKey = BuildCacheKey(tokenProviderName, purpose, uniqueIdentifier);
        var token = CoreHelpers.SecureRandomString(
            _otpTokenProviderOptions.TokenLength,
            _otpTokenProviderOptions.TokenAlpha,
            true,
            false,
            _otpTokenProviderOptions.TokenNumeric,
            false);
        var cacheEntryOptions = _otpTokenProviderOptions.DistributedCacheEntryOptions;
        var entry = new OtpCacheEntry
        {
            Token = token,
            BoundValue = boundValue,
            ExpiresAt = cacheEntryOptions.AbsoluteExpiration
                ?? _timeProvider.GetUtcNow() + cacheEntryOptions.AbsoluteExpirationRelativeToNow,
        };
        await _distributedCache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(entry), cacheEntryOptions);
        return token;
    }

    public async Task<bool> ValidateTokenAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier, string? boundValue = null)
    {
        if (await MatchStoredTokenAsync(token, tokenProviderName, purpose, uniqueIdentifier, boundValue)
            is not { } match)
        {
            return false;
        }

        if (match.Valid)
        {
            await _distributedCache.RemoveAsync(match.CacheKey);
        }

        return match.Valid;
    }

    public async Task<bool> ValidateTokenWithFailureLimitAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier, int maxFailedAttempts, string? boundValue = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFailedAttempts);

        if (await MatchStoredTokenAsync(token, tokenProviderName, purpose, uniqueIdentifier, boundValue)
            is not { } match)
        {
            return false;
        }

        if (match.Valid)
        {
            await _distributedCache.RemoveAsync(match.CacheKey);
            return true;
        }

        var entry = match.Entry;
        entry.FailedAttempts++;

        // Rewriting an entry with no stored expiry, or one already past it, would extend its lifetime.
        if (entry.FailedAttempts >= maxFailedAttempts
            || entry.ExpiresAt == null
            || entry.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            await _distributedCache.RemoveAsync(match.CacheKey);
            return false;
        }

        await _distributedCache.SetAsync(
            match.CacheKey,
            JsonSerializer.SerializeToUtf8Bytes(entry),
            new DistributedCacheEntryOptions { AbsoluteExpiration = entry.ExpiresAt });
        return false;
    }

    /// <summary>
    /// Reads the stored entry and compares it with the provided token and bound value. Does not change the cache.
    /// </summary>
    /// <returns>
    /// The cache key, the stored entry, and whether the token and bound value match it; or <see langword="null"/>
    /// if an input is empty or no readable entry is stored.
    /// </returns>
    private async Task<(string CacheKey, OtpCacheEntry Entry, bool Valid)?> MatchStoredTokenAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier, string? boundValue)
    {
        if (string.IsNullOrEmpty(token)
            || string.IsNullOrEmpty(tokenProviderName)
            || string.IsNullOrEmpty(purpose)
            || string.IsNullOrEmpty(uniqueIdentifier))
        {
            return null;
        }

        var cacheKey = BuildCacheKey(tokenProviderName, purpose, uniqueIdentifier);
        var entry = await GetEntryAsync(cacheKey);
        if (entry == null)
        {
            return null;
        }

        var valid = entry.BoundValue == boundValue && CoreHelpers.FixedTimeEquals(token, entry.Token);
        return (cacheKey, entry, valid);
    }

    private string BuildCacheKey(string tokenProviderName, string purpose, string uniqueIdentifier)
    {
        return string.Format(CultureInfo.InvariantCulture, _cacheKeyFormat, tokenProviderName, purpose, uniqueIdentifier);
    }

    private async Task<OtpCacheEntry?> GetEntryAsync(string cacheKey)
    {
        var cachedValue = await _distributedCache.GetAsync(cacheKey);
        if (cachedValue == null || cachedValue.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OtpCacheEntry>(cachedValue);
        }
        catch (JsonException)
        {
            // Fail closed on any cache entry that isn't this JSON shape, rather than crash validation.
            return null;
        }
    }

    private sealed class OtpCacheEntry
    {
        public string Token { get; set; } = "";
        public string? BoundValue { get; set; }

        /// <summary>
        /// When the entry expires. Stored so a rewrite can keep the expiry set at generation.
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>
        /// Wrong tokens counted by <see cref="ValidateTokenWithFailureLimitAsync"/>.
        /// </summary>
        public int FailedAttempts { get; set; }
    }
}

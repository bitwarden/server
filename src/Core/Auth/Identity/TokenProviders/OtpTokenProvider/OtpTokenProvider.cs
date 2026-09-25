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
    IOptions<TOptions> options) : IOtpTokenProvider<TOptions>
        where TOptions : DefaultOtpTokenProviderOptions
{
    private readonly TOptions _otpTokenProviderOptions = options.Value;

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
        var entry = new OtpCacheEntry { Token = token, BoundValue = boundValue };
        await _distributedCache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(entry), _otpTokenProviderOptions.DistributedCacheEntryOptions);
        return token;
    }

    public async Task<bool> ValidateTokenAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier, string? boundValue = null)
    {
        if (string.IsNullOrEmpty(token)
            || string.IsNullOrEmpty(tokenProviderName)
            || string.IsNullOrEmpty(purpose)
            || string.IsNullOrEmpty(uniqueIdentifier))
        {
            return false;
        }

        var cacheKey = BuildCacheKey(tokenProviderName, purpose, uniqueIdentifier);
        var entry = await GetEntryAsync(cacheKey);
        if (entry == null)
        {
            return false;
        }

        var valid = entry.BoundValue == boundValue && CoreHelpers.FixedTimeEquals(token, entry.Token);
        if (valid)
        {
            await _distributedCache.RemoveAsync(cacheKey);
        }

        return valid;
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
    }
}

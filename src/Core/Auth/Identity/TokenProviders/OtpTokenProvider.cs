using System.Text;
using Bit.Core.Utilities;
using Core.Auth.Identity.TokenProviders;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity.TokenProviders;

/// <summary>
/// Constructor for the OTP token provider.
/// </summary>
/// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.
/// This is used as part of the cache key. This field should be a constant value
/// that describes the purpose of the token. This can be thought of like custom `GrantTypes`.</param>
/// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another. This is used as part of the cache key.
/// If you have collisions the key is not unique enough.</param>
/// <param name="distributedCache">Used to store and fetch the OTP tokens from the distributed cache.</param>
public class OtpTokenProvider(
    string purpose,
    string uniqueIdentifier,
    [FromKeyedServices("persistent")]
    IDistributedCache distributedCache) : IOtpTokenProvider
{
    /// <summary>
    /// This is where the OTP tokens are stored.
    /// </summary>
    private readonly IDistributedCache _distributedCache = distributedCache;

    /// <summary>
    /// Used to store and fetch the OTP tokens from the distributed cache.
    /// The format is "{purpose}_{uniqueIdentifier}".
    /// </summary>
    private readonly string _cacheKey = string.Format("{0}_{1}", purpose, uniqueIdentifier);

    /// <summary>
    /// Sets the cache entry options for the token provider.
    /// Default is 5 minutes expiration.
    /// </summary>
    public DistributedCacheEntryOptions _distributedCacheEntryOptions { get; protected set; } =
        new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

    /// <summary>
    /// Sets the length of the token to be generated. Default is 6 characters.
    /// </summary>
    public int TokenLength { get; protected set; } = 6;
    /// <summary>
    /// Sets whether the token should contain alphabetic characters. Default is false.
    /// </summary>
    public bool TokenAlpha { get; protected set; } = false;
    /// <summary>
    /// Sets whether the token should contain numeric characters. Default is true.
    /// </summary>
    public bool TokenNumeric { get; protected set; } = true;

    /// <summary>
    /// Generates a new one-time password (OTP) based on the configured parameters.
    /// The generated OTP is stored in the distributed cache with a key based on the unique identifier. If the
    /// Unique Identifier is already in use, it will overwrite and generate a new OTP with a refreshed TTL.
    /// </summary>
    /// <returns>generated token</returns>
    public async Task<string> GenerateTokenAsync()
    {
        var code = CoreHelpers.SecureRandomString(TokenLength, TokenAlpha, true, false, TokenNumeric, false);
        await _distributedCache.SetAsync(_cacheKey, Encoding.UTF8.GetBytes(code), _distributedCacheEntryOptions);
        return code;
    }

    /// <summary>
    /// Validates the provided token against the stored value in the distributed cache.
    /// </summary>
    /// <param name="token">string value matched against the unique identifier in the cache if found</param>
    /// <returns>true if the token matches what is fetched from the cache, false if not.</returns>
    public async Task<bool> ValidateTokenAsync(string token)
    {
        var cachedValue = await _distributedCache.GetAsync(_cacheKey);
        if (cachedValue == null)
        {
            return false;
        }

        var code = Encoding.UTF8.GetString(cachedValue);
        var valid = string.Equals(token, code);
        if (valid)
        {
            await _distributedCache.RemoveAsync(_cacheKey);
        }

        return valid;
    }

    /// <summary>
    /// Configures the token provider with the specified parameters.
    /// This method allows you to set the length of the token, and whether it should contain
    /// alphabetic and numeric characters.
    /// </summary>
    /// <param name="length">length of generated token</param>
    /// <param name="alpha">whether the token should contain alphabetic characters</param>
    /// <param name="numeric">whether the token should contain numeric characters</param>
    public void ConfigureToken(int length, bool alpha, bool numeric)
    {
        TokenLength = length;
        TokenAlpha = alpha;
        TokenNumeric = numeric;
    }

    public void SetCacheEntryOptions(DistributedCacheEntryOptions options)
    {
        _distributedCacheEntryOptions = options;
    }
}

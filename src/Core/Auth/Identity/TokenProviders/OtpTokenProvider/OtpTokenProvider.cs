using System.Text;
using Bit.Core.Utilities;
using Core.Auth.Identity.TokenProviders;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Identity.TokenProviders;

public class OtpTokenProvider(
    [FromKeyedServices("persistent")]
    IDistributedCache distributedCache) : IOtpTokenProvider
{
    /// <summary>
    /// This is where the OTP tokens are stored.
    /// </summary>
    private readonly IDistributedCache _distributedCache = distributedCache;

    /// <summary>
    /// Used to store and fetch the OTP tokens from the distributed cache.
    /// The format is "{tokenProviderName}_{purpose}_{uniqueIdentifier}".
    /// </summary>
    private readonly string _cacheKeyFormat = "{0}_{1}_{2}";

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

    public async Task<string?> GenerateTokenAsync(string tokenProviderName, string purpose, string uniqueIdentifier)
    {
        if (string.IsNullOrEmpty(tokenProviderName)
            || string.IsNullOrEmpty(purpose)
            || string.IsNullOrEmpty(uniqueIdentifier))
        {
            return null;
        }

        var cacheKey = string.Format(_cacheKeyFormat, tokenProviderName, purpose, uniqueIdentifier);
        var token = CoreHelpers.SecureRandomString(TokenLength, TokenAlpha, true, false, TokenNumeric, false);
        await _distributedCache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(token), _distributedCacheEntryOptions);
        return token;
    }

    public async Task<bool> ValidateTokenAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier)
    {
        if (string.IsNullOrEmpty(token)
            || string.IsNullOrEmpty(tokenProviderName)
            || string.IsNullOrEmpty(purpose)
            || string.IsNullOrEmpty(uniqueIdentifier))
        {
            return false;
        }

        var cacheKey = string.Format(_cacheKeyFormat, tokenProviderName, purpose, uniqueIdentifier);
        var cachedValue = await _distributedCache.GetAsync(cacheKey);
        if (cachedValue == null)
        {
            return false;
        }

        var code = Encoding.UTF8.GetString(cachedValue);
        var valid = string.Equals(token, code);
        if (valid)
        {
            await _distributedCache.RemoveAsync(cacheKey);
        }

        return valid;
    }

    public void ConfigureToken(OtpTokenProviderConfigurationOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options), "Options cannot be null.");
        }

        TokenLength = options.TokenLength;
        TokenAlpha = options.TokenAlpha;
        TokenNumeric = options.TokenNumeric;
        _distributedCacheEntryOptions = options.DistributedCacheEntryOptions;
    }
}

using System.Text;
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

    public async Task<string?> GenerateTokenAsync(string tokenProviderName, string purpose, string uniqueIdentifier)
    {
        if (string.IsNullOrEmpty(tokenProviderName)
            || string.IsNullOrEmpty(purpose)
            || string.IsNullOrEmpty(uniqueIdentifier))
        {
            return null;
        }

        var cacheKey = string.Format(_cacheKeyFormat, tokenProviderName, purpose, uniqueIdentifier);
        var token = CoreHelpers.SecureRandomString(
            _otpTokenProviderOptions.TokenLength,
            _otpTokenProviderOptions.TokenAlpha,
            true,
            false,
            _otpTokenProviderOptions.TokenNumeric,
            false);
        await _distributedCache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(token), _otpTokenProviderOptions.DistributedCacheEntryOptions);
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
}

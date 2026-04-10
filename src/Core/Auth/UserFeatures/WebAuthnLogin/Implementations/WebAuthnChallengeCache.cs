using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class WebAuthnChallengeCache : IWebAuthnChallengeCache
{
    private const string _cacheKeyPrefix = "WebAuthnLoginAssertion_";
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(17)
    };

    private readonly IDistributedCache _distributedCache;

    public WebAuthnChallengeCache(
        [FromKeyedServices("persistent")] IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task StoreChallengeAsync(byte[] challenge)
    {
        var cacheKey = BuildCacheKey(challenge);
        await _distributedCache.SetAsync(cacheKey, [1], _cacheOptions);
    }

    public async Task<bool> ConsumeChallengeAsync(byte[] challenge)
    {
        var cacheKey = BuildCacheKey(challenge);
        var cached = await _distributedCache.GetAsync(cacheKey);
        if (cached == null)
        {
            return false;
        }

        await _distributedCache.RemoveAsync(cacheKey);
        return true;
    }

    private static string BuildCacheKey(byte[] challenge)
        => $"{_cacheKeyPrefix}{CoreHelpers.Base64UrlEncode(challenge)}";
}

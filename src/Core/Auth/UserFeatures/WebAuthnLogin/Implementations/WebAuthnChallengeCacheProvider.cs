using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;

internal class WebAuthnChallengeCacheProvider(
    [FromKeyedServices("persistent")] IDistributedCache distributedCache) : IWebAuthnChallengeCacheProvider
{
    private const string _cacheKeyPrefix = "WebAuthnLoginAssertion_";
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(17)
    };

    private readonly IDistributedCache _distributedCache = distributedCache;

    public async Task<bool> TryMarkChallengeAsUsedAsync(byte[] challenge)
    {
        var cacheKey = BuildCacheKey(challenge);
        var cached = await _distributedCache.GetAsync(cacheKey);
        if (cached != null)
        {
            return false;
        }

        await _distributedCache.SetAsync(cacheKey, [1], _cacheOptions);
        return true;
    }

    private static string BuildCacheKey(byte[] challenge)
        => $"{_cacheKeyPrefix}{CoreHelpers.Base64UrlEncode(challenge)}";
}

using System.Security.Cryptography;
using System.Text;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Services.Implementations;

/// <summary>
/// Stores OIDC consent nonces in the persistent distributed cache, keyed by the nonce value itself so each
/// is globally single-use, for the lifetime of the consent token.
/// </summary>
public class OAuthConsentNonceStore(
    [FromKeyedServices("persistent")] IDistributedCache distributedCache) : IOAuthConsentNonceStore
{
    private const string _cacheKeyPrefix = "OAuthConsentNonce_";
    private const int _nonceSizeBytes = 32;
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = OAuthConsentTokenable.Lifetime,
    };

    public async Task<string> IssueAsync()
    {
        var nonce = CoreHelpers.Base64UrlEncode(RandomNumberGenerator.GetBytes(_nonceSizeBytes));
        await distributedCache.SetAsync(BuildCacheKey(nonce), [1], _cacheOptions);
        return nonce;
    }

    public async Task<bool> TryConsumeAsync(string nonce)
    {
        if (string.IsNullOrEmpty(nonce))
        {
            return false;
        }

        var cacheKey = BuildCacheKey(nonce);
        var active = await distributedCache.GetAsync(cacheKey);
        if (active is null)
        {
            return false;
        }

        await distributedCache.RemoveAsync(cacheKey);
        return true;
    }

    private static string BuildCacheKey(string nonce)
    {
        // Key by a hash of the nonce so the cache key never contains the raw secret value.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(nonce));
        return $"{_cacheKeyPrefix}{CoreHelpers.Base64UrlEncode(hash)}";
    }
}

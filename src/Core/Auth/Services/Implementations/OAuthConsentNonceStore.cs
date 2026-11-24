#nullable enable

using System.Security.Cryptography;
using System.Text;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Bit.Core.Auth.Services.Implementations;

/// <summary>
/// Stores short-lived, single-use OIDC consent nonces for the lifetime of the consent token. When Redis
/// is configured the nonce is stored and consumed directly in Redis, so <see cref="TryConsumeAsync"/> is
/// a single atomic <c>DEL</c> — two concurrent consent callbacks can never both consume the same nonce.
/// Without Redis (e.g. single-instance self-hosted) it falls back to the default distributed cache with a
/// best-effort get-then-remove.
/// </summary>
public class OAuthConsentNonceStore : IOAuthConsentNonceStore
{
    private const string _cacheKeyPrefix = "OAuthConsentNonce_";
    private const int _nonceSizeBytes = 32;
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = OAuthConsentTokenable.Lifetime,
    };

    private readonly IDistributedCache _distributedCache;
    private readonly IDatabase? _redisDatabase;

    public OAuthConsentNonceStore(
        IDistributedCache distributedCache,
        IEnumerable<IConnectionMultiplexer> connectionMultiplexers)
    {
        _distributedCache = distributedCache;
        // The shared multiplexer is only registered when Redis is configured; without it there is no
        // atomic primitive and we use the distributed-cache fallback.
        _redisDatabase = connectionMultiplexers.FirstOrDefault()?.GetDatabase();
    }

    public async Task<string> IssueAsync()
    {
        var nonce = CoreHelpers.Base64UrlEncode(RandomNumberGenerator.GetBytes(_nonceSizeBytes));
        var key = BuildCacheKey(nonce);

        if (_redisDatabase != null)
        {
            await _redisDatabase.StringSetAsync(key, "1", OAuthConsentTokenable.Lifetime);
        }
        else
        {
            await _distributedCache.SetAsync(key, [1], _cacheOptions);
        }

        return nonce;
    }

    public async Task<bool> TryConsumeAsync(string nonce)
    {
        if (string.IsNullOrEmpty(nonce))
        {
            return false;
        }

        var key = BuildCacheKey(nonce);

        if (_redisDatabase != null)
        {
            // Redis executes commands serially, so exactly one concurrent caller's DEL removes the
            // key and returns true; every other caller sees it already gone and returns false.
            return await _redisDatabase.KeyDeleteAsync(key);
        }

        // Fallback without Redis: not atomic, acceptable for single-instance deployments.
        var active = await _distributedCache.GetAsync(key);
        if (active is null)
        {
            return false;
        }

        await _distributedCache.RemoveAsync(key);
        return true;
    }

    private static string BuildCacheKey(string nonce)
    {
        // Key by a hash of the nonce so the stored key never contains the raw secret value.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(nonce));
        return $"{_cacheKeyPrefix}{CoreHelpers.Base64UrlEncode(hash)}";
    }
}

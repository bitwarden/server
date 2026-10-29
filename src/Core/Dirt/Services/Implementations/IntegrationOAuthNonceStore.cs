using System.Security.Cryptography;
using System.Text;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Utilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Dirt.Services.Implementations;

/// <summary>
/// Stores OAuth nonces in the persistent distributed cache, keyed by integration, for the lifetime of the OAuth state.
/// </summary>
/// <remarks>
/// The read and remove in <see cref="TryConsumeAsync"/> are not atomic, so two simultaneous callbacks with the same
/// state could both succeed. Completing an integration is further guarded by the controllers only configuring an
/// integration that is still in the Initiated state.
/// </remarks>
public class IntegrationOAuthNonceStore(
    [FromKeyedServices("persistent")] IDistributedCache distributedCache) : IIntegrationOAuthNonceStore
{
    private const string _cacheKeyPrefix = "IntegrationOAuthNonce_";
    private const int _nonceSizeBytes = 32;
    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = IntegrationOAuthStateTokenable.Lifetime
    };

    public async Task<string> IssueAsync(Guid integrationId)
    {
        var nonce = CoreHelpers.Base64UrlEncode(RandomNumberGenerator.GetBytes(_nonceSizeBytes));
        await distributedCache.SetAsync(BuildCacheKey(integrationId), Encoding.UTF8.GetBytes(nonce), _cacheOptions);
        return nonce;
    }

    public async Task<bool> TryConsumeAsync(Guid integrationId, string nonce)
    {
        if (string.IsNullOrEmpty(nonce))
        {
            return false;
        }

        var cacheKey = BuildCacheKey(integrationId);
        var activeNonce = await distributedCache.GetAsync(cacheKey);
        if (activeNonce is null ||
            !CryptographicOperations.FixedTimeEquals(activeNonce, Encoding.UTF8.GetBytes(nonce)))
        {
            return false;
        }

        await distributedCache.RemoveAsync(cacheKey);
        return true;
    }

    private static string BuildCacheKey(Guid integrationId) => $"{_cacheKeyPrefix}{integrationId:N}";
}

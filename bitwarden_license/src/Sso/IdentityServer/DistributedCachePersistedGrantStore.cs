using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Sso.IdentityServer;

/// <summary>
/// Distributed cache-backed persisted grant store for short-lived grants.
/// Uses IFusionCache (which wraps IDistributedCache) for horizontal scaling support,
/// and fall back to in-memory caching if Redis is not configured.
/// Designed for SSO authorization codes which are short-lived (5 minutes) and single-use.
/// </summary>
/// <remarks>
/// This is purposefully a different implementation from how Identity solves Persisted Grants.
/// Because even flavored grant store, e.g., AuthorizationCodeGrantStore, can add intermediary
/// logic to a grant's handling by type, the fact that they all wrap IdentityServer's IPersistedGrantStore
/// leans on IdentityServer's opinion that all grants, regardless of type, go to the same persistence
/// mechanism (cache, database).
/// <seealso href="https://docs.duendesoftware.com/identityserver/reference/stores/persisted-grant-store/"/>
/// </remarks>
public class DistributedCachePersistedGrantStore : IPersistedGrantStore
{
    private readonly IFusionCache _cache;

    public DistributedCachePersistedGrantStore(
        [FromKeyedServices("sso-grants")] IFusionCache cache)
    {
        _cache = cache;
    }

    public async Task<PersistedGrant?> GetAsync(string key)
    {
        var result = await _cache.TryGetAsync<PersistedGrant>(key);

        if (!result.HasValue)
        {
            return null;
        }

        var grant = result.Value;

        // Check if grant has expired - remove expired grants from cache
        if (grant.Expiration.HasValue && grant.Expiration.Value < DateTime.UtcNow)
        {
            await RemoveAsync(key);
            return null;
        }

        return grant;
    }

    public Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        // Cache stores are key-value based and don't support querying by filter criteria.
        // This method is typically used for cleanup operations on long-lived grants in databases.
        // For SSO's short-lived authorization codes, we rely on TTL expiration instead.

        return Task.FromResult(Enumerable.Empty<PersistedGrant>());
    }

    public Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        // Revocation Strategy: SSO's logout flow (AccountController.LogoutAsync) only clears local
        // authentication cookies and performs federated logout with external IdPs. It does not invoke
        // Duende's EndSession or TokenRevocation endpoints. Authorization codes are single-use and expire
        // within 5 minutes, making explicit revocation unnecessary for SSO's security model.
        // https://docs.duendesoftware.com/identityserver/reference/stores/persisted-grant-store/

        // Cache stores are key-value based and don't support bulk deletion by filter.
        // This method is typically used for cleanup operations on long-lived grants in databases.
        // For SSO's short-lived authorization codes, we rely on TTL expiration instead.

        return Task.FromResult(0);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public async Task StoreAsync(PersistedGrant grant)
    {
        // Calculate TTL based on grant expiration
        var duration = grant.Expiration.HasValue
            ? grant.Expiration.Value - DateTime.UtcNow
            : TimeSpan.FromMinutes(5); // Default to 5 minutes if no expiration set

        // Ensure positive duration
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        // Cache key "sso-grants:" is configured by service registration. Going through the consumed KeyedService will
        // give us a consistent cache key prefix for these grants.
        await _cache.SetAsync(
            grant.Key,
            grant,
            new FusionCacheEntryOptions
            {
                Duration = duration,
                // Keep distributed cache enabled for multi-instance scenarios
                // When Redis isn't configured, FusionCache gracefully uses only L1 (in-memory)
            }.SetSkipDistributedCache(false, false));
    }
}

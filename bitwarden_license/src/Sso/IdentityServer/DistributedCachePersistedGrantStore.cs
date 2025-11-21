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
    private readonly ILogger<DistributedCachePersistedGrantStore> _logger;

    private const string KeyPrefix = "grant:";

    public DistributedCachePersistedGrantStore(
        [FromKeyedServices("sso-grants")] IFusionCache cache,
        ILogger<DistributedCachePersistedGrantStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<PersistedGrant?> GetAsync(string key)
    {
        var cacheKey = GetCacheKey(key);

        var result = await _cache.TryGetAsync<PersistedGrant>(cacheKey);

        if (!result.HasValue)
        {
            _logger.LogDebug("Grant {Key} not found in cache", key);
            return null;
        }

        var grant = result.Value;

        // Check expiration
        if (grant.Expiration.HasValue && grant.Expiration.Value < DateTime.UtcNow)
        {
            _logger.LogDebug("Grant {Key} has expired", key);
            await RemoveAsync(key);
            return null;
        }

        _logger.LogDebug("Retrieved grant {Key} of type {GrantType}", key, grant.Type);
        return grant;
    }

    public Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        // Cache stores are key-value based and don't support querying by filter criteria.
        // This method is typically used for cleanup operations on long-lived grants in databases.
        // For SSO's short-lived authorization codes, we rely on TTL expiration instead.
        _logger.LogDebug(
            "GetAllAsync called on cache-backed store with filter SubjectId={SubjectId}, SessionId={SessionId}, ClientId={ClientId}, Type={Type}. " +
            "Cache stores do not support filtering. Returning empty collection.",
            filter.SubjectId, filter.SessionId, filter.ClientId, filter.Type);

        return Task.FromResult(Enumerable.Empty<PersistedGrant>());
    }

    public Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        // Cache stores are key-value based and don't support bulk deletion by filter.
        // This method is typically used for cleanup operations on long-lived grants in databases.
        // For SSO's short-lived authorization codes, we rely on TTL expiration instead.
        _logger.LogDebug(
            "RemoveAllAsync called on cache-backed store with filter SubjectId={SubjectId}, SessionId={SessionId}, ClientId={ClientId}, Type={Type}. " +
            "Cache stores do not support filtering. No action taken.",
            filter.SubjectId, filter.SessionId, filter.ClientId, filter.Type);

        return Task.FromResult(0);
    }

    public async Task RemoveAsync(string key)
    {
        var cacheKey = GetCacheKey(key);

        await _cache.RemoveAsync(cacheKey);

        _logger.LogDebug("Removed grant {Key} from cache", key);
    }

    public async Task StoreAsync(PersistedGrant grant)
    {
        var cacheKey = GetCacheKey(grant.Key);

        // Calculate TTL based on grant expiration
        var duration = grant.Expiration.HasValue
            ? grant.Expiration.Value - DateTime.UtcNow
            : TimeSpan.FromMinutes(5); // Default to 5 minutes if no expiration set

        // Ensure positive duration
        if (duration <= TimeSpan.Zero)
        {
            _logger.LogWarning("Grant {Key} has already expired. Not storing in cache.", grant.Key);
            return;
        }

        await _cache.SetAsync(
            cacheKey,
            grant,
            new FusionCacheEntryOptions
            {
                Duration = duration,
                // Keep distributed cache enabled for multi-instance scenarios
                // When Redis isn't configured, FusionCache gracefully uses only L1 (in-memory)
            }.SetSkipDistributedCache(false, false));

        _logger.LogDebug("Stored grant {Key} of type {GrantType} with TTL {Duration}s",
            grant.Key, grant.Type, duration.TotalSeconds);
    }

    private string GetCacheKey(string key) => $"{KeyPrefix}{key}";
}

namespace Bit.Sso.Utilities;

public static class PersistedGrantsDistributedCacheConstants
{
    /// <summary>
    /// The SSO Persisted Grant cache key. Identifies the keyed service consumed by the SSO Persisted Grant Store as
    /// well as the cache key/namespace for grant storage.
    /// </summary>
    public const string CacheKey = "sso-grants";
}

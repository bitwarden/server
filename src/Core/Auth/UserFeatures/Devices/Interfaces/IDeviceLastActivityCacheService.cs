namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Suppresses redundant <c>Device.LastActivityDate</c> writes by recording a sentinel entry per
/// <c>(userId, identifier)</c> with a TTL set by <c>GlobalSettings.DeviceLastActivityCacheTtlHours</c>.
/// Presence means the device's activity has already been persisted within the TTL window — callers
/// skip the DB write; on expiry the next activity event writes and refreshes the entry. Device
/// identifiers are unique per user, not globally. On self-hosted without Cosmos, the cache is
/// DB-backed.
/// <para>
/// The TTL is the effective update cadence, so <c>LastActivityDate</c> can lag actual activity by
/// up to that window. The <c>Device_UpdateLastActivity*</c> stored procedures still guard against
/// backwards or redundant writes (day-level on <c>LastActivityDate</c>, value-level on
/// <c>ClientVersion</c>); these would permit writes more often than the cache allows, so they act
/// as a safety net for eviction or bypass rather than the cadence gate.
/// </para>
/// </summary>
public interface IDeviceLastActivityCacheService
{
    /// <summary>
    /// Returns <c>true</c> if a cache entry exists for this <c>(userId, identifier)</c>, meaning
    /// the device's activity has already been recorded within the TTL window. The caller should
    /// skip the DB write.
    /// </summary>
    Task<bool> IsUpToDateAsync(Guid userId, string identifier);

    /// <summary>
    /// Records that an activity event has been persisted for this device. Sets a cache entry
    /// that expires after <c>GlobalSettings.DeviceLastActivityCacheTtlHours</c>.
    /// </summary>
    Task RecordUpdateAsync(Guid userId, string identifier);
}

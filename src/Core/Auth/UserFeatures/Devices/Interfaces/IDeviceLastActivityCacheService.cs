namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Distributed cache that suppresses redundant <c>Device.LastActivityDate</c> writes by recording
/// a sentinel entry per device with a configurable TTL. Each cached entry indicates that an
/// activity event has already been persisted for this device within the TTL window — the caller
/// should skip the DB write. Once the entry expires, the next activity event produces a write
/// and refreshes the cache. Keys are scoped to <c>(userId, identifier)</c>; device identifiers
/// are unique per user, not globally. The TTL is configurable via
/// <c>GlobalSettings.DeviceLastActivityCacheTtlHours</c>.
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

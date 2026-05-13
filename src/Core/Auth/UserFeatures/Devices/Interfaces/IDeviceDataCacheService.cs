namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Distributed cache that suppresses DB writes when both today's date and the supplied client
/// version match the last recorded bump. A version change is a cache miss and produces a write —
/// the cache only short-circuits calls where neither input changed.
/// Keys are scoped to <c>(userId, identifier)</c>; device identifiers are unique per user, not
/// globally. On self-hosted without Cosmos, the cache is DB-backed; the SP guards ensure correctness
/// regardless.
/// </summary>
public interface IDeviceDataCacheService
{
    /// <summary>
    /// Returns <c>true</c> if the device has already been bumped today AND the cached client version
    /// matches the supplied one. A hit means both columns are up-to-date; the caller should skip the DB.
    /// </summary>
    Task<bool> IsUpToDateAsync(Guid userId, string identifier, string? clientVersion);

    /// <summary>
    /// Records today's date and the supplied client version as the most recent bump for this device.
    /// </summary>
    Task RecordBumpAsync(Guid userId, string identifier, string? clientVersion);
}

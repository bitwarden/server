namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Distributed cache that suppresses DB writes when today's date and the supplied client version
/// match the last recorded update. A change to any tracked input is a cache miss and produces a
/// write — the cache only short-circuits calls where nothing about the activity event has changed.
/// Keys are scoped to <c>(userId, identifier)</c>; device identifiers are unique per user, not
/// globally. On self-hosted without Cosmos, the cache is DB-backed; the SP guards ensure correctness
/// regardless.
/// <para>
/// The cached value composes the per-event facts (currently date + client version). If
/// <see cref="IUpdateDeviceLastActivityCommand"/> grows to track additional last-observed
/// properties (last IP, OS, etc.), they would be folded into the composed value here so a cache
/// hit continues to mean "every tracked input matches what we last wrote."
/// </para>
/// </summary>
public interface IDeviceLastActivityCacheService
{
    /// <summary>
    /// Returns <c>true</c> if today's date AND the cached client version match the supplied one.
    /// A hit means the activity event is already represented; the caller should skip the DB.
    /// </summary>
    Task<bool> IsUpToDateAsync(Guid userId, string identifier, string? clientVersion);

    /// <summary>
    /// Records today's date and the supplied client version as the most recent activity event for
    /// this device.
    /// </summary>
    Task RecordUpdateAsync(Guid userId, string identifier, string? clientVersion);
}

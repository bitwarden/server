using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Updates a device's last-activity state, gated by a distributed cache to avoid redundant
/// database writes. A single DB round-trip is issued when a write is needed; otherwise the
/// call short-circuits on cache hit.
///
/// <para>
/// "Last activity" names the <em>event</em> of the device's most recent appearance, not just one
/// column. The fields written are the set of facts we observed about that event: today that's
/// <c>LastActivityDate</c> (when it occurred) and <c>ClientVersion</c> (what was running at the
/// time). <c>ClientVersion</c> is treated as a property of the activity event rather than an
/// independent value — readers should think of it the same way "last seen on Chrome 124" pairs
/// a timestamp with the client observed at that moment.
/// </para>
///
/// <para>
/// The contract is intentionally event-oriented so additional last-observed properties (e.g. last
/// IP, OS, device model) can be added without renaming this command. New fields would flow through
/// as additional parameters (or a single options object as the surface grows), into the repository
/// methods, and into the SP / EF execute-update — the event-based naming holds regardless of how
/// many facets we track.
/// </para>
/// </summary>
public interface IUpdateDeviceLastActivityCommand
{
    /// <summary>
    /// Updates the device's last-activity state — <c>LastActivityDate</c> to today (if not already
    /// today) and <c>ClientVersion</c> to <paramref name="clientVersion"/> (if non-null and
    /// different from the stored value) — using the resolved <see cref="Device"/> object.
    /// </summary>
    Task UpdateAsync(Device device, string? clientVersion);

    /// <summary>
    /// Same as <see cref="UpdateAsync"/>, but for callers that don't have the <see cref="Device"/>
    /// entity loaded (e.g. refresh-token path).
    /// </summary>
    Task UpdateByIdentifierAndUserIdAsync(string identifier, Guid userId, string? clientVersion);
}

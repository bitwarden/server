using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Updates a device's last-activity state, gated by a distributed cache: a single DB round-trip
/// when a write is needed, short-circuit on cache hit.
///
/// <para>
/// "Last activity" names the <em>event</em> of the device's most recent appearance — the fields
/// written are facts observed at that moment: <c>LastActivityDate</c> (when) and
/// <c>ClientVersion</c> (what was running). Think "last seen on Chrome 124" — version is a
/// property of the event, not an independent value.
/// </para>
///
/// <para>
/// The event-oriented contract lets us add other last-observed facets (last IP, OS, device model)
/// as parameters without renaming the command — they flow through to the repository and SP / EF
/// execute-update the same way.
/// </para>
/// </summary>
public interface IUpdateDeviceLastActivityCommand
{
    /// <summary>
    /// Updates the device's last-activity state — <c>LastActivityDate</c> to today (if not already
    /// today) and <c>ClientVersion</c> to <paramref name="clientVersion"/> (if non-null and
    /// different from the stored value) — using the resolved <see cref="Device"/> object.
    /// </summary>
    /// <param name="clientVersion">
    /// The client version observed for this activity event. A <c>null</c> value is treated as
    /// "no opinion" and leaves the stored value untouched — it does <em>not</em> clear an existing
    /// version. Clearing would require a separate code path / sentinel.
    /// </param>
    Task UpdateAsync(Device device, string? clientVersion);

    /// <summary>
    /// Same as <see cref="UpdateAsync"/>, but for callers that don't have the <see cref="Device"/>
    /// entity loaded (e.g. refresh-token path). <paramref name="clientVersion"/> follows the same
    /// null-is-no-op semantics described on <see cref="UpdateAsync"/>.
    /// </summary>
    Task UpdateByIdentifierAndUserIdAsync(string identifier, Guid userId, string? clientVersion);
}

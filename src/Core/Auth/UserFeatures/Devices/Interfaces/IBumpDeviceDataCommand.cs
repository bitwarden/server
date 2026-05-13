using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Updates a device's bumped data (<c>LastActivityDate</c> to today and <c>ClientVersion</c> to the
/// supplied value), gated by a distributed cache to avoid redundant database writes. A single DB
/// round-trip is issued when a write is needed; otherwise the call short-circuits on cache hit.
/// </summary>
public interface IBumpDeviceDataCommand
{
    /// <summary>
    /// Bumps the device's <c>LastActivityDate</c> (to today, if not already today) and
    /// <c>ClientVersion</c> (to <paramref name="clientVersion"/>, if non-null and different from
    /// the stored value), using the resolved <see cref="Device"/> object.
    /// </summary>
    Task BumpAsync(Device device, string? clientVersion);

    /// <summary>
    /// Same as <see cref="BumpAsync"/>, but for callers that don't have the <see cref="Device"/>
    /// entity loaded (e.g. refresh-token path).
    /// </summary>
    Task BumpByIdentifierAndUserIdAsync(string identifier, Guid userId, string? clientVersion);
}

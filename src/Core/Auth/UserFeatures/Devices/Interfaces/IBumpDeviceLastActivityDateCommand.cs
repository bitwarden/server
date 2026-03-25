using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Updates a device's <c>LastActivityDate</c> to the current UTC time, at most once per calendar day.
/// A distributed cache is checked first; writes are skipped if the device has already been bumped today.
/// </summary>
public interface IBumpDeviceLastActivityDateCommand
{
    /// <summary>Bumps the device's <c>LastActivityDate</c> by its <c>Id</c>, using the resolved <see cref="Device"/> object.</summary>
    Task BumpAsync(Device device);

    /// <summary>Bumps the device's <c>LastActivityDate</c> by <c>identifier</c> when the device <c>Id</c> is not available.</summary>
    Task BumpByIdentifierAsync(string identifier, Guid userId);
}

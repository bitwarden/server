namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Updates a device's <c>LastActivityDate</c> to the current UTC time, at most once per calendar day.
/// A distributed cache is checked first; writes are skipped if the device has already been bumped today.
/// </summary>
public interface IBumpDeviceLastActivityDateCommand
{
    Task BumpByIdAsync(Guid deviceId, string identifier, Guid userId);

    Task BumpByIdentifierAsync(string identifier, Guid userId);
}

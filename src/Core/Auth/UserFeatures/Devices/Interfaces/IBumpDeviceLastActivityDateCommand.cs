namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

public interface IBumpDeviceLastActivityDateCommand
{
    Task BumpByIdAsync(Guid deviceId, string identifier);
    Task BumpByIdentifierAsync(string identifier, Guid userId);
}

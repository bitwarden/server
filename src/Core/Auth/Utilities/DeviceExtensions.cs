using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Auth.Utilities;

public static class DeviceExtensions
{
    /// <summary>
    /// Gets a boolean representing if the device has enough information on it to determine whether or not it is trusted.
    /// </summary>
    public static bool IsTrusted(this Device device)
    {
        return !string.IsNullOrEmpty(device.EncryptedUserKey) &&
            !string.IsNullOrEmpty(device.EncryptedPublicKey) &&
            !string.IsNullOrEmpty(device.EncryptedPrivateKey);
    }
}

using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Auth.Utilities;

public static class DeviceExtensions
{
    /// <summary>
    /// Gets a boolean representing if the device has enough information on it to determine whether or not it is trusted.
    /// </summary>
    /// <remarks>
    /// It is possible for a device to be un-trusted client side and not notify the server side. This should not be
    /// the source of truth for whether a device is fully trusted and should just be considered that, to the server,
    /// a device has the necessary information to be "trusted".
    /// </remarks>
    public static bool IsTrusted(this Device device)
    {
        return !string.IsNullOrEmpty(device.EncryptedUserKey)
            && !string.IsNullOrEmpty(device.EncryptedPublicKey)
            && !string.IsNullOrEmpty(device.EncryptedPrivateKey);
    }
}

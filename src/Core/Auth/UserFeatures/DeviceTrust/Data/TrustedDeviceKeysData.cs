using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.DeviceTrust.Data;

/// <summary>
/// Keys that establish trusted device encryption for one of the user's devices.
/// </summary>
public class TrustedDeviceKeysData
{
    /// <summary>
    /// The client-generated identifier of a device that belongs to the user.
    /// </summary>
    public required string DeviceIdentifier { get; init; }

    /// <inheritdoc cref="Device.EncryptedUserKey" />
    public required string EncryptedUserKey { get; init; }

    /// <inheritdoc cref="Device.EncryptedPublicKey" />
    public required string EncryptedPublicKey { get; init; }

    /// <inheritdoc cref="Device.EncryptedPrivateKey" />
    public required string EncryptedPrivateKey { get; init; }
}
